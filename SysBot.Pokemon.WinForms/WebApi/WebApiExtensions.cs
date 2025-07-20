using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SysBot.Base;
using System.Diagnostics;

using SysBot.Pokemon.Helpers;
using SysBot.Pokemon.WinForms.WebApi;


namespace SysBot.Pokemon.WinForms;

public static class WebApiExtensions
{
    private static BotServer? _server;
    private static TcpListener? _tcp;
    private static CancellationTokenSource? _cts;
    private static CancellationTokenSource? _monitorCts;
    private static Main? _main;
    private static System.Threading.Timer? _scheduleTimer;

    private const int WebPort = 8080;
    private static int _tcpPort = 0;
    private static readonly object _portLock = new object();
    private static readonly Dictionary<int, DateTime> _portReservations = new();

    // Bot type detection
    public enum BotType
    {
        PokeBot,
        RaidBot,
        Unknown
    }

    public static void InitWebServer(this Main mainForm)
    {
        _main = mainForm;

        try
        {
            CleanupStalePortFiles();

            CheckPostRestartStartup(mainForm);

            if (IsPortInUse(WebPort))
            {
                LogUtil.LogInfo($"Web port {WebPort} is in use by another bot instance. Starting as slave...", "WebServer");
                lock (_portLock)
                {
                    _tcpPort = FindAvailablePort(8081);
                    ReservePort(_tcpPort);
                }
                StartTcpOnly();
                LogUtil.LogInfo($"Slave instance started with TCP port {_tcpPort}. Monitoring master...", "WebServer");

                StartMasterMonitor();
                StartScheduledRestartTimer();
                return;
            }

            TryAddUrlReservation(WebPort);

            lock (_portLock)
            {
                _tcpPort = FindAvailablePort(8081);
                ReservePort(_tcpPort);
            }
            LogUtil.LogInfo($"Starting as master web server on port {WebPort} with TCP port {_tcpPort}", "WebServer");
            StartFullServer();
            LogUtil.LogInfo($"Web interface is available at http://localhost:{WebPort}", "WebServer");

            StartScheduledRestartTimer();
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to initialize web server: {ex.Message}", "WebServer");
        }
    }

    private static void ReservePort(int port)
    {
        _portReservations[port] = DateTime.Now;
    }

    private static void ReleasePort(int port)
    {
        _portReservations.Remove(port);
    }

    private static void CleanupStalePortFiles()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var exeDir = Path.GetDirectoryName(exePath) ?? Program.WorkingDirectory;

            // Also clean up stale port reservations (older than 5 minutes)
            var staleReservations = _portReservations
                .Where(kvp => (DateTime.Now - kvp.Value).TotalMinutes > 5)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var port in staleReservations)
            {
                _portReservations.Remove(port);
            }

            // Clean up both PokeBot and RaidBot port files
            var pokeBotPortFiles = Directory.GetFiles(exeDir, "PokeBot_*.port");
            var raidBotPortFiles = Directory.GetFiles(exeDir, "SVRaidBot_*.port");
            var allPortFiles = pokeBotPortFiles.Concat(raidBotPortFiles);

            foreach (var portFile in allPortFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(portFile);
                    var pidStr = "";
                    
                    if (fileName.StartsWith("PokeBot_"))
                    {
                        pidStr = fileName.Substring("PokeBot_".Length);
                    }
                    else if (fileName.StartsWith("SVRaidBot_"))
                    {
                        pidStr = fileName.Substring("SVRaidBot_".Length);
                    }

                    if (int.TryParse(pidStr, out int pid))
                    {
                        if (pid == Environment.ProcessId)
                            continue;

                        try
                        {
                            var process = Process.GetProcessById(pid);
                            if (process.ProcessName.Contains("SysBot", StringComparison.OrdinalIgnoreCase) ||
                                process.ProcessName.Contains("PokeBot", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }
                        catch (ArgumentException)
                        {
                        }

                        File.Delete(portFile);
                        LogUtil.LogInfo($"Cleaned up stale port file: {Path.GetFileName(portFile)}", "WebServer");
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Error processing port file {portFile}: {ex.Message}", "WebServer");
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to cleanup stale port files: {ex.Message}", "WebServer");
        }
    }

    private static BotType DetectBotType()
    {
        try
        {
            // Try to detect PokeBot first
            var pokeBotType = Type.GetType("SysBot.Pokemon.Helpers.PokeBot, SysBot.Pokemon");
            if (pokeBotType != null)
                return BotType.PokeBot;

            // Try to detect RaidBot
            var raidBotType = Type.GetType("SysBot.Pokemon.SV.BotRaid.Helpers.SVRaidBot, SysBot.Pokemon");
            if (raidBotType != null)
                return BotType.RaidBot;

            return BotType.Unknown;
        }
        catch
        {
            return BotType.Unknown;
        }
    }

    private static void StartMasterMonitor()
    {
        _monitorCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            var random = new Random();

            while (!_monitorCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000 + random.Next(5000), _monitorCts.Token);

                    if (UpdateManager.IsSystemUpdateInProgress || UpdateManager.IsSystemRestartInProgress)
                    {
                        continue;
                    }

                    if (!IsPortInUse(WebPort))
                    {
                        LogUtil.LogInfo("Master web server is down. Attempting to take over...", "WebServer");

                        await Task.Delay(random.Next(1000, 3000));

                        if (!IsPortInUse(WebPort) && !UpdateManager.IsSystemUpdateInProgress && !UpdateManager.IsSystemRestartInProgress)
                        {
                            TryTakeOverAsMaster();
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogUtil.LogError($"Error in master monitor: {ex.Message}", "WebServer");
                }
            }
        }, _monitorCts.Token);
    }

    private static void TryTakeOverAsMaster()
    {
        try
        {
            TryAddUrlReservation(WebPort);

            _server = new BotServer(_main!, WebPort, _tcpPort);
            _server.Start();

            _monitorCts?.Cancel();
            _monitorCts = null;

            LogUtil.LogInfo($"Successfully took over as master web server on port {WebPort}", "WebServer");
            LogUtil.LogInfo($"Web interface is now available at http://localhost:{WebPort}", "WebServer");
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to take over as master: {ex.Message}", "WebServer");
            StartMasterMonitor();
        }
    }

    private static bool TryAddUrlReservation(int port)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"http add urlacl url=http://+:{port}/ user=Everyone",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Verb = "runas"
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void StartTcpOnly()
    {
        StartTcp();
        CreatePortFile();
    }

    private static void StartFullServer()
    {
        try
        {
            _server = new BotServer(_main!, WebPort, _tcpPort);
            _server.Start();
            StartTcp();
            CreatePortFile();
        }
        catch (Exception ex) when (ex.Message.Contains("conflicts with an existing registration"))
        {
            // Another instance became master first - gracefully become a slave
            LogUtil.LogInfo("Port 8080 conflict during startup, starting as slave", "WebServer");
            StartTcpOnly();  // This will create the port file as a slave
        }
    }

    private static void StartTcp()
    {
        _cts = new CancellationTokenSource();
        var retryCount = 0;
        var maxRetries = 5;
        var random = new Random();

        Task.Run(async () =>
        {
            while (retryCount < maxRetries && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _tcp = new TcpListener(System.Net.IPAddress.Any, _tcpPort);
                    _tcp.Start();

                    LogUtil.LogInfo($"TCP listener started successfully on port {_tcpPort}", "TCP");

                    while (!_cts.Token.IsCancellationRequested)
                    {
                        var tcpTask = _tcp.AcceptTcpClientAsync();
                        var tcs = new TaskCompletionSource<bool>();

                        using var registration = _cts.Token.Register(() => tcs.SetCanceled());
                        var completedTask = await Task.WhenAny(tcpTask, tcs.Task);
                        if (completedTask == tcpTask && tcpTask.IsCompletedSuccessfully)
                        {
                            _ = Task.Run(() => HandleClient(tcpTask.Result));
                        }
                    }
                    break; // Success, exit retry loop
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse && retryCount < maxRetries - 1)
                {
                    retryCount++;
                    LogUtil.LogInfo($"TCP port {_tcpPort} in use, finding new port (attempt {retryCount}/{maxRetries})", "TCP");

                    // Wait a bit before retrying
                    await Task.Delay(random.Next(500, 1500));

                    // Find a new port
                    lock (_portLock)
                    {
                        ReleasePort(_tcpPort);
                        _tcpPort = FindAvailablePort(_tcpPort + 1);
                        ReservePort(_tcpPort);
                    }

                    // Update the port file with the new port
                    CreatePortFile();
                }
                catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
                {
                    LogUtil.LogError($"TCP listener error: {ex.Message}", "TCP");
                    throw;
                }
            }

            if (retryCount >= maxRetries)
            {
                LogUtil.LogError($"Failed to start TCP listener after {maxRetries} attempts", "TCP");
                throw new InvalidOperationException("Unable to find available TCP port");
            }
        });
    }

    private static async Task HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                var command = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(command))
                {
                    var response = ProcessCommand(command);
                    await writer.WriteLineAsync(response);
                    await stream.FlushAsync();
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception ex) when (!(ex is IOException { InnerException: SocketException }))
        {
            LogUtil.LogError($"Error handling TCP client: {ex.Message}", "TCP");
        }
    }

    private static string ProcessCommand(string command)
    {
        if (_main == null)
            return "ERROR: Main form not initialized";

        var parts = command.Split(':');
        var cmd = parts[0].ToUpperInvariant();
        var botId = parts.Length > 1 ? parts[1] : null;

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
            "REFRESHMAPALL" => HandleRefreshMapAllCommand(),
            "LISTBOTS" => GetBotsList(),
            "STATUS" => GetBotStatuses(botId),
            "ISREADY" => CheckReady(),
            "INFO" => GetInstanceInfo(),
            "VERSION" => GetVersionForBotType(DetectBotType()),
            "UPDATE" => TriggerUpdate(),
            "SELFRESTARTALL" => TriggerSelfRestart(),
            _ => $"ERROR: Unknown command '{cmd}'"
        };
    }

    private static string TriggerUpdate()
    {
        try
        {
            if (_main == null)
                return "ERROR: Main form not initialized";

            var botType = DetectBotType();

            _main.BeginInvoke((MethodInvoker)(async () =>
            {
                try
                {
                    bool updateAvailable = false;
                    string newVersion = "";

                    if (botType == BotType.PokeBot)
                    {
                        // Use PokeBot UpdateChecker
                        var (available, _, version) = await UpdateChecker.CheckForUpdatesAsync(false);
                        updateAvailable = available;
                        newVersion = version;
                    }
                    else if (botType == BotType.RaidBot)
                    {
                        // Use RaidBot UpdateChecker
                        var raidUpdateCheckerType = Type.GetType("SysBot.Pokemon.SV.BotRaid.Helpers.UpdateChecker, SysBot.Pokemon");
                        if (raidUpdateCheckerType != null)
                        {
                            var checkMethod = raidUpdateCheckerType.GetMethod("CheckForUpdatesAsync");
                            if (checkMethod != null)
                            {
                                var task = (Task<(bool, string, string)>)checkMethod.Invoke(null, new object[] { false });
                                var result = await task;
                                updateAvailable = result.Item1;
                                newVersion = result.Item3;
                            }
                        }
                    }

                    if (updateAvailable && !string.IsNullOrEmpty(newVersion))
                    {
                        var updateForm = new UpdateForm(false, newVersion, true);
                        updateForm.PerformUpdate();
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Error in TriggerUpdate: {ex.Message}", "WebAPI");
                }
            }));

            return "OK: Update triggered";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static string TriggerSelfRestart()
    {
        try
        {
            if (_main == null)
                return "ERROR: Main form not initialized";

            Task.Run(async () =>
            {
                await Task.Delay(2000);
                _main.BeginInvoke((MethodInvoker)(() =>
                {
                    Application.Restart();
                }));
            });

            return "OK: Restart triggered";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static string ExecuteGlobalCommand(BotControlCommand command)
    {
        try
        {
            _main!.BeginInvoke((MethodInvoker)(() =>
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

    private static string HandleRefreshMapAllCommand()
    {
        try
        {
            // RefreshMap is only available for RaidBot, not PokeBot
            var botType = DetectBotType();
            if (botType != BotType.RaidBot)
            {
                return "ERROR: REFRESHMAPALL command is only available for RaidBot instances";
            }

            if (_main == null)
                return "ERROR: Main form not initialized";

            _main.BeginInvoke((MethodInvoker)(() =>
            {
                var sendAllMethod = _main.GetType().GetMethod("SendAll",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                sendAllMethod?.Invoke(_main, new object[] { BotControlCommand.RefreshMap });
            }));

            return "OK: RefreshMapAll command sent to all RaidBot instances";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to execute RefreshMapAll - {ex.Message}";
        }
    }

    private static string GetBotsList()
    {
        try
        {
            var botList = new List<object>();
            var config = GetConfig();
            var controllers = GetBotControllers();

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
            var botType = DetectBotType();
            var version = GetVersionForBotType(botType);
            var mode = config?.Mode.ToString() ?? "Unknown";
            var name = GetInstanceName(config, mode, botType);

            var info = new
            {
                Version = version,
                Mode = mode,
                Name = name,
                BotType = botType.ToString(),
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

    private static List<BotController> GetBotControllers()
    {
        var flpBotsField = _main!.GetType().GetField("FLP_Bots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (flpBotsField?.GetValue(_main) is FlowLayoutPanel flpBots)
        {
            return [.. flpBots.Controls.OfType<BotController>()];
        }

        return [];
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

    private static string GetVersionForBotType(BotType botType)
    {
        try
        {
            return botType switch
            {
                BotType.PokeBot => GetPokeBotVersion(),
                BotType.RaidBot => GetRaidBotVersion(),
                _ => "Unknown"
            };
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetPokeBotVersion()
    {
        try
        {
            var pokeBotType = Type.GetType("SysBot.Pokemon.Helpers.PokeBot, SysBot.Pokemon");
            if (pokeBotType != null)
            {
                var versionField = pokeBotType.GetField("Version",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (versionField != null)
                {
                    return versionField.GetValue(null)?.ToString() ?? "Unknown";
                }
            }
            return PokeBot.Version;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetRaidBotVersion()
    {
        try
        {
            var raidBotType = Type.GetType("SysBot.Pokemon.SV.BotRaid.Helpers.SVRaidBot, SysBot.Pokemon");
            if (raidBotType != null)
            {
                var versionField = raidBotType.GetField("Version",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (versionField != null)
                {
                    return versionField.GetValue(null)?.ToString() ?? "Unknown";
                }
            }
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetVersion()
    {
        return PokeBot.Version;
    }

    private static string GetInstanceName(ProgramConfig? config, string mode, BotType botType)
    {
        if (!string.IsNullOrEmpty(config?.Hub?.BotName))
            return config.Hub.BotName;

        return botType switch
        {
            BotType.RaidBot => "RaidBot",
            BotType.PokeBot => "PokeBot",
            _ => "PokeBot"
        };
    }

    private static void CreatePortFile()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var exeDir = Path.GetDirectoryName(exePath) ?? Program.WorkingDirectory;
            
            // Detect bot type and create appropriate port file
            var botType = DetectBotType();
            var portFileName = botType switch
            {
                BotType.RaidBot => $"SVRaidBot_{Environment.ProcessId}.port",
                BotType.PokeBot => $"PokeBot_{Environment.ProcessId}.port",
                _ => $"PokeBot_{Environment.ProcessId}.port" // Default to PokeBot
            };
            
            var portFile = Path.Combine(exeDir, portFileName);

            // Write with file lock to prevent race conditions
            using (var fs = new FileStream(portFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs))
            {
                writer.WriteLine(_tcpPort);
            }

            LogUtil.LogInfo($"Created port file: {portFileName} with port {_tcpPort}", "WebServer");
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
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var exeDir = Path.GetDirectoryName(exePath) ?? Program.WorkingDirectory;
            var portFile = Path.Combine(exeDir, $"PokeBot_{Environment.ProcessId}.port");

            if (File.Exists(portFile))
                File.Delete(portFile);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to cleanup port file: {ex.Message}", "WebServer");
        }
    }

    private static int FindAvailablePort(int startPort)
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
        var exeDir = Path.GetDirectoryName(exePath) ?? Program.WorkingDirectory;

        // Use a lock to prevent race conditions
        lock (_portLock)
        {
            for (int port = startPort; port < startPort + 100; port++)
            {
                // Check if port is reserved by another instance
                if (_portReservations.ContainsKey(port))
                    continue;

                if (!IsPortInUse(port))
                {
                    // Check if any port file claims this port
                    var portFiles = Directory.GetFiles(exeDir, "PokeBot_*.port");
                    bool portClaimed = false;

                    foreach (var file in portFiles)
                    {
                        try
                        {
                            // Lock the file before reading to prevent race conditions
                            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (var reader = new StreamReader(fs))
                            {
                                var content = reader.ReadToEnd().Trim();
                                if (content == port.ToString() || content.Contains($"\"Port\":{port}"))
                                {
                                    portClaimed = true;
                                    break;
                                }
                            }
                        }
                        catch { }
                    }

                    if (!portClaimed)
                    {
                        // Double-check the port is still available
                        if (!IsPortInUse(port))
                        {
                            return port;
                        }
                    }
                }
            }
        }
        throw new InvalidOperationException("No available ports found");
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(200) };
            var response = client.GetAsync($"http://localhost:{port}/api/bot/instances").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            try
            {
                using var tcpClient = new TcpClient();
                var result = tcpClient.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200));
                if (success)
                {
                    tcpClient.EndConnect(result);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void StopWebServer(this Main mainForm)
    {
        try
        {
            _monitorCts?.Cancel();
            _cts?.Cancel();
            _tcp?.Stop();
            _server?.Dispose();
            _scheduleTimer?.Dispose();

            // Release the port reservation
            lock (_portLock)
            {
                ReleasePort(_tcpPort);
            }

            CleanupPortFile();
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error stopping web server: {ex.Message}", "WebServer");
        }
    }

    private static void CheckPostRestartStartup(Main mainForm)
    {
        try
        {
            var workingDir = Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory;
            var restartFlagPath = Path.Combine(workingDir, "restart_in_progress.flag");
            var updateFlagPath = Path.Combine(workingDir, "update_in_progress.flag");

            bool isPostRestart = File.Exists(restartFlagPath);
            bool isPostUpdate = File.Exists(updateFlagPath);

            if (!isPostRestart && !isPostUpdate)
                return;

            string operation = isPostRestart ? "restart" : "update";
            LogUtil.LogInfo($"Post-{operation} startup detected. Waiting for all instances to come online...", operation == "restart" ? "RestartManager" : "UpdateManager");

            if (isPostRestart) File.Delete(restartFlagPath);
            if (isPostUpdate) File.Delete(updateFlagPath);

            Task.Run(async () =>
            {
                await Task.Delay(5000);

                var attempts = 0;
                var maxAttempts = 12;

                while (attempts < maxAttempts)
                {
                    try
                    {
                        LogUtil.LogInfo($"Post-{operation} check attempt {attempts + 1}/{maxAttempts}", operation == "restart" ? "RestartManager" : "UpdateManager");

                        mainForm.BeginInvoke((MethodInvoker)(() =>
                        {
                            var sendAllMethod = mainForm.GetType().GetMethod("SendAll",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            sendAllMethod?.Invoke(mainForm, new object[] { BotControlCommand.Start });
                        }));

                        LogUtil.LogInfo("Start All command sent to local bots", operation == "restart" ? "RestartManager" : "UpdateManager");

                        var instances = GetAllRunningInstances(0);
                        if (instances.Count > 0)
                        {
                            LogUtil.LogInfo($"Found {instances.Count} remote instances online. Sending Start All command...", operation == "restart" ? "RestartManager" : "UpdateManager");

                            // Send start commands in parallel
                            var tasks = instances.Select(async instance =>
                            {
                                try
                                {
                                    await Task.Run(() =>
                                    {
                                        var response = BotServer.QueryRemote(instance.Port, "STARTALL");
                                        LogUtil.LogInfo($"Start command sent to port {instance.Port}: {response}", operation == "restart" ? "RestartManager" : "UpdateManager");
                                    });
                                }
                                catch (Exception ex)
                                {
                                    LogUtil.LogError($"Failed to send start command to port {instance.Port}: {ex.Message}", operation == "restart" ? "RestartManager" : "UpdateManager");
                                }
                            });

                            await Task.WhenAll(tasks);
                        }

                        LogUtil.LogInfo($"Post-{operation} Start All commands completed successfully", operation == "restart" ? "RestartManager" : "UpdateManager");
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error during post-{operation} startup attempt {attempts + 1}: {ex.Message}", operation == "restart" ? "RestartManager" : "UpdateManager");
                    }

                    attempts++;
                    if (attempts < maxAttempts)
                        await Task.Delay(5000);
                }
            });
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error checking post-restart/update startup: {ex.Message}", "StartupManager");
        }
    }

    private static List<(int Port, int ProcessId)> GetAllRunningInstances(int currentPort)
    {
        var instances = new List<(int, int)>();

        try
        {
            var processes = Process.GetProcessesByName("PokeBot")
                .Where(p => p.Id != Environment.ProcessId);

            foreach (var process in processes)
            {
                try
                {
                    var exePath = process.MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath))
                        continue;

                    var portFile = Path.Combine(Path.GetDirectoryName(exePath)!, $"PokeBot_{process.Id}.port");
                    if (!File.Exists(portFile))
                        continue;

                    var portText = File.ReadAllText(portFile).Trim();
                    if (!int.TryParse(portText, out var port))
                        continue;

                    if (IsPortInUse(port))
                    {
                        instances.Add((port, process.Id));
                    }
                }
                catch { }
            }
        }
        catch { }

        return instances;
    }



    private static void StartScheduledRestartTimer()
    {
        _scheduleTimer = new System.Threading.Timer(CheckScheduledRestart, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private static void CheckScheduledRestart(object? state)
    {
        try
        {
            // Only master instance should check schedule
            if (_server == null) return;

            var workingDir = Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory;
            var schedulePath = Path.Combine(workingDir, "restart_schedule.json");
            if (!File.Exists(schedulePath))
                return;

            var scheduleJson = File.ReadAllText(schedulePath);
            var schedule = System.Text.Json.JsonSerializer.Deserialize<RestartSchedule>(scheduleJson);

            if (schedule == null || !schedule.Enabled)
                return;

            var now = DateTime.Now;
            var scheduledTime = DateTime.Parse(schedule.Time);
            scheduledTime = new DateTime(now.Year, now.Month, now.Day,
                scheduledTime.Hour, scheduledTime.Minute, 0);

            if (now.Hour == scheduledTime.Hour && now.Minute == scheduledTime.Minute)
            {
                var lastRestartPath = Path.Combine(workingDir, "last_restart.txt");
                if (File.Exists(lastRestartPath))
                {
                    var lastRestart = File.ReadAllText(lastRestartPath);
                    if (lastRestart == now.ToString("yyyy-MM-dd"))
                        return;
                }

                File.WriteAllText(lastRestartPath, now.ToString("yyyy-MM-dd"));

                LogUtil.LogInfo("Scheduled restart triggered", "ScheduledRestart");

                if (_main != null)
                {
                    Task.Run(async () =>
                    {
                        await UpdateManager.RestartAllInstancesAsync(_main, _tcpPort);
                        await Task.Delay(10000);
                        await UpdateManager.ProceedWithRestartsAsync(_main, _tcpPort);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error checking scheduled restart: {ex.Message}", "ScheduledRestart");
        }
    }



    private class RestartSchedule
    {
        public bool Enabled { get; set; }
        public string Time { get; set; } = "00:00";
    }
}
