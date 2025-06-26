using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using SysBot.Pokemon.WinForms.WebApi;

namespace SysBot.Pokemon.WinForms;

public class SlaveInstance
{
    public string ProcessId { get; set; } = "";
    public int Port { get; set; }
    public string BotType { get; set; } = "";
    public string Version { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public DateTime LastSeen { get; set; } = DateTime.Now;
}

public static class WebApiExtensions
{
    private static BotServer? _server;
    private static TcpListener? _tcp;
    private static CancellationTokenSource? _cts;
    private static CancellationTokenSource? _monitorCts;
    private static Main? _main;

    private const int WebPort = 8080;
    private static int _tcpPort = 0;

    private static readonly Dictionary<string, SlaveInstance> _slaveInstances = new();

    public static void InitWebServer(this Main mainForm)
    {
        _main = mainForm;

        CleanupStalePortFiles();

        try
        {
            LogUtil.LogInfo("Searching for available TCP port starting from 8081...", "WebServer");
            _tcpPort = FindAvailablePort(8081);
            CreatePortFile();

            if (File.Exists("master"))
            {
                StartFullServer();
            }
            else
            {
                StartTcpOnly();
                StartMasterMonitor();
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to initialize web server: {ex.Message}", "WebServer");
        }
    }

    private static void CleanupStalePortFiles()
    {
        try
        {
            var files = Directory.GetFiles(".", "*.port");
            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    if (int.TryParse(content, out int port))
                    {
                        if (!IsPortInUse(port))
                        {
                            File.Delete(file);
                            LogUtil.LogInfo($"Cleaned up stale port file: {Path.GetFileName(file)}", "WebServer");
                        }
                    }
                }
                catch
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error cleaning up port files: {ex.Message}", "WebServer");
        }
    }

    private static void StartMasterMonitor()
    {
        _monitorCts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_monitorCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, _monitorCts.Token);

                    if (!File.Exists("master"))
                    {
                        var processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
                        var masterExists = false;

                        foreach (var proc in processes)
                        {
                            if (proc.Id != Environment.ProcessId)
                            {
                                try
                                {
                                    if (IsPortInUse(WebPort))
                                    {
                                        masterExists = true;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }

                        if (!masterExists)
                        {
                            TryTakeOverAsMaster();
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Master monitor error: {ex.Message}", "WebServer");
                }
            }
        }, _monitorCts.Token);
    }

    private static void TryTakeOverAsMaster()
    {
        try
        {
            File.WriteAllText("master", Environment.ProcessId.ToString());
            LogUtil.LogInfo("Taking over as master instance", "WebServer");

            _server?.Stop();
            _server = null;

            StartFullServer();
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to take over as master: {ex.Message}", "WebServer");
        }
    }

    private static bool TryAddUrlReservation(int port)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"http add urlacl url=http://+:{port}/ user=Everyone",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void StartTcpOnly()
    {
        LogUtil.LogInfo($"Starting TCP server only on port {_tcpPort}", "WebServer");
        StartTcp();
    }

    private static void StartFullServer()
    {
        LogUtil.LogInfo($"Starting full web server on port {WebPort} and TCP on port {_tcpPort}", "WebServer");
        
        // Only start TCP if not already running
        if (_tcp == null || !_tcp.Server.IsBound)
        {
            StartTcp();
        }
        else
        {
            LogUtil.LogInfo($"TCP server already running on port {_tcpPort}", "WebServer");
        }
        
        _server = new BotServer(_main!, WebPort, _tcpPort);
        _server.Start();
    }

    private static void StartTcp()
    {
        _cts = new CancellationTokenSource();
        _tcp = new TcpListener(IPAddress.Any, _tcpPort);
        _tcp.Start();

        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcp.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client), _cts.Token);
                }
                catch (ObjectDisposedException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_cts.Token.IsCancellationRequested)
                    {
                        LogUtil.LogError($"TCP server error: {ex.Message}", "WebServer");
                    }
                }
            }
        }, _cts.Token);
    }

    private static async Task HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[1024];
                var received = await stream.ReadAsync(buffer, 0, buffer.Length);
                var command = Encoding.UTF8.GetString(buffer, 0, received).Trim();

                LogUtil.LogInfo($"Received command: {command}", "TCP");

                var response = ProcessCommand(command);
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error handling client: {ex.Message}", "TCP");
        }
    }

    private static string ProcessCommand(string command)
    {
        if (_main == null)
            return "ERROR: Main form not initialized";

        // Check if this is a JSON slave registration
        if (command.StartsWith("{") && command.Contains("REGISTER_SLAVE"))
        {
            return ProcessSlaveRegistration(command);
        }

        // Parse command and optional bot ID (format: "COMMAND:BOTID")
        var parts = command.Split(':');
        var cmd = parts[0].ToUpperInvariant();
        var botId = parts.Length > 1 ? parts[1] : null;

        // Detect bot type for conditional commands
        var botType = DetectBotType();

        return cmd switch
        {
            "STARTALL" => ExecuteGlobalCommand(BotControlCommand.Start),
            "STOPALL" => ExecuteGlobalCommand(BotControlCommand.Stop),
            "IDLEALL" => ExecuteGlobalCommand(BotControlCommand.Idle),
            "RESUMEALL" => ExecuteGlobalCommand(BotControlCommand.Resume),
            "RESTARTALL" => ExecuteGlobalCommand(BotControlCommand.Restart),
            "REBOOTALL" => ExecuteGlobalCommand(BotControlCommand.RebootAndStop),
            "SCREENONALL" => ExecuteGlobalCommand(BotControlCommand.ScreenOnAll),
            "SCREENOFFALL" => ExecuteGlobalCommand(BotControlCommand.ScreenOffAll),
            
            // Raid Bot specific commands
            "REFRESHMAPALL" when botType == "Raid" => ExecuteGlobalCommand(BotControlCommand.Restart), // Placeholder
            "REFRESHMAP" when botType == "Raid" => ExecuteGlobalCommand(BotControlCommand.Restart), // Placeholder
            
            // Individual bot commands (with :botId)
            "START" when !string.IsNullOrEmpty(botId) => ExecuteIndividualCommand(BotControlCommand.Start, botId),
            "STOP" when !string.IsNullOrEmpty(botId) => ExecuteIndividualCommand(BotControlCommand.Stop, botId),
            "IDLE" when !string.IsNullOrEmpty(botId) => ExecuteIndividualCommand(BotControlCommand.Idle, botId),
            "RESUME" when !string.IsNullOrEmpty(botId) => ExecuteIndividualCommand(BotControlCommand.Resume, botId),
            "RESTART" when !string.IsNullOrEmpty(botId) => ExecuteIndividualCommand(BotControlCommand.Restart, botId),
            "REBOOT" when !string.IsNullOrEmpty(botId) => ExecuteIndividualCommand(BotControlCommand.RebootAndStop, botId),
            
            // Fallback: if no botId specified, execute on all bots
            "START" => ExecuteGlobalCommand(BotControlCommand.Start),
            "STOP" => ExecuteGlobalCommand(BotControlCommand.Stop),
            "IDLE" => ExecuteGlobalCommand(BotControlCommand.Idle),
            "RESUME" => ExecuteGlobalCommand(BotControlCommand.Resume),
            "RESTART" => ExecuteGlobalCommand(BotControlCommand.Restart),
            "REBOOT" => ExecuteGlobalCommand(BotControlCommand.RebootAndStop),
            "SCREENON" => ExecuteGlobalCommand(BotControlCommand.ScreenOnAll),
            "SCREENOFF" => ExecuteGlobalCommand(BotControlCommand.ScreenOffAll),
            
            "LISTBOTS" => GetBotsList(),
            "STATUS" => GetBotStatuses(botId),
            "ISREADY" => CheckReady(),
            "INFO" => GetInstanceInfo(),
            "VERSION" => GetVersion(),
            "BOTTYPE" => botType,
            "UPDATE" => TriggerUpdate(),
            _ => $"ERROR: Unknown command '{cmd}'. Use format 'COMMAND' or 'COMMAND:BOTID' for individual bot control."
        };
    }

    private static string ProcessSlaveRegistration(string jsonCommand)
    {
        try
        {
            var registration = JsonSerializer.Deserialize<JsonElement>(jsonCommand);
            
            if (registration.TryGetProperty("ProcessId", out var pidProp) &&
                registration.TryGetProperty("Port", out var portProp) &&
                registration.TryGetProperty("BotType", out var typeProp) &&
                registration.TryGetProperty("InstanceName", out var nameProp))
            {
                var processId = pidProp.GetInt32().ToString();
                var slaveInstance = new SlaveInstance
                {
                    ProcessId = processId,
                    Port = portProp.GetInt32(),
                    BotType = typeProp.GetString() ?? "Unknown",
                    Version = registration.TryGetProperty("Version", out var vProp) ? vProp.GetString() ?? "Unknown" : "Unknown",
                    InstanceName = nameProp.GetString() ?? $"Slave-{processId}",
                    LastSeen = DateTime.Now
                };

                var key = $"slave_{processId}:{slaveInstance.Port}";
                _slaveInstances[key] = slaveInstance;
                
                LogUtil.LogInfo($"Registered slave: {slaveInstance.InstanceName} ({slaveInstance.BotType}) on port {slaveInstance.Port}", "WebServer");
                return "OK: Slave registered successfully";
            }
            
            return "ERROR: Invalid registration data";
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error processing slave registration: {ex.Message}", "WebServer");
            return $"ERROR: Registration failed - {ex.Message}";
        }
    }

    private static string ExecuteIndividualCommand(BotControlCommand command, string botId)
    {
        try
        {
            var controllers = GetBotControllers();
            var targetBot = controllers.FirstOrDefault(c => 
                $"{c.State.Connection.IP}:{c.State.Connection.Port}" == botId ||
                c.State.Connection.IP == botId);

            if (targetBot == null)
            {
                return $"ERROR: Bot with ID '{botId}' not found. Available bots: {string.Join(", ", controllers.Select(c => $"{c.State.Connection.IP}:{c.State.Connection.Port}"))}";
            }

            _main!.BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
            {
                // Send command to specific bot controller
                var sendMethod = targetBot.GetType().GetMethod("SendCommand", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (sendMethod != null)
                {
                    sendMethod.Invoke(targetBot, new object[] { command });
                }
                else
                {
                    // Fallback: try to get the bot and send command directly
                    var bot = targetBot.GetBot();
                    if (bot != null)
                    {
                        switch (command)
                        {
                            case BotControlCommand.Start:
                                if (!bot.IsRunning) bot.Start();
                                break;
                            case BotControlCommand.Stop:
                                if (bot.IsRunning) bot.Stop();
                                break;
                            case BotControlCommand.Idle:
                                bot.Pause();
                                break;
                            case BotControlCommand.Resume:
                                bot.Start();
                                break;
                        }
                    }
                }
            }));

            return $"OK: {command} command sent to bot {botId} ({targetBot.State.Connection.IP})";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to execute {command} on bot {botId} - {ex.Message}";
        }
    }

    private static string TriggerUpdate()
    {
        try
        {
            if (_main == null)
                return "ERROR: Main form not initialized";

            var botType = DetectBotType();
            LogUtil.LogInfo($"Triggering update for {botType} Bot", "WebServer");

            _main.BeginInvoke((System.Windows.Forms.MethodInvoker)(async () =>
            {
                await TriggerBotTypeSpecificUpdate(botType);
            }));

            return $"OK: {botType} Bot update triggered";
        }
        catch (Exception ex)
        {
            return $"ERROR: Update failed - {ex.Message}";
        }
    }

    private static async Task TriggerBotTypeSpecificUpdate(string botType)
    {
        try
        {
            if (botType == "Raid")
            {
                // Raid Bot specific update logic
                LogUtil.LogInfo("Applying Raid Bot update from Raid Bot repository", "WebServer");
                await TriggerGenericUpdate("RaidBot");
            }
            else
            {
                // Trade Bot specific update logic  
                LogUtil.LogInfo("Applying Trade Bot update from Trade Bot repository", "WebServer");
                await TriggerGenericUpdate("TradeBot");
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Bot-specific update failed: {ex.Message}", "WebServer");
        }
    }

    private static async Task TriggerGenericUpdate(string targetBotType)
    {
        var updateCheckerType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == "UpdateChecker");

        if (updateCheckerType != null)
        {
            var checkMethod = updateCheckerType.GetMethod("CheckForUpdatesAsync", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            if (checkMethod != null)
            {
                var result = await (Task<(bool, bool, string)>)checkMethod.Invoke(null, new object[] { false });
                var (updateAvailable, _, newVersion) = result;
                
                if (updateAvailable)
                {
                    LogUtil.LogInfo($"Update available for {targetBotType}: {newVersion}", "WebServer");
                    
                    var updateFormType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == "UpdateForm");
                        
                    if (updateFormType != null)
                    {
                        var updateForm = Activator.CreateInstance(updateFormType, false, newVersion, true);
                        var performMethod = updateFormType.GetMethod("PerformUpdate");
                        performMethod?.Invoke(updateForm, null);
                    }
                }
                else
                {
                    LogUtil.LogInfo($"{targetBotType} is already up to date", "WebServer");
                }
                return;
            }
        }

        // Fallback for other implementations
        LogUtil.LogInfo($"Update check completed for {targetBotType}", "WebServer");
    }

    private static string ExecuteGlobalCommand(BotControlCommand command)
    {
        try
        {
            _main!.BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
            {
                var sendAllMethod = _main.GetType().GetMethod("SendAll",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                sendAllMethod?.Invoke(_main, new object[] { command });
            }));

            return $"OK: {command} command sent to all bots";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to execute {command} - {ex.Message}";
        }
    }

    private static string GetBotsList()
    {
        try
        {
            var botList = new List<object>();
            var config = GetConfig();
            var controllers = GetBotControllers();

            // If no controllers found in UI, try to get from Bots property
            if (controllers.Count == 0)
            {
                var botsProperty = _main!.GetType().GetProperty("Bots",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (botsProperty?.GetValue(_main) is List<PokeBotState> bots)
                {
                    foreach (var bot in bots)
                    {
                        botList.Add(new
                        {
                            Id = $"{bot.Connection.IP}:{bot.Connection.Port}",
                            Name = bot.Connection.IP,
                            RoutineType = bot.InitialRoutine.ToString(),
                            Status = "Unknown",
                            ConnectionType = bot.Connection.Protocol.ToString(),
                            bot.Connection.IP,
                            bot.Connection.Port
                        });
                    }

                    return System.Text.Json.JsonSerializer.Serialize(new { Bots = botList });
                }
            }

            foreach (var controller in controllers)
            {
                var state = controller.State;
                var botName = GetBotName(state, config);
                var status = controller.ReadBotState();

                botList.Add(new
                {
                    Id = $"{state.Connection.IP}:{state.Connection.Port}",
                    Name = botName,
                    RoutineType = state.InitialRoutine.ToString(),
                    Status = status,
                    ConnectionType = state.Connection.Protocol.ToString(),
                    state.Connection.IP,
                    state.Connection.Port
                });
            }

            // Add registered slaves to bot list
            foreach (var slave in _slaveInstances.Values)
            {
                // Check if slave is still alive (last seen within 30 seconds)
                if ((DateTime.Now - slave.LastSeen).TotalSeconds < 30)
                {
                    botList.Add(new
                    {
                        Id = $"slave_{slave.ProcessId}:{slave.Port}",
                        Name = slave.InstanceName,
                        RoutineType = $"{slave.BotType}Bot",
                        Status = "Connected",
                        ConnectionType = "TCP Slave",
                        IP = "localhost",
                        Port = slave.Port,
                        InstanceType = "Slave",
                        BotType = slave.BotType,
                        Version = slave.Version
                    });
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(new { Bots = botList });
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"GetBotsList error: {ex.Message}", "WebAPI");
            return $"ERROR: Failed to get bots list - {ex.Message}";
        }
    }

    private static string GetBotStatuses(string? botId)
    {
        try
        {
            var config = GetConfig();
            var controllers = GetBotControllers();

            if (string.IsNullOrEmpty(botId))
            {
                var statuses = controllers.Select(c => new
                {
                    Id = $"{c.State.Connection.IP}:{c.State.Connection.Port}",
                    Name = GetBotName(c.State, config),
                    Status = c.ReadBotState()
                }).ToList();

                return System.Text.Json.JsonSerializer.Serialize(statuses);
            }

            var botController = controllers.FirstOrDefault(c =>
                $"{c.State.Connection.IP}:{c.State.Connection.Port}" == botId);

            return botController?.ReadBotState() ?? "ERROR: Bot not found";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get status - {ex.Message}";
        }
    }

    private static string CheckReady()
    {
        try
        {
            var controllers = GetBotControllers();
            var hasRunningBots = controllers.Any(c => c.GetBot()?.IsRunning ?? false);
            return hasRunningBots ? "READY" : "NOT_READY";
        }
        catch
        {
            return "NOT_READY";
        }
    }

    private static string GetInstanceInfo()
    {
        try
        {
            var config = GetConfig();
            var version = GetVersion();
            var mode = config?.Mode.ToString() ?? "Unknown";
            var name = GetInstanceName(config, mode);
            var botType = DetectBotType();

            var info = new
            {
                Version = version,
                Mode = mode,
                Name = name,
                BotType = botType,
                Environment.ProcessId,
                Port = _tcpPort
            };

            return System.Text.Json.JsonSerializer.Serialize(info);
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get instance info - {ex.Message}";
        }
    }

    private static string DetectBotType()
    {
        try
        {
            var config = GetConfig();
            var mode = config?.Mode.ToString() ?? "";
            
            // Check for raid-specific modes or features
            if (mode.Contains("Raid", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("Den", StringComparison.OrdinalIgnoreCase))
            {
                return "Raid";
            }

            // Check for trade-specific modes
            if (mode.Contains("Trade", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("Distribution", StringComparison.OrdinalIgnoreCase))
            {
                return "Trade";
            }

            // Check assembly names for indicators
            var assemblyNames = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name ?? "").ToList();

            if (assemblyNames.Any(n => n.Contains("Raid", StringComparison.OrdinalIgnoreCase)))
                return "Raid";

            if (assemblyNames.Any(n => n.Contains("Trade", StringComparison.OrdinalIgnoreCase)))
                return "Trade";

            // Default to Trade for backward compatibility
            return "Trade";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static List<BotController> GetBotControllers()
    {
        var flpBotsField = _main!.GetType().GetField("FLP_Bots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (flpBotsField?.GetValue(_main) is FlowLayoutPanel flpBots)
        {
            return [.. flpBots.Controls.OfType<BotController>()];
        }

        return new List<BotController>();
    }

    private static ProgramConfig? GetConfig()
    {
        var configProp = _main?.GetType().GetProperty("Config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return configProp?.GetValue(_main) as ProgramConfig;
    }

    private static string GetBotName(PokeBotState state, ProgramConfig? config)
    {
        return state.Connection.IP;
    }

    private static string GetVersion()
    {
        try
        {
            // Try SVRaidBot version first for Raid Bot
            return SysBot.Pokemon.SV.BotRaid.Helpers.SVRaidBot.Version;
        }
        catch
        {
            // Fallback to assembly version
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }
    }

    private static string GetInstanceName(ProgramConfig? config, string mode)
    {
        try
        {
            var botType = DetectBotType();
            var baseName = $"{botType}Bot";
            
            // Simplified version without BotList dependency

            return $"{baseName} ({mode})";
        }
        catch
        {
            return "PokeBot";
        }
    }

    private static void CreatePortFile()
    {
        try
        {
            var fileName = $"{Environment.ProcessId}.port";
            File.WriteAllText(fileName, _tcpPort.ToString());
            LogUtil.LogInfo($"Created port file: {fileName} with port {_tcpPort}", "WebServer");
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to create port file: {ex.Message}", "WebServer");
        }
    }

    private static void CleanupPortFile()
    {
        try
        {
            var fileName = $"{Environment.ProcessId}.port";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                LogUtil.LogInfo($"Cleaned up port file: {fileName}", "WebServer");
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to cleanup port file: {ex.Message}", "WebServer");
        }
    }

    private static int FindAvailablePort(int startPort)
    {
        for (int port = startPort; port < startPort + 100; port++)
        {
            if (!IsPortInUse(port))
            {
                LogUtil.LogInfo($"Found available TCP port: {port}", "WebServer");
                return port;
            }
        }
        throw new InvalidOperationException($"Could not find available port starting from {startPort}");
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var listeners = ipGlobalProperties.GetActiveTcpListeners();
            return listeners.Any(listener => listener.Port == port);
        }
        catch
        {
            return false;
        }
    }

    public static void StopWebServer(this Main mainForm)
    {
        try
        {
            _monitorCts?.Cancel();
            _cts?.Cancel();
            _server?.Stop();
            _tcp?.Stop();
            CleanupPortFile();
            if (File.Exists("master"))
            {
                File.Delete("master");
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error stopping web server: {ex.Message}", "WebServer");
        }
    }
}
