using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using SysBot.Base;

namespace SysBot.Pokemon.WinForms.WebApi;

public class BotServer(Main mainForm, int port = 8080, int tcpPort = 8081) : IDisposable
{
    private HttpListener? _listener;
    private Thread? _listenerThread;
    private readonly int _port = port;
    private readonly int _tcpPort = tcpPort;
    private readonly CancellationTokenSource _cts = new();
    private readonly Main _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
    private volatile bool _running;
    private string? _htmlTemplate;
    
    // Instance cache to prevent expensive re-scanning
    private static readonly object _instanceCacheLock = new();
    private static List<BotInstance>? _cachedInstances = null;
    private static DateTime _lastCacheUpdate = DateTime.MinValue;
    private static readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(1); // Reduced cache for better responsiveness

    private string HtmlTemplate
    {
        get
        {
            if (_htmlTemplate == null)
            {
                _htmlTemplate = LoadEmbeddedResource("BotControlPanel.html");
            }
            return _htmlTemplate;
        }
    }

    private static string LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fullResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(fullResourceName))
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        using var stream = assembly.GetManifestResourceStream(fullResourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Could not load embedded resource '{fullResourceName}'");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Start()
    {
        if (_running) return;

        try
        {
            _listener = new HttpListener();

            try
            {
                _listener.Prefixes.Add($"http://+:{_port}/");
                _listener.Start();
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Start();

                LogUtil.LogError($"Web server requires administrator privileges for network access. Currently limited to localhost only.", "WebServer");
            }

            _running = true;

            _listenerThread = new Thread(Listen)
            {
                IsBackground = true,
                Name = "BotWebServer"
            };
            _listenerThread.Start();
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to start web server: {ex.Message}", "WebServer");
            throw;
        }
    }

    public void Stop()
    {
        if (!_running) return;

        try
        {
            _running = false;
            _cts.Cancel();
            _listener?.Stop();
            _listenerThread?.Join(5000);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error stopping web server: {ex.Message}", "WebServer");
        }
    }

    private void Listen()
    {
        while (_running && _listener != null)
        {
            try
            {
                var asyncResult = _listener.BeginGetContext(null, null);

                while (_running && !asyncResult.AsyncWaitHandle.WaitOne(100))
                {
                }

                if (!_running)
                    break;

                var context = _listener.EndGetContext(asyncResult);

                // Use Task.Run instead of ThreadPool for better async handling
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequest(context);
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error handling request {context.Request.Url}: {ex.Message}", "WebServer");
                        
                        // Try to send error response if possible
                        try
                        {
                            if (!context.Response.OutputStream.CanWrite) return;
                            
                            var errorResponse = CreateErrorResponse($"Request handling failed: {ex.Message}");
                            var errorBuffer = System.Text.Encoding.UTF8.GetBytes(errorResponse);
                            
                            context.Response.StatusCode = 500;
                            context.Response.ContentType = "application/json";
                            context.Response.ContentLength64 = errorBuffer.Length;
                            
                            await context.Response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
                            await context.Response.OutputStream.FlushAsync();
                        }
                        catch { /* Ignore errors when sending error response */ }
                        finally
                        {
                            try { context.Response.Close(); } catch { }
                        }
                    }
                });
            }
            catch (HttpListenerException ex) when (!_running || ex.ErrorCode == 995)
            {
                break;
            }
            catch (ObjectDisposedException) when (!_running)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    LogUtil.LogError($"Error in listener (will retry): {ex.Message}", "WebServer");
                    
                    // Wait before retrying, but check if we should still be running
                    for (int i = 0; i < 10 && _running; i++)
                    {
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        HttpListenerResponse? response = null;
        try
        {
            var request = context.Request;
            response = context.Response;


            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            // Handle query parameters (like ?v=2.0.0) by using only the path
            var requestPath = request.Url?.LocalPath ?? "";
            
            string? responseString = requestPath switch
            {
                "/" => HtmlTemplate,
                "/BotControlPanel.css" => ServeEmbeddedResource("BotControlPanel.css", "text/css"),
                "/BotControlPanel.js" => ServeEmbeddedResource("BotControlPanel.js", "application/javascript"),
                "/api/bot/instances" => GetInstances(),
                "/api/bot/debug" => GetDebugInfo(),
                var path when path?.StartsWith("/api/bot/instances/") == true && path.EndsWith("/bots") =>
                    GetBots(path),
                var path when path?.StartsWith("/api/bot/instances/") == true && path.EndsWith("/command") =>
                    await RunCommand(request, path),
                "/api/bot/command/all" => await RunAllCommand(request),
                "/api/bot/update/check" => await CheckForUpdates(),
                "/api/bot/update/idle-status" => GetIdleStatus(),
                "/api/bot/update/all" => await UpdateAllInstances(request),
                "/api/bot/update/pokebot" => await UpdatePokeBotInstances(request),
                "/api/bot/update/raidbot" => await UpdateRaidBotInstances(request),
                "/api/bot/update/active" => GetActiveUpdates(),
                "/api/bot/restart/all" => await RestartAllInstances(request),
                "/api/bot/restart/proceed" => await ProceedWithRestarts(request),
                "/api/bot/restart/schedule" => await UpdateRestartSchedule(request),
                "/icon.ico" => ServeIcon(),
                var path when path?.EndsWith(".png") == true => ServeImage(path),
                _ => null
            };

            if (responseString == null)
            {
                response.StatusCode = 404;
                responseString = "Not Found";
            }
            else
            {
                response.StatusCode = 200;

                // Set appropriate content type
                if (requestPath == "/")
                {
                    response.ContentType = "text/html; charset=utf-8";
                }
                else if (requestPath == "/BotControlPanel.css")
                {
                    response.ContentType = "text/css; charset=utf-8";
                }
                else if (requestPath == "/BotControlPanel.js")
                {
                    response.ContentType = "application/javascript; charset=utf-8";
                }
                else if (requestPath == "/icon.ico")
                {
                    response.ContentType = "image/x-icon";
                }
                else if (requestPath.EndsWith(".png"))
                {
                    response.ContentType = "image/png";
                }
                else
                {
                    response.ContentType = "application/json; charset=utf-8";
                }
            }

            // Handle binary content for icon and images
            if (requestPath == "/icon.ico" && responseString == "BINARY_ICON")
            {
                var iconBytes = GetIconBytes();
                if (iconBytes != null)
                {
                    response.ContentLength64 = iconBytes.Length;
                    await response.OutputStream.WriteAsync(iconBytes, 0, iconBytes.Length);
                    await response.OutputStream.FlushAsync();
                    return;
                }
            }
            else if (requestPath.EndsWith(".png") && responseString == "BINARY_IMAGE")
            {
                var imageBytes = GetImageBytes(requestPath);
                if (imageBytes != null)
                {
                    response.ContentLength64 = imageBytes.Length;
                    await response.OutputStream.WriteAsync(imageBytes, 0, imageBytes.Length);
                    await response.OutputStream.FlushAsync();
                    return;
                }
            }

            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;

            try
            {
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 64 || ex.ErrorCode == 1229)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error processing request: {ex.Message}", "WebServer");

            if (response != null && response.OutputStream.CanWrite)
            {
                try
                {
                    response.StatusCode = 500;
                }
                catch { }
            }
        }
        finally
        {
            try
            {
                response?.Close();
            }
            catch { }
        }
    }

    private async Task<string> UpdateAllInstances(HttpListenerRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();

            // Check if this is a status check for an existing update
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    var requestData = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                    if (requestData?.ContainsKey("updateId") == true)
                    {
                        // Return status of existing update
                        var status = UpdateManager.GetUpdateStatus(requestData["updateId"]);
                        if (status != null)
                        {
                            return JsonSerializer.Serialize(new
                            {
                                status.Id,
                                status.Stage,
                                status.Message,
                                status.Progress,
                                status.IsComplete,
                                status.Success,
                                StartTime = status.StartTime.ToString("o"),
                                Result = status.Result != null ? new
                                {
                                    status.Result.TotalInstances,
                                    status.Result.UpdatesNeeded,
                                    status.Result.UpdatesStarted,
                                    status.Result.UpdatesFailed
                                } : null
                            });
                        }
                        return CreateErrorResponse("Update not found");
                    }
                }
                catch
                {
                    // Not a status check, proceed with starting new update
                }
            }

            // Start a new fire-and-forget background update
            var updateStatus = UpdateManager.StartBackgroundUpdate(_mainForm, _tcpPort);


            return JsonSerializer.Serialize(new
            {
                updateStatus.Id,
                updateStatus.Stage,
                updateStatus.Message,
                updateStatus.Progress,
                StartTime = updateStatus.StartTime.ToString("o"),
                Success = true,
                Info = "Update process started in background. It will continue even if this connection is closed."
            });
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to start update: {ex.Message}", "WebServer");
            return CreateErrorResponse(ex.Message);
        }
    }

    private async Task<string> UpdatePokeBotInstances(HttpListenerRequest request)
    {
        try
        {
            // Start a new fire-and-forget background update for PokeBot instances only
            var updateStatus = UpdateManager.StartPokeBotUpdate(_mainForm, _tcpPort);


            return JsonSerializer.Serialize(new
            {
                updateStatus.Id,
                updateStatus.Stage,
                updateStatus.Message,
                updateStatus.Progress,
                StartTime = updateStatus.StartTime.ToString("o"),
                Success = true,
                Info = "PokeBot update process started in background. It will continue even if this connection is closed."
            });
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to start PokeBot update: {ex.Message}", "WebServer");
            return CreateErrorResponse(ex.Message);
        }
    }

    private async Task<string> UpdateRaidBotInstances(HttpListenerRequest request)
    {
        try
        {
            // Start a new fire-and-forget background update for RaidBot instances only
            var updateStatus = UpdateManager.StartRaidBotUpdate(_mainForm, _tcpPort);


            return JsonSerializer.Serialize(new
            {
                updateStatus.Id,
                updateStatus.Stage,
                updateStatus.Message,
                updateStatus.Progress,
                StartTime = updateStatus.StartTime.ToString("o"),
                Success = true,
                Info = "RaidBot update process started in background. It will continue even if this connection is closed."
            });
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to start RaidBot update: {ex.Message}", "WebServer");
            return CreateErrorResponse(ex.Message);
        }
    }

    private async Task<string> RestartAllInstances(HttpListenerRequest request)
    {
        try
        {
            var result = await UpdateManager.RestartAllInstancesAsync(_mainForm, _tcpPort);

            return JsonSerializer.Serialize(new
            {
                result.Success,
                result.TotalInstances,
                result.Error,
                Message = result.Success ? "Restart initiated successfully" : "Failed to initiate restart"
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(ex.Message);
        }
    }

    private async Task<string> ProceedWithRestarts(HttpListenerRequest request)
    {
        try
        {
            var result = await UpdateManager.ProceedWithRestartsAsync(_mainForm, _tcpPort);

            return JsonSerializer.Serialize(new
            {
                result.Success,
                result.TotalInstances,
                result.MasterRestarting,
                result.Error,
                Results = result.InstanceResults.Select(r => new
                {
                    r.Port,
                    r.ProcessId,
                    r.RestartStarted,
                    r.Error
                })
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(ex.Message);
        }
    }

    private async Task<string> UpdateRestartSchedule(HttpListenerRequest request)
    {
        try
        {
            if (request.HttpMethod == "GET")
            {
                var workingDir = Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory;
                var schedulePath = Path.Combine(workingDir, "restart_schedule.json");

                if (File.Exists(schedulePath))
                {
                    var scheduleJson = File.ReadAllText(schedulePath);
                    return scheduleJson;
                }

                return JsonSerializer.Serialize(new { Enabled = false, Time = "00:00" });
            }
            else if (request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream);
                var body = await reader.ReadToEndAsync();

                var workingDir = Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory;
                var schedulePath = Path.Combine(workingDir, "restart_schedule.json");

                File.WriteAllText(schedulePath, body);

                return JsonSerializer.Serialize(new { Success = true });
            }

            return CreateErrorResponse("Invalid method");
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(ex.Message);
        }
    }

    private string GetIdleStatus()
    {
        try
        {
            var instances = new List<object>();

            // Get local bots with error handling
            var localBots = new List<BotController>();
            var localIdleCount = 0;
            var localTotalCount = 0;
            var localNonIdleBots = new List<object>();
            
            try
            {
                localBots = GetBotControllers();
                localTotalCount = localBots.Count;
                
                foreach (var controller in localBots)
                {
                    try
                    {
                        var status = controller.ReadBotState();
                        var upperStatus = status?.ToUpper() ?? "";

                        if (upperStatus == "IDLE" || upperStatus == "STOPPED")
                        {
                            localIdleCount++;
                        }
                        else
                        {
                            localNonIdleBots.Add(new
                            {
                                Name = GetBotName(controller.State, GetConfig()),
                                Status = status
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error reading bot controller state: {ex.Message}", "WebServer");
                        // Count as non-idle if we can't read status
                        localNonIdleBots.Add(new
                        {
                            Name = "Unknown",
                            Status = "ERROR"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error getting bot controllers: {ex.Message}", "WebServer");
            }

            instances.Add(new
            {
                Port = _tcpPort,
                ProcessId = Environment.ProcessId,
                TotalBots = localTotalCount,
                IdleBots = localIdleCount,
                NonIdleBots = localNonIdleBots,
                AllIdle = localIdleCount == localTotalCount
            });

            // Get remote instances with error handling
            try
            {
                var remoteInstances = ScanRemoteInstances()?.Where(i => i.IsOnline) ?? Enumerable.Empty<dynamic>();
                foreach (var instance in remoteInstances)
                {
                    try
                    {
                        var botsResponse = QueryRemote(instance.Port, "LISTBOTS");
                        if (botsResponse.StartsWith("{") && botsResponse.Contains("Bots"))
                        {
                            try
                            {
                                var botsData = JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, object>>>>(botsResponse);
                                if (botsData?.ContainsKey("Bots") == true)
                                {
                                    var bots = botsData["Bots"];
                                    var idleCount = 0;
                                    var nonIdleBots = new List<object>();

                                    foreach (var bot in bots)
                                    {
                                        if (bot.TryGetValue("Status", out object? status))
                                        {
                                            var statusStr = status?.ToString()?.ToUpperInvariant() ?? "";
                                            if (statusStr == "IDLE" || statusStr == "STOPPED")
                                            {
                                                idleCount++;
                                            }
                                            else
                                            {
                                                nonIdleBots.Add(new
                                                {
                                                    Name = bot.TryGetValue("Name", out object? name) ? name?.ToString() : "Unknown",
                                                    Status = statusStr
                                                });
                                            }
                                        }
                                    }

                                    instances.Add(new
                                    {
                                        instance.Port,
                                        instance.ProcessId,
                                        TotalBots = bots.Count,
                                        IdleBots = idleCount,
                                        NonIdleBots = nonIdleBots,
                                        AllIdle = idleCount == bots.Count
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogUtil.LogError($"Error parsing remote bots response from port {instance.Port}: {ex.Message}", "WebServer");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error querying remote instance on port {instance.Port}: {ex.Message}", "WebServer");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error scanning remote instances: {ex.Message}", "WebServer");
            }

            // Calculate totals with error handling
            var totalBots = 0;
            var totalIdle = 0;
            var allInstancesIdle = true;
            
            try
            {
                totalBots = instances.Sum(i => (int)((dynamic)i).TotalBots);
                totalIdle = instances.Sum(i => (int)((dynamic)i).IdleBots);
                allInstancesIdle = instances.All(i => (bool)((dynamic)i).AllIdle);
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error calculating bot totals: {ex.Message}", "WebServer");
            }

            return JsonSerializer.Serialize(new
            {
                Instances = instances,
                TotalBots = totalBots,
                TotalIdleBots = totalIdle,
                AllBotsIdle = allInstancesIdle,
                Timestamp = DateTime.Now,
                Success = true
            });
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Critical error in GetIdleStatus: {ex.Message}", "WebServer");
            return CreateErrorResponse($"Failed to get idle status: {ex.Message}");
        }
    }

    private async Task<string> CheckForUpdates()
    {
        try
        {
            var (updateAvailable, _, latestVersion) = await UpdateChecker.CheckForUpdatesAsync(false);
            var changelog = await UpdateChecker.FetchChangelogAsync();

            return JsonSerializer.Serialize(new
            {
                version = latestVersion,
                changelog,
                available = updateAvailable
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                version = "Unknown",
                changelog = "Unable to fetch update information",
                available = false,
                error = ex.Message
            });
        }
    }

    private static int ExtractPort(string path)
    {
        var parts = path.Split('/');
        return parts.Length > 4 && int.TryParse(parts[4], out var port) ? port : 0;
    }

    private static (string ip, int port) ExtractIpAndPort(string path)
    {
        try
        {
            // Erwartete Pfad: /api/bot/instances/{ip}:{port}/command
            // Oder: /api/bot/instances/{ip}:{port}/bots
            var parts = path.Split('/');
            if (parts.Length > 4)
            {
                var ipPortPart = parts[4]; // z.B. "100.x.x.x:8081" oder "8081"
                
                var colonIndex = ipPortPart.LastIndexOf(':');
                
                if (colonIndex > 0)
                {
                    // Format: "IP:Port"
                    var ip = ipPortPart.Substring(0, colonIndex);
                    var portStr = ipPortPart.Substring(colonIndex + 1);
                    
                    if (int.TryParse(portStr, out var port))
                    {
                        return (ip, port);
                    }
                }
                // Fallback: Port-only (für Rückwärtskompatibilität)
                else if (int.TryParse(ipPortPart, out var port))
                {
                    return ("127.0.0.1", port);
                }
            }
            
            return ("127.0.0.1", 0);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"[WebServer] Error parsing path '{path}': {ex.Message}", "WebServer");
            return ("127.0.0.1", 0);
        }
    }

    private bool IsRemoteInstance(string ip, int port)
    {
        // Check if this IP:Port combination is in our known remote instances
        var remoteInstances = ScanRemoteInstances();
        return remoteInstances.Any(instance => 
            instance.IP == ip && instance.Port == port && instance.IsRemote);
    }

    private bool IsLocalInstance(string ip, int port)
    {
        // Eine Instanz ist nur dann lokal, wenn sie EXAKT unsere Master-Instanz ist
        // (gleiche IP UND gleicher Port wie unser TCP-Port)
        
        var isLocalhostIP = ip == "127.0.0.1" || ip == "localhost";
        var isOurPort = port == _tcpPort;
        
        // Nur wenn es sowohl localhost IP als auch unser Port ist, behandeln wir es als lokale Instanz
        return isLocalhostIP && isOurPort;
    }

    private string GetInstances()
    {
        try
        {
            lock (_instanceCacheLock)
            {
                // Check if cache is still valid
                if (_cachedInstances != null && DateTime.Now - _lastCacheUpdate < _cacheTimeout)
                {
                    // Update only the local instance (which is fast) and return cached remote instances
                    var localInstance = CreateLocalInstance();
                    var result = new List<BotInstance> { localInstance };
                    
                    // Add cached instances (both local slaves and remote instances)
                    // Don't exclude based on port alone - instead exclude based on being our own master instance
                    result.AddRange(_cachedInstances.Where(i => !(i.IP == "127.0.0.1" && i.Port == _tcpPort && i.ProcessId == Environment.ProcessId)));
                    
                    return JsonSerializer.Serialize(new { Instances = result });
                }
                
                // Cache expired or doesn't exist - perform full scan
                var instances = new List<BotInstance>
                {
                    CreateLocalInstance()
                };
                
                // Perform expensive remote scan with optimizations
                var remoteInstances = ScanRemoteInstancesFast();
                
                // Filter out any remote instances that match our local master instance
                var filteredRemoteInstances = remoteInstances.Where(r => 
                    !(r.IP == "127.0.0.1" && r.Port == _tcpPort)).ToList();
                    
                instances.AddRange(filteredRemoteInstances);
                
                // Cache the results
                _cachedInstances = new List<BotInstance>(instances);
                _lastCacheUpdate = DateTime.Now;
                
                return JsonSerializer.Serialize(new { Instances = instances });
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in GetInstances: {ex.Message}", "WebServer");
            
            // Fallback to just local instance if there's an error
            var localInstance = CreateLocalInstance();
            return JsonSerializer.Serialize(new { Instances = new[] { localInstance } });
        }
    }

    private string GetDebugInfo()
    {
        try
        {
            var config = GetConfig();
            var controllers = GetBotControllers();
            var localInstance = CreateLocalInstance();
            
            var debugInfo = new
            {
                ConfigExists = config != null,
                ConfigMode = config?.Mode.ToString(),
                ControllerCount = controllers?.Count ?? 0,
                TcpPort = _tcpPort,
                LocalInstance = new
                {
                    localInstance.ProcessId,
                    localInstance.Name,
                    localInstance.Port,
                    localInstance.IP,
                    localInstance.Version,
                    localInstance.Mode,
                    localInstance.BotCount,
                    localInstance.IsOnline,
                    localInstance.BotType
                },
                RawGetInstancesResponse = GetInstances()
            };

            return JsonSerializer.Serialize(debugInfo, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { Error = ex.Message, StackTrace = ex.StackTrace }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private BotInstance CreateLocalInstance()
    {
        try
        {
            var config = GetConfig();
            var controllers = GetBotControllers();

            var mode = config?.Mode.ToString() ?? "Unknown";
            var tailscaleConfig = config?.Hub?.Tailscale;
            
            // Detect bot type
            var botType = "PokeBot"; // Default
            try
            {
                // Try to detect RaidBot
                var raidBotType = Type.GetType("SysBot.Pokemon.SV.BotRaid.Helpers.SVRaidBot, SysBot.Pokemon");
                if (raidBotType != null)
                {
                    botType = "RaidBot";
                }
            }
            catch { }

            var name = GetMainBotName(config);
            
            // Add Master Dashboard indicator to name if this is the master node
            if (tailscaleConfig?.Enabled == true && tailscaleConfig.IsMasterNode)
            {
                name = $"{name} (Master Dashboard)";
            }

            // Reduced logging - only log when instance is created for the first time

            var version = "Unknown";
            try
            {
                if (botType == "RaidBot")
                {
                    // Get RaidBot version
                    var raidBotType = Type.GetType("SysBot.Pokemon.SV.BotRaid.Helpers.SVRaidBot, SysBot.Pokemon");
                    if (raidBotType != null)
                    {
                        var versionField = raidBotType.GetField("Version",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (versionField != null)
                        {
                            version = versionField.GetValue(null)?.ToString() ?? "Unknown";
                        }
                    }
                }
                else
                {
                    // Get PokeBot version - try multiple approaches for better compatibility
                    try
                    {
                        // First try: Direct static access to SysBot.Pokemon.PokeBot.Version
                        version = SysBot.Pokemon.PokeBot.Version;
                    }
                    catch
                    {
                        // Second try: Reflection-based access
                        try
                        {
                            var pokeBotType = Type.GetType("SysBot.Pokemon.Helpers.PokeBot, SysBot.Pokemon");
                            if (pokeBotType != null)
                            {
                                var versionField = pokeBotType.GetField("Version",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                if (versionField != null)
                                {
                                    version = versionField.GetValue(null)?.ToString() ?? "Unknown";
                                }
                            }
                        }
                        catch
                        {
                            // Third try: SVRaidBot PokeBot version if available
                            try
                            {
                                var svRaidBotType = Type.GetType("SysBot.Pokemon.PokeBot, SysBot.Pokemon");
                                if (svRaidBotType != null)
                                {
                                    var versionProperty = svRaidBotType.GetProperty("Version",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                    if (versionProperty != null)
                                    {
                                        version = versionProperty.GetValue(null)?.ToString() ?? "Unknown";
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (version == "Unknown")
                {
                    version = _mainForm.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
                }
            }
            catch
            {
                version = _mainForm.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
            }

            var botStatuses = controllers?.Select(c => 
            {
                try
                {
                    return new BotStatusInfo
                    {
                        Name = GetBotName(c.State, config),
                        Status = c.ReadBotState()
                    };
                }
                catch
                {
                    return new BotStatusInfo { Name = "Unknown", Status = "Error" };
                }
            }).ToList() ?? new List<BotStatusInfo>();

            // Get local IP for proper identification
            var localIP = GetLocalTailscaleIP() ?? "127.0.0.1";

            var instance = new BotInstance
            {
                ProcessId = Environment.ProcessId,
                Name = name ?? "Local Bot",
                Port = _tcpPort,
                IP = localIP,
                Version = version,
                Mode = mode,
                BotCount = botStatuses.Count,
                IsOnline = true,
                IsMaster = tailscaleConfig?.IsMasterNode == true,
                IsRemote = false,
                BotStatuses = botStatuses,
                BotType = botType
            };

            // Only log once on startup, not on every instance creation
            return instance;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error creating local instance: {ex.Message}", "WebServer");
            
            // Return minimal working instance
            return new BotInstance
            {
                ProcessId = Environment.ProcessId,
                Name = "Local Bot (Error)",
                Port = _tcpPort,
                IP = "127.0.0.1",
                Version = "Error",
                Mode = "Error",
                BotCount = 0,
                IsOnline = true,
                IsMaster = false,
                IsRemote = false,
                BotStatuses = new List<BotStatusInfo>(),
                BotType = "PokeBot"
            };
        }
    }

    private List<BotInstance> ScanRemoteInstancesFast()
    {
        var instances = new List<BotInstance>();

        try
        {
            // Get Tailscale configuration
            var config = GetConfig();
            var tailscaleConfig = config?.Hub?.Tailscale;

            // If Tailscale is enabled, scan remote nodes (this is fast network scanning)
            if (tailscaleConfig?.Enabled == true)
            {
                var tasks = tailscaleConfig.RemoteNodes.Select(remoteIP => 
                    Task.Run(() => 
                    {
                        try
                        {
                            return ScanRemoteNodeFast(remoteIP, tailscaleConfig);
                        }
                        catch (Exception ex)
                        {
                            LogUtil.LogError($"Error scanning remote node {remoteIP}: {ex.Message}", "WebServer");
                            return new List<BotInstance>();
                        }
                    })).ToArray();

                // Wait for all remote scans to complete with timeout
                if (Task.WaitAll(tasks, TimeSpan.FromMilliseconds(5000))) // Much longer for Tailscale
                {
                    foreach (var task in tasks)
                    {
                        instances.AddRange(task.Result);
                    }
                }
            }

            // Scan full bot port range for multi-bot support (8080-8100)
            var portsToScan = Enumerable.Range(8080, 21).ToArray(); // 8080 to 8100 inclusive
            var portTasks = new List<Task<BotInstance?>>();
            
            
            foreach (var port in portsToScan)
            {
                // Skip our own master port to prevent duplicate instances
                if (port == _tcpPort) continue;
                
                var currentPort = port;
                var task = Task.Run(() => TryCreateInstanceFromPortAndIPFast("127.0.0.1", currentPort));
                portTasks.Add(task);
            }
            
            // Also scan the configured port range if different
            var portRange = GetLocalPortRange(tailscaleConfig);
            for (int port = portRange.start; port <= portRange.end; port++)
            {
                if (port == _tcpPort) continue; // Skip our own port
                if (portsToScan.Contains(port)) continue; // Skip already scanned ports
                
                var currentPort = port;
                var task = Task.Run(() => TryCreateInstanceFromPortAndIPFast("127.0.0.1", currentPort));
                portTasks.Add(task);
            }
            
            // Wait for port scans with longer timeout for full range
            var portScanCompleted = Task.WaitAll(portTasks.ToArray(), TimeSpan.FromMilliseconds(4000)); // Increased for local stability
            
            var foundInstances = 0;
            foreach (var task in portTasks.Where(t => t.IsCompleted))
            {
                try
                {
                    var instance = task.Result;
                    if (instance != null)
                    {
                        instances.Add(instance);
                        foundInstances++;
                    }
                }
                catch
                {
                    // Ignore individual task failures
                }
            }
            
            
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in fast remote instance scan: {ex.Message}", "WebServer");
        }

        // Remove duplicates based on IP and Port combination
        var uniqueInstances = new Dictionary<string, BotInstance>();
        foreach (var instance in instances)
        {
            var key = $"{instance.IP}:{instance.Port}";
            if (!uniqueInstances.ContainsKey(key) || uniqueInstances[key].ProcessId < instance.ProcessId)
            {
                uniqueInstances[key] = instance;
            }
        }
        instances = uniqueInstances.Values.ToList();

        return instances;
    }

    private List<BotInstance> ScanRemoteNodeFast(string remoteIP, TailscaleSettings tailscaleConfig)
    {
        var instances = new List<BotInstance>();
        var portRange = GetPortRangeForNode(remoteIP, tailscaleConfig);

        for (int port = portRange.start; port <= portRange.end; port++)
        {
            try
            {
                var instance = TryCreateInstanceFromPortAndIPFast(remoteIP, port);
                if (instance != null)
                {
                    instance.IsRemote = true;
                    instance.IP = remoteIP;
                    instances.Add(instance);
                }
            }
            catch
            {
                // Ignore individual port failures
            }
        }

        return instances;
    }

    private static BotInstance? TryCreateInstanceFromPortAndIPFast(string ip, int port)
    {
        try
        {
            // Quick port check with minimal timeout
            if (!IsPortOpenFast(ip, port))
            {
                return null;
            }
            

            // Quick INFO query
            var infoResponse = QueryRemoteFast(ip, port, "INFO");
            
            if (string.IsNullOrEmpty(infoResponse) || 
                infoResponse.StartsWith("Failed") || 
                infoResponse.StartsWith("ERROR") ||
                !infoResponse.Contains("{"))
            {
                return null;
            }

            var instance = new BotInstance
            {
                ProcessId = 0,
                Name = "Unknown Bot",
                Port = port,
                IP = ip,
                Version = "Unknown",
                Mode = "Unknown",
                BotCount = 0,
                IsOnline = true,
                IsRemote = ip != "127.0.0.1",
                BotType = "PokeBot"
            };

            UpdateInstanceInfoFast(instance, ip, port);
            return instance;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPortOpenFast(string ip, int port)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(ip, port, null, null);
            var timeoutMs = ip == "127.0.0.1" ? 600 : 2500; // Much longer timeout for remote instances  
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeoutMs));
            if (success)
            {
                client.EndConnect(result);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string QueryRemoteFast(string ip, int port, string command)
    {
        try
        {
            using var client = new TcpClient();
            
            var result = client.BeginConnect(ip, port, null, null);
            var timeoutMs = ip == "127.0.0.1" ? 1500 : 5000; // Increased timeouts for better connectivity
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeoutMs));
            
            if (!success || !client.Connected)
            {
                return "ERROR: Connection timeout";
            }
            
            client.EndConnect(result);
            var readTimeoutMs = ip == "127.0.0.1" ? 2000 : 5000; // Increased for better reliability
            client.ReceiveTimeout = readTimeoutMs;
            client.SendTimeout = readTimeoutMs;

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            writer.WriteLine(command);
            return reader.ReadLine() ?? "No response";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static void UpdateInstanceInfoFast(BotInstance instance, string ip, int port)
    {
        try
        {
            var infoResponse = QueryRemoteFast(ip, port, "INFO");

            if (infoResponse.StartsWith("{") && !infoResponse.StartsWith("{\"ERROR"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(infoResponse);
                    var root = doc.RootElement;

                    static string? GetStringCaseInsensitiveLocal(JsonElement el, params string[] names)
                    {
                        foreach (var name in names)
                        {
                            if (el.TryGetProperty(name, out var v))
                                return v.GetString();
                        }
                        foreach (var prop in el.EnumerateObject())
                        {
                            foreach (var name in names)
                            {
                                if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                                    return prop.Value.GetString();
                            }
                        }
                        return null;
                    }
                    static int GetIntCaseInsensitiveLocal(JsonElement el, params string[] names)
                    {
                        foreach (var name in names)
                        {
                            if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
                                return i;
                        }
                        foreach (var prop in el.EnumerateObject())
                        {
                            foreach (var name in names)
                            {
                                if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                                {
                                    var v = prop.Value;
                                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
                                        return i;
                                    if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var j))
                                        return j;
                                }
                            }
                        }
                        return 0;
                    }

                    var versionFromJson = GetStringCaseInsensitiveLocal(root, "Version", "version");
                    if (!IsVersionInvalid(versionFromJson))
                    {
                        instance.Version = versionFromJson!;
                    }

                    var modeFromJson = GetStringCaseInsensitiveLocal(root, "Mode", "mode");
                    if (!string.IsNullOrWhiteSpace(modeFromJson))
                    {
                        instance.Mode = modeFromJson;
                    }

                    var nameFromJson = GetStringCaseInsensitiveLocal(root, "Name", "name");
                    if (!string.IsNullOrWhiteSpace(nameFromJson))
                    {
                        instance.Name = nameFromJson;
                    }

                    var botTypeStrJson = GetStringCaseInsensitiveLocal(root, "BotType", "botType", "bottype");
                    if (!string.IsNullOrWhiteSpace(botTypeStrJson))
                    {
                        instance.BotType = botTypeStrJson!;
                    }
                else
                {
                    // Enhanced bot type detection fallback
                    var versionStr = instance.Version.ToLower();
                    var nameStr = instance.Name.ToLower();
                    var modeStr = instance.Mode.ToLower();

                    if (nameStr.Contains("raid") || versionStr.Contains("raid") ||
                        nameStr.Contains("sv") || modeStr.Contains("raid") ||
                        (port >= 8082 && port <= 8089))
                    {
                        instance.BotType = "RaidBot";
                        if (instance.Name == "Unknown Bot")
                            instance.Name = $"SVRaidBot (Port {port})";
                    }
                    else if (port >= 8080 && port <= 8100)
                    {
                        instance.BotType = "PokeBot";
                        if (instance.Name == "Unknown Bot")
                        {
                            instance.Name = port switch
                            {
                                8080 => "PokeBot (Master)",
                                8081 => "PokeBot (Secondary)",
                                _ => $"PokeBot (Port {port})"
                            };
                        }
                    }
                    else
                    {
                        instance.BotType = "PokeBot";
                        if (instance.Name == "Unknown Bot" || instance.Name.Contains("Unknown"))
                            instance.Name = $"PokeBot (Port {port})";
                    }
                }

                    var pid = GetIntCaseInsensitiveLocal(root, "ProcessId", "processId", "processid");
                    if (pid > 0)
                        instance.ProcessId = pid;
                }
                catch (JsonException ex)
                {
                    LogUtil.LogError($"Failed to parse INFO JSON response from {ip}:{port}: {ex.Message}", "WebServer");
                    // Continue with fallback logic below
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Unexpected error parsing INFO response from {ip}:{port}: {ex.Message}", "WebServer");
                }
            }

            // Try to get process ID from port files
            if (ip == "127.0.0.1" && instance.ProcessId == 0)
            {
                instance.ProcessId = GetProcessIdFromPortFiles(port);
            }

            // Quick bot count check - skip if too slow
            try
            {
                var botsResponse = QueryRemoteFast(ip, port, "LISTBOTS");
                if (botsResponse.StartsWith("{") && botsResponse.Contains("Bots") && !botsResponse.StartsWith("{\"ERROR"))
                {
                    try
                    {
                        var botsData = JsonSerializer.Deserialize<Dictionary<string, List<BotInfo>>>(botsResponse);
                        if (botsData?.ContainsKey("Bots") == true)
                        {
                            instance.BotCount = botsData["Bots"].Count;
                            instance.BotStatuses = [.. botsData["Bots"].Select(b => new BotStatusInfo
                            {
                                Name = b.Name ?? "Unknown",
                                Status = b.Status ?? "Unknown"
                            })];
                        }
                    }
                    catch (JsonException ex)
                    {
                        LogUtil.LogError($"Failed to parse LISTBOTS JSON response from {ip}:{port}: {ex.Message}", "WebServer");
                        instance.BotCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Bot list query failed for {ip}:{port}: {ex.Message}", "WebServer");
                instance.BotCount = 0;
            }

            // For remote instances, try to get version from INFO/VERSION commands
            if (ip != "127.0.0.1" && IsVersionInvalid(instance.Version))
            {
                var ver = QueryRemoteFast(ip, port, "VERSION");
                if (!string.IsNullOrWhiteSpace(ver) && !ver.StartsWith("ERROR") && !ver.StartsWith("Failed") && !ver.StartsWith("No response"))
                {
                    instance.Version = ver.Trim();
                }
                else
                {
                    // Additional fallback - try INFO command again with longer timeout
                    var infoRetry = QueryRemoteFast(ip, port, "INFO");
                    if (infoRetry.StartsWith("{"))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(infoRetry);
                            var root = doc.RootElement;
                            var versionFromInfo = GetStringCaseInsensitive(root, "Version", "version", "Ver", "ver");
                            if (!string.IsNullOrWhiteSpace(versionFromInfo))
                            {
                                instance.Version = versionFromInfo;
                            }
                        }
                        catch
                        {
                            // JSON parsing failed, keep existing version or set default
                        }
                    }
                }
                
                // Final fallback for remote instances
                if (IsVersionInvalid(instance.Version))
                {
                    instance.Version = "Unknown";
                }
            }

            if (string.IsNullOrWhiteSpace(instance.BotType))
                instance.BotType = "PokeBot";

            // FINAL STEP: For local instances (127.0.0.1), ensure bot-type specific consistent versioning
            if (ip == "127.0.0.1")
            {
                try
                {
                    if (instance.BotType == "RaidBot")
                    {
                        // RaidBot should keep its own version from reflection logic above
                        // Only override if version is invalid
                        if (IsVersionInvalid(instance.Version))
                        {
                            instance.Version = "Unknown RaidBot"; 
                        }
                    }
                    else
                    {
                        // For PokeBot instances, enforce consistent PokeBot version
                        instance.Version = SysBot.Pokemon.PokeBot.Version;
                    }
                }
                catch
                {
                    // Fallback handling
                    if (instance.BotType != "RaidBot")
                    {
                        instance.Version = "v1.1.14c"; // PokeBot fallback
                    }
                }
            }

            // Local helper method for case-insensitive JSON property access
            static string? GetStringCaseInsensitive(JsonElement el, params string[] names)
            {
                foreach (var name in names)
                {
                    if (el.TryGetProperty(name, out var v))
                        return v.GetString();
                }
                foreach (var prop in el.EnumerateObject())
                {
                    foreach (var name in names)
                    {
                        if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            return prop.Value.GetString();
                    }
                }
                return null;
            }

            // Augment missing fields using process metadata (exe name, file versions)
            TryAugmentFromProcess(instance);
        }
        catch
        {
            // If anything fails, just use default values
        }
    }

    // Helper method to determine if a version string is invalid
    private static bool IsVersionInvalid(string? version)
    {
        return string.IsNullOrWhiteSpace(version) || 
               version.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
               version.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
               version.Equals("0.0.0", StringComparison.OrdinalIgnoreCase) ||
               version.Equals("1.0.0", StringComparison.OrdinalIgnoreCase) ||
               version.Length < 3 ||
               version.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
               version.StartsWith("Failed", StringComparison.OrdinalIgnoreCase);
    }

    private List<BotInstance> ScanRemoteInstances()
    {
        var instances = new List<BotInstance>();
        var currentPid = Environment.ProcessId;

        try
        {
            // Get Tailscale configuration
            var config = GetConfig();
            var tailscaleConfig = config?.Hub?.Tailscale;
            var scanLocalhost = true;

            // If Tailscale is enabled, scan remote nodes
            if (tailscaleConfig?.Enabled == true)
            {
                foreach (var remoteIP in tailscaleConfig.RemoteNodes)
                {
                    try
                    {
                        var remoteInstances = ScanRemoteNode(remoteIP, tailscaleConfig);
                        instances.AddRange(remoteInstances);
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error scanning remote node {remoteIP}: {ex.Message}", "WebServer");
                    }
                }

                // If this is NOT the master node, don't scan localhost for process discovery
                // (still scan ports for local bots)
                if (!tailscaleConfig.IsMasterNode)
                {
                    scanLocalhost = false;
                }
            }

            // Local process discovery (only if enabled)
            if (scanLocalhost)
            {
                // Scan for PokeBot processes (try multiple possible names)
                var pokeBotProcesses = new List<Process>();
                try
                {
                    pokeBotProcesses.AddRange(Process.GetProcessesByName("PokeBot")
                        .Where(p => p.Id != currentPid));
                }
                catch { }
                
                try
                {
                    pokeBotProcesses.AddRange(Process.GetProcessesByName("SysBot.Pokemon.WinForms")
                        .Where(p => p.Id != currentPid));
                }
                catch { }

                foreach (var process in pokeBotProcesses)
                {
                    var instance = TryCreateInstance(process, "PokeBot");
                    if (instance != null)
                        instances.Add(instance);
                }

                // Scan for RaidBot processes (try multiple possible names)
                var raidBotProcesses = new List<Process>();
                try
                {
                    raidBotProcesses.AddRange(Process.GetProcessesByName("SysBot")
                        .Where(p => p.Id != currentPid));
                }
                catch { }
                
                try
                {
                    raidBotProcesses.AddRange(Process.GetProcessesByName("SVRaidBot")
                        .Where(p => p.Id != currentPid));
                }
                catch { }
                
                try
                {
                    raidBotProcesses.AddRange(Process.GetProcessesByName("SysBot.Pokemon.WinForms")
                        .Where(p => p.Id != currentPid));
                }
                catch { }

                foreach (var process in raidBotProcesses)
                {
                    var instance = TryCreateInstance(process, "RaidBot");
                    if (instance != null)
                        instances.Add(instance);
                }
            }

            // Always scan full bot port range first for multi-bot support
            var standardPorts = Enumerable.Range(8080, 21).ToArray(); // 8080 to 8100 inclusive
            foreach (var port in standardPorts)
            {
                if (port == _tcpPort) continue; // Skip our own port

                try
                {
                    var directInstance = TryCreateInstanceFromPortAndIP("127.0.0.1", port);
                    if (directInstance != null && !instances.Any(i => i.Port == port && i.IP == "127.0.0.1"))
                    {
                        instances.Add(directInstance);
                        // Suppress frequent instance discovery logging
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Error scanning standard port {port}: {ex.Message}", "WebServer");
                }
            }
            
            // Also scan the configured port range if different
            var portRange = GetLocalPortRange(tailscaleConfig);
            for (int port = portRange.start; port <= portRange.end; port++)
            {
                if (port == _tcpPort) continue; // Skip our own port
                if (standardPorts.Contains(port)) continue; // Skip already scanned standard ports

                try
                {
                    var directInstance = TryCreateInstanceFromPortAndIP("127.0.0.1", port);
                    if (directInstance != null && !instances.Any(i => i.Port == port && i.IP == "127.0.0.1"))
                    {
                        instances.Add(directInstance);
                    }
                }
                catch
                {
                    // Ignore port scan errors
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error scanning remote instances: {ex.Message}", "WebServer");
        }

        return instances;
    }

    private List<BotInstance> ScanRemoteNode(string remoteIP, TailscaleSettings tailscaleConfig)
    {
        var instances = new List<BotInstance>();
        var portRange = GetPortRangeForNode(remoteIP, tailscaleConfig);

        for (int port = portRange.start; port <= portRange.end; port++)
        {
            try
            {
                var instance = TryCreateInstanceFromPortAndIP(remoteIP, port);
                if (instance != null)
                {
                    instance.IsRemote = true;
                    instance.IP = remoteIP;
                    instances.Add(instance);
                }
            }
            catch
            {
                // Ignore individual port failures
            }
        }

        return instances;
    }

    private (int start, int end) GetPortRangeForNode(string ip, TailscaleSettings tailscaleConfig)
    {
        // Check if this IP has a specific port allocation
        if (tailscaleConfig.PortAllocation.NodeAllocations.TryGetValue(ip, out var allocation))
        {
            return (allocation.Start, allocation.End);
        }

        // Use default range
        return (tailscaleConfig.PortScanStart, tailscaleConfig.PortScanEnd);
    }

    private (int start, int end) GetLocalPortRange(TailscaleSettings? tailscaleConfig)
    {
        if (tailscaleConfig?.Enabled == true)
        {
            // Try to get our local IP and find our allocated range
            var localIP = GetLocalTailscaleIP();
            if (!string.IsNullOrEmpty(localIP))
            {
                return GetPortRangeForNode(localIP, tailscaleConfig);
            }
        }

        // Default fallback
        return (8081, 8110);
    }

    private string? GetLocalTailscaleIP()
    {
        try
        {
            // Try to detect our Tailscale IP
            // This is a simple heuristic - in production you might want to use Tailscale API
            var config = GetConfig();
            var tailscaleConfig = config?.Hub?.Tailscale;
            
            if (tailscaleConfig?.MasterNodeIP == "100.108.242.23")
            {
                return "100.108.242.23"; // We are the master node
            }

            // Could implement more sophisticated detection here
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static BotInstance? TryCreateInstance(Process process, string botType)
    {
        try
        {
            var exePath = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                return null;

            var exeDir = Path.GetDirectoryName(exePath)!;
            var processName = process.ProcessName.ToLowerInvariant();
            
            // Better bot type detection based on process name and executable path
            if (processName.Contains("raid") || processName.Contains("sv") || exePath.Contains("SVRaidBot"))
                botType = "RaidBot";
            else if (processName.Contains("poke") || exePath.Contains("PokeBot"))
                botType = "PokeBot";
            
            // Try to find port file with multiple naming conventions
            var possiblePortFiles = new List<string>();
            
            if (botType == "RaidBot")
            {
                possiblePortFiles.Add(Path.Combine(exeDir, $"SVRaidBot_{process.Id}.port"));
                possiblePortFiles.Add(Path.Combine(exeDir, $"SysBot_{process.Id}.port"));
                possiblePortFiles.Add(Path.Combine(exeDir, $"PokeBot_{process.Id}.port"));
            }
            else
            {
                possiblePortFiles.Add(Path.Combine(exeDir, $"PokeBot_{process.Id}.port"));
                possiblePortFiles.Add(Path.Combine(exeDir, $"SysBot_{process.Id}.port"));
            }
            
            // Find the first existing port file
            var portFile = possiblePortFiles.FirstOrDefault(File.Exists) ?? "";

            if (!File.Exists(portFile))
                return null;

            var portText = File.ReadAllText(portFile).Trim();
            if (portText.StartsWith("ERROR:") || !int.TryParse(portText, out var port))
                return null;

            var isOnline = IsPortOpen(port);
            var instance = new BotInstance
            {
                ProcessId = process.Id,
                Name = botType == "RaidBot" ? "SVRaidBot" : "PokeBot",
                Port = port,
                Version = "Unknown",
                Mode = "Unknown",
                BotCount = 0,
                IsOnline = isOnline,
                BotType = botType
            };

            if (isOnline)
            {
                UpdateInstanceInfo(instance, port);
            }

            return instance;
        }
        catch
        {
            return null;
        }
    }

    private static BotInstance? TryCreateInstanceFromPort(int port)
    {
        return TryCreateInstanceFromPortAndIP("127.0.0.1", port);
    }

    private static BotInstance? TryCreateInstanceFromPortAndIP(string ip, int port)
    {
        try
        {
            // Erst prüfen ob Port offen ist
            var isOnline = IsPortOpen(ip, port);
            if (!isOnline)
                return null;

            // Dann prüfen ob tatsächlich ein Bot-Server läuft
            var infoResponse = QueryRemote(ip, port, "INFO");
            if (string.IsNullOrEmpty(infoResponse) || 
                infoResponse.StartsWith("Failed") || 
                infoResponse.StartsWith("ERROR") ||
                !infoResponse.Contains("{"))
            {
                // Kein gültiger Bot-Server
                return null;
            }

            var instance = new BotInstance
            {
                ProcessId = 0, // Cannot get process ID from port alone
                Name = "Unknown Bot",
                Port = port,
                IP = ip,
                Version = "Unknown",
                Mode = "Unknown",
                BotCount = 0,
                IsOnline = true,
                IsRemote = ip != "127.0.0.1",
                BotType = "PokeBot" // Default to PokeBot instead of Unknown
            };

            UpdateInstanceInfo(instance, ip, port);

            return instance;
        }
        catch
        {
            return null;
        }
    }

    private static void UpdateInstanceInfo(BotInstance instance, int port)
    {
        UpdateInstanceInfo(instance, "127.0.0.1", port);
    }

    private static void UpdateInstanceInfo(BotInstance instance, string ip, int port)
    {
        try
        {
            var infoResponse = QueryRemote(ip, port, "INFO");
            if (infoResponse.StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(infoResponse);
                var root = doc.RootElement;

                static string? GetStringCaseInsensitive(JsonElement el, params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (el.TryGetProperty(name, out var v))
                            return v.GetString();
                    }
                    foreach (var prop in el.EnumerateObject())
                    {
                        foreach (var name in names)
                        {
                            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                                return prop.Value.GetString();
                        }
                    }
                    return null;
                }
                static int GetIntCaseInsensitive(JsonElement el, params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
                            return i;
                    }
                    foreach (var prop in el.EnumerateObject())
                    {
                        foreach (var name in names)
                        {
                            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                var v = prop.Value;
                                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
                                    return i;
                                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var j))
                                    return j;
                            }
                        }
                    }
                    return 0;
                }

                instance.Version = GetStringCaseInsensitive(root, "Version", "version") ?? "Unknown";
                instance.Mode = GetStringCaseInsensitive(root, "Mode", "mode") ?? "Unknown";
                instance.Name = GetStringCaseInsensitive(root, "Name", "name") ?? "Unknown Bot";

                // Try to get BotType from the INFO response
                var botTypeStrJson = GetStringCaseInsensitive(root, "BotType", "botType", "bottype");
                if (!string.IsNullOrWhiteSpace(botTypeStrJson))
                {
                    instance.BotType = botTypeStrJson!;
                }
                else
                {
                    // Enhanced fallback: try to detect from other properties
                    var versionStr = instance.Version.ToLower();
                    var nameStr = instance.Name.ToLower();
                    var modeStr = instance.Mode.ToLower();
                    
                    if (nameStr.Contains("raid") || versionStr.Contains("raid") || 
                        nameStr.Contains("sv") || versionStr.Contains("sv") ||
                        modeStr.Contains("raid"))
                    {
                        instance.BotType = "RaidBot";
                        if (instance.Name == "Unknown Bot" || instance.Name == "SysBot")
                            instance.Name = "SVRaidBot";
                    }
                    else if (nameStr.Contains("poke") || versionStr.Contains("poke") ||
                             modeStr.Contains("poke"))
                    {
                        instance.BotType = "PokeBot";
                        if (instance.Name == "Unknown Bot" || instance.Name == "SysBot")
                            instance.Name = "PokeBot";
                    }
                    else
                    {
                        // Final fallback based on port range (if using standard port allocation)
                        if (port >= 8090 && port <= 8099)
                            instance.BotType = "RaidBot";
                        else
                            instance.BotType = "PokeBot"; // Default
                    }
                }

                var pid = GetIntCaseInsensitive(root, "ProcessId", "processId", "processid");
                if (pid > 0)
                    instance.ProcessId = pid;
            }

            var botsResponse = QueryRemote(ip, port, "LISTBOTS");
            if (botsResponse.StartsWith("{") && botsResponse.Contains("Bots"))
            {
                var botsData = JsonSerializer.Deserialize<Dictionary<string, List<BotInfo>>>(botsResponse);
                if (botsData?.ContainsKey("Bots") == true)
                {
                    instance.BotCount = botsData["Bots"].Count;
                    instance.BotStatuses = [.. botsData["Bots"].Select(b => new BotStatusInfo
                    {
                        Name = b.Name,
                        Status = b.Status
                    })];
                }
            }

            // Fallbacks if INFO wasn't available
            if (string.IsNullOrWhiteSpace(instance.Version) || instance.Version.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                var ver = QueryRemote(ip, port, "VERSION");
                if (!string.IsNullOrWhiteSpace(ver) && !ver.StartsWith("ERROR") && !ver.StartsWith("Failed"))
                    instance.Version = ver.Trim();
            }

            if (string.IsNullOrWhiteSpace(instance.BotType))
                instance.BotType = "PokeBot";

            // Augment missing fields using process metadata
            TryAugmentFromProcess(instance);
        }
        catch { }
    }

    private static void TryAugmentFromProcess(BotInstance instance)
    {
        try
        {
            if (instance.ProcessId <= 0)
                return;

            var proc = Process.GetProcessById(instance.ProcessId);
            var exePath = proc.MainModule?.FileName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var fileName = Path.GetFileName(exePath).ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(instance.BotType) || instance.BotType.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    if (fileName.Contains("svraid") || exePath.Contains("SVRaidBot", StringComparison.OrdinalIgnoreCase))
                        instance.BotType = "RaidBot";
                    else if (fileName.Contains("pokebot") || fileName.Contains("sysbot.pokemon.winforms"))
                        instance.BotType = "PokeBot";
                }

                if (string.IsNullOrWhiteSpace(instance.Version) || instance.Version.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    var dir = Path.GetDirectoryName(exePath)!;
                    var coreDll = Path.Combine(dir, "SysBot.Pokemon.dll");
                    string? ver = null;
                    if (File.Exists(coreDll))
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(coreDll);
                        ver = fvi.ProductVersion ?? fvi.FileVersion;
                    }
                    else
                    {
                        var fviExe = FileVersionInfo.GetVersionInfo(exePath);
                        ver = fviExe.ProductVersion ?? fviExe.FileVersion;
                    }
                    if (!string.IsNullOrWhiteSpace(ver))
                        instance.Version = ver!;
                }
            }
        }
        catch
        {
            // ignore process inspection errors
        }
    }

    private static bool IsPortOpen(int port)
    {
        return IsPortOpen("127.0.0.1", port);
    }

    private static bool IsPortOpen(string ip, int port)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(ip, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200)); // Reduziert von 1s auf 200ms
            if (success)
            {
                client.EndConnect(result);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private string GetBots(string path)
    {
        var (ip, port) = ExtractIpAndPort(path);
        var isLocalInstance = IsLocalInstance(ip, port);
        
        if (isLocalInstance)
        {
            var config = GetConfig();
            var controllers = GetBotControllers();

            var bots = controllers.Select(c => new BotInfo
            {
                Id = $"{c.State.Connection.IP}:{c.State.Connection.Port}",
                Name = GetBotName(c.State, config),
                RoutineType = c.State.InitialRoutine.ToString(),
                Status = c.ReadBotState(),
                ConnectionType = c.State.Connection.Protocol.ToString(),
                IP = c.State.Connection.IP,
                Port = c.State.Connection.Port
            }).ToList();

            return JsonSerializer.Serialize(new { Bots = bots });
        }
        else
        {
            return QueryRemote(ip, port, "LISTBOTS");
        }
    }

    private async Task<string> RunCommand(HttpListenerRequest request, string path)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            
            var commandRequest = JsonSerializer.Deserialize<BotCommandRequest>(body, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (commandRequest == null)
                return CreateErrorResponse("Invalid command request");

            if (string.IsNullOrWhiteSpace(commandRequest.Command))
                return CreateErrorResponse("Empty command received");

            var (ip, port) = ExtractIpAndPort(path);
            
            // Entscheidung: Ist es eine lokale oder Remote-Instanz?
            var isLocalInstance = IsLocalInstance(ip, port);
            
            if (isLocalInstance)
            {
                return RunLocalCommand(commandRequest.Command);
            }
            else
            {
                // Remote Instanz: ältere/kompatible Bots (z.B. SVRaidBot) erwarten *ALL*-Suffix
                var commandUpper = commandRequest.Command.ToUpper();
                var tcpCommand = commandUpper.EndsWith("ALL") ? commandUpper : $"{commandUpper}ALL";
                var result = QueryRemote(ip, port, tcpCommand);
                
                var success = !result.StartsWith("ERROR") && !result.StartsWith("Failed");

                return JsonSerializer.Serialize(new CommandResponse
                {
                    Success = success,
                    Message = result,
                    Port = port,
                    Command = commandRequest.Command,
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"[Command] Error: {ex.Message}", "WebServer");
            return CreateErrorResponse(ex.Message);
        }
    }

    private async Task<string> RunAllCommand(HttpListenerRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            
            var commandRequest = JsonSerializer.Deserialize<BotCommandRequest>(body, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (commandRequest == null)
                return CreateErrorResponse("Invalid command request");
                            
            if (string.IsNullOrWhiteSpace(commandRequest.Command))
                return CreateErrorResponse("Command cannot be empty");

            var results = new List<CommandResponse>();

            var localResult = JsonSerializer.Deserialize<CommandResponse>(RunLocalCommand(commandRequest.Command));
            if (localResult != null)
            {
                localResult.InstanceName = _mainForm.Text;
                results.Add(localResult);
            }

            // Use cached instances from GetInstances for better performance
            var instancesJson = GetInstances();
            var instancesDoc = JsonDocument.Parse(instancesJson);
            var remoteInstances = new List<BotInstance>();
                        
            if (instancesDoc.RootElement.TryGetProperty("Instances", out var instancesArray))
            {
                foreach (var instanceElement in instancesArray.EnumerateArray())
                {
                    try
                    {
                        var instance = JsonSerializer.Deserialize<BotInstance>(instanceElement.GetRawText());
                        if (instance != null)
                        {                            
                            if (instance.IsOnline && instance.Port != _tcpPort)
                            {
                                remoteInstances.Add(instance);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"[ALL Command] Error parsing instance: {ex.Message}", "WebServer");
                    }
                }
            }
            else
            {
                LogUtil.LogError("[ALL Command] No 'Instances' property found in response", "WebServer");
            }
            
            foreach (var instance in remoteInstances)
            {
                try
                {
                    var targetIP = string.IsNullOrWhiteSpace(instance.IP) ? "127.0.0.1" : instance.IP;
                    // Convert STARTALL -> start, STOPALL -> stop, etc. for individual instances
                    var baseCommand = commandRequest.Command.ToUpper().Replace("ALL", "").ToLower();
                    var tcpCommand = baseCommand;
                    
                    var result = QueryRemote(targetIP, instance.Port, tcpCommand);
                    
                    results.Add(new CommandResponse
                    {
                        Success = !result.StartsWith("ERROR"),
                        Message = result,
                        Port = instance.Port,
                        Command = commandRequest.Command,
                        InstanceName = instance.Name
                    });
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"[ALL Command] Error sending to {instance.IP}:{instance.Port}: {ex.Message}", "WebServer");
                    results.Add(new CommandResponse
                    {
                        Success = false,
                        Message = $"ERROR: {ex.Message}",
                        Port = instance.Port,
                        Command = commandRequest.Command,
                        InstanceName = instance.Name
                    });
                }
            }

            return JsonSerializer.Serialize(new BatchCommandResponse
            {
                Results = results,
                TotalInstances = results.Count,
                SuccessfulCommands = results.Count(r => r.Success)
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(ex.Message);
        }
    }

    private string RunLocalCommand(string command)
    {
        try
        {
            var cmd = MapCommand(command);

            _mainForm.BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
            {
                var sendAllMethod = _mainForm.GetType().GetMethod("SendAll",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                sendAllMethod?.Invoke(_mainForm, new object[] { cmd });
            }));

            return JsonSerializer.Serialize(new CommandResponse
            {
                Success = true,
                Message = $"Command {command} sent successfully",
                Port = _tcpPort,
                Command = command,
                Timestamp = DateTime.Now
            });
        }
        catch
        {
            return JsonSerializer.Serialize(new CommandResponse
            {
                Success = true,
                Message = $"Command {command} sent successfully",
                Port = _tcpPort,
                Command = command,
                Timestamp = DateTime.Now
            });
        }
    }

    private static BotControlCommand MapCommand(string webCommand)
    {
        return webCommand.ToLower() switch
        {
            "start" => BotControlCommand.Start,
            "stop" => BotControlCommand.Stop,
            "idle" => BotControlCommand.Idle,
            "resume" => BotControlCommand.Resume,
            "restart" => BotControlCommand.Restart,
            "reboot" => BotControlCommand.RebootAndStop,
            "screenon" => BotControlCommand.ScreenOnAll,
            "screenoff" => BotControlCommand.ScreenOffAll,
            "refreshmap" => BotControlCommand.RefreshMap,
            "update" => BotControlCommand.Restart, // Update als Restart behandeln
            _ => BotControlCommand.Start // Fallback zu Start statt None
        };
    }

    public static string QueryRemote(int port, string command)
    {
        return QueryRemote("127.0.0.1", port, command);
    }

    public static string QueryRemote(string ip, int port, string command)
    {
        try
        {
            using var client = new TcpClient();
            
            // Optimierter Timeout-basierter Connect
            var result = client.BeginConnect(ip, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500)); // 500ms statt default
            
            if (!success || !client.Connected)
            {
                return "ERROR: Connection timeout";
            }
            
            client.EndConnect(result);
            
            // Reduzierte Socket-Timeouts
            client.ReceiveTimeout = 1000;  // 1s statt default
            client.SendTimeout = 1000;     // 1s statt default

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            writer.WriteLine(command);
            return reader.ReadLine() ?? "No response";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private List<BotController> GetBotControllers()
    {
        var flpBotsField = _mainForm.GetType().GetField("FLP_Bots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (flpBotsField?.GetValue(_mainForm) is FlowLayoutPanel flpBots)
        {
            return [.. flpBots.Controls.OfType<BotController>()];
        }

        return [];
    }

    private ProgramConfig? GetConfig()
    {
        var configProp = _mainForm.GetType().GetProperty("Config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return configProp?.GetValue(_mainForm) as ProgramConfig;
    }

    private static string GetBotName(PokeBotState state, ProgramConfig? config)
    {
        // Try to get a meaningful bot name instead of just IP
        
        // Option 1: Use Hub BotName if available
        if (!string.IsNullOrEmpty(config?.Hub?.BotName))
        {
            // If multiple bots, differentiate by IP suffix
            var ipSuffix = state.Connection.IP.Split('.').LastOrDefault();
            return $"{config.Hub.BotName}-{ipSuffix}";
        }
        
        // Option 2: Use routine type + IP suffix
        var routineName = state.InitialRoutine.ToString();
        var suffix = state.Connection.IP.Split('.').LastOrDefault() ?? state.Connection.Port.ToString();
        
        // Simplify routine names
        var simplifiedRoutine = routineName switch
        {
            var r when r.Contains("Trade") => "Trade",
            var r when r.Contains("Dump") => "Dump", 
            var r when r.Contains("Clone") => "Clone",
            var r when r.Contains("Encounter") => "Encounter",
            var r when r.Contains("Egg") => "Egg",
            var r when r.Contains("Fossil") => "Fossil",
            _ => "Bot"
        };
        
        return $"PokeBot-{simplifiedRoutine}-{suffix}";
    }
    
    private static string GetMainBotName(ProgramConfig? config)
    {
        // Get name for the main bot instance (not individual bots)
        if (!string.IsNullOrEmpty(config?.Hub?.BotName))
        {
            return config.Hub.BotName;
        }
        
        return "PokeBot";
    }

    private static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(new CommandResponse
        {
            Success = false,
            Message = $"Error: {message}"
        });
    }

    private string GetActiveUpdates()
    {
        try
        {
            var activeUpdates = UpdateManager.GetActiveUpdates()
                .Select(u => new
                {
                    u.Id,
                    u.Stage,
                    u.Message,
                    u.Progress,
                    u.IsComplete,
                    u.Success,
                    StartTime = u.StartTime.ToString("o")
                })
                .ToList();

            return JsonSerializer.Serialize(activeUpdates);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(ex.Message);
        }
    }

    private string ServeEmbeddedResource(string resourceName, string contentType)
    {
        try
        {
            // Try file system first (for flexibility in development/deployment)
            var exePath = System.Windows.Forms.Application.ExecutablePath;
            var exeDir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
            var resourcePath = Path.Combine(exeDir, "Resources", resourceName);
            
            if (File.Exists(resourcePath))
            {
                return File.ReadAllText(resourcePath);
            }
            
            // Fallback to embedded resource
            return LoadEmbeddedResource(resourceName);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to load resource '{resourceName}': {ex.Message}", "WebServer");
            return $"/* Error loading {resourceName}: {ex.Message} */";
        }
    }

    private string ServeIcon()
    {
        return "BINARY_ICON"; // Special marker for binary content
    }
    
    private string ServeImage(string path)
    {
        return "BINARY_IMAGE"; // Special marker for binary content
    }

    private byte[]? GetIconBytes()
    {
        try
        {
            // First try to find icon.ico in the executable directory
            var exePath = Application.ExecutablePath;
            var exeDir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
            var iconPath = Path.Combine(exeDir, "icon.ico");

            if (File.Exists(iconPath))
            {
                return File.ReadAllBytes(iconPath);
            }

            // If not found, try to extract from embedded resources
            var assembly = Assembly.GetExecutingAssembly();
            var iconStream = assembly.GetManifestResourceStream("SysBot.Pokemon.WinForms.icon.ico");

            if (iconStream != null)
            {
                using (iconStream)
                {
                    var buffer = new byte[iconStream.Length];
                    iconStream.ReadExactly(buffer);
                    return buffer;
                }
            }

            // Try to get the application icon as a fallback
            var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon != null)
            {
                using (var ms = new MemoryStream())
                {
                    icon.Save(ms);
                    return ms.ToArray();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to load icon: {ex.Message}", "WebServer");
            return null;
        }
    }
    
    private byte[]? GetImageBytes(string imagePath)
    {
        try
        {
            // Extract filename from path (e.g., "/update_pokebot.png" -> "update_pokebot.png")
            var fileName = Path.GetFileName(imagePath);
            
            // First try to find image in the executable directory
            var exePath = Application.ExecutablePath;
            var exeDir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
            var resourcesDir = Path.Combine(exeDir, "Resources");
            var imagePath1 = Path.Combine(resourcesDir, fileName);
            var imagePath2 = Path.Combine(exeDir, fileName);
            
            if (File.Exists(imagePath1))
            {
                return File.ReadAllBytes(imagePath1);
            }
            
            if (File.Exists(imagePath2))
            {
                return File.ReadAllBytes(imagePath2);
            }
            
            // Try to extract from embedded resources
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            
            if (!string.IsNullOrEmpty(resourceName))
            {
                using var imageStream = assembly.GetManifestResourceStream(resourceName);
                if (imageStream != null)
                {
                    using var memoryStream = new MemoryStream();
                    imageStream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to load image {imagePath}: {ex.Message}", "WebServer");
            return null;
        }
    }

    private static int GetProcessIdFromPortFiles(int port)
    {
        try
        {
            // Look for port files in common locations
            var searchDirectories = new[]
            {
                Directory.GetCurrentDirectory(),
                @"F:\PokeBot\SysBot.Pokemon.WinForms\bin\Debug\net9.0-windows\win-x86",
                @"F:\PokeBot\SVRaidBot\SysBot.Pokemon.WinForms\bin\Debug\net9.0-windows\win-x64",
                Path.Combine(AppContext.BaseDirectory)
            };

            foreach (var directory in searchDirectories)
            {
                if (!Directory.Exists(directory)) continue;

                var portFiles = Directory.GetFiles(directory, "*.port", SearchOption.TopDirectoryOnly);
                foreach (var file in portFiles)
                {
                    try
                    {
                        // Quick filename check first to avoid unnecessary file reads
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        if (fileName.Contains("_"))
                        {
                            var content = File.ReadAllText(file).Trim();
                            if (int.TryParse(content, out var filePort) && filePort == port)
                            {
                                // Extract process ID from filename (e.g., "SVRaidBot_23928.port" -> 23928)
                                var lastUnderscore = fileName.LastIndexOf('_');
                                if (lastUnderscore > 0 && lastUnderscore < fileName.Length - 1)
                                {
                                    var processIdStr = fileName.Substring(lastUnderscore + 1);
                                    if (int.TryParse(processIdStr, out var processId))
                                    {
                                        // Suppress verbose process ID logging
                                        return processId;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error reading port file {file}: {ex.Message}", "WebServer");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error searching for port files for port {port}: {ex.Message}", "WebServer");
            return 0;
        }
    }

    public void Dispose()
    {
        Stop();
        _listener?.Close();
        _cts?.Dispose();
    }
}

public class BotInstance
{
    public int ProcessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Port { get; set; }
    public string IP { get; set; } = "127.0.0.1";
    public string Version { get; set; } = string.Empty;
    public int BotCount { get; set; }
    public string Mode { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsMaster { get; set; }
    public bool IsRemote { get; set; }
    public bool IsLocal => !IsRemote;
    public List<BotStatusInfo>? BotStatuses { get; set; }
    public string BotType { get; set; } = "PokeBot";
}

public class BotStatusInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class BotInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoutineType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public int Port { get; set; }
}

public class BotCommandRequest
{
    public string Command { get; set; } = string.Empty;
    public string? BotId { get; set; }
}

public class CommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Command { get; set; } = string.Empty;
    public string? InstanceName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class BatchCommandResponse
{
    public List<CommandResponse> Results { get; set; } = [];
    public int TotalInstances { get; set; }
    public int SuccessfulCommands { get; set; }
}
