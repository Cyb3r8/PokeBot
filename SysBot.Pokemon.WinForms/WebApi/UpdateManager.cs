using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SysBot.Base;
using SysBot.Pokemon.Helpers;

namespace SysBot.Pokemon.WinForms.WebApi;

public static class UpdateManager
{
    public static bool IsSystemUpdateInProgress { get; private set; }
    public static bool IsSystemRestartInProgress { get; private set; }

    private static readonly Dictionary<string, UpdateStatus> _activeUpdates = [];

    public class UpdateAllResult
    {
        public int TotalInstances { get; set; }
        public int UpdatesNeeded { get; set; }
        public int UpdatesStarted { get; set; }
        public int UpdatesFailed { get; set; }
        public List<InstanceUpdateResult> InstanceResults { get; set; } = [];
    }

    public class InstanceUpdateResult
    {
        public int Port { get; set; }
        public int ProcessId { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public bool NeedsUpdate { get; set; }
        public bool UpdateStarted { get; set; }
        public string? Error { get; set; }
    }

    public class UpdateStatus
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.Now;
        public string Stage { get; set; } = "initializing";
        public string Message { get; set; } = "Starting update process...";
        public int Progress { get; set; } = 0;
        public bool IsComplete { get; set; }
        public bool Success { get; set; }
        public UpdateAllResult? Result { get; set; }
    }

    public static UpdateStatus? GetUpdateStatus(string updateId)
    {
        return _activeUpdates.TryGetValue(updateId, out var status) ? status : null;
    }

    public static List<UpdateStatus> GetActiveUpdates()
    {
        // Clean up old completed updates (older than 1 hour)
        var cutoffTime = DateTime.Now.AddHours(-1);
        var oldUpdates = _activeUpdates
            .Where(kvp => kvp.Value.IsComplete && kvp.Value.StartTime < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldUpdates)
        {
            _activeUpdates.Remove(key);
        }

        return [.. _activeUpdates.Values];
    }

    public static UpdateStatus StartBackgroundUpdate(Main mainForm, int currentPort)
    {
        var status = new UpdateStatus();
        _activeUpdates[status.Id] = status;

        // Start the ENTIRE update process in a fire-and-forget task
        _ = Task.Run(async () =>
        {
            try
            {
                LogUtil.LogInfo($"Starting fire-and-forget update process with ID: {status.Id}", "UpdateManager");

                // Phase 1: Check for updates
                status.Stage = "checking";
                status.Message = "Checking for updates...";
                status.Progress = 5;

                var (updateAvailable, _, latestVersion) = await UpdateChecker.CheckForUpdatesAsync(false);
                if (!updateAvailable || string.IsNullOrEmpty(latestVersion))
                {
                    status.Stage = "complete";
                    status.Message = "No updates available";
                    status.Progress = 100;
                    status.IsComplete = true;
                    status.Success = true;
                    LogUtil.LogInfo("No updates available", "UpdateManager");
                    return;
                }

                LogUtil.LogInfo($"Update available: current version != {latestVersion}", "UpdateManager");

                // Phase 2: Identify instances needing updates
                status.Stage = "scanning";
                status.Message = "Scanning instances...";
                status.Progress = 10;

                var instances = GetAllInstances(currentPort);
                LogUtil.LogInfo($"Found {instances.Count} total instances", "UpdateManager");

                var instancesNeedingUpdate = instances.Where(i => i.Version != latestVersion).ToList();
                LogUtil.LogInfo($"{instancesNeedingUpdate.Count} instances need updating", "UpdateManager");

                if (instancesNeedingUpdate.Count == 0)
                {
                    status.Stage = "complete";
                    status.Message = "All instances are already up to date";
                    status.Progress = 100;
                    status.IsComplete = true;
                    status.Success = true;
                    return;
                }

                status.Result = new UpdateAllResult
                {
                    TotalInstances = instances.Count,
                    UpdatesNeeded = instancesNeedingUpdate.Count
                };

                IsSystemUpdateInProgress = true;

                // Phase 3: Send idle command to all instances
                status.Stage = "idling";
                status.Message = $"Idling all bots across {instancesNeedingUpdate.Count} instances...";
                status.Progress = 20;

                await IdleAllInstances(mainForm, currentPort, instancesNeedingUpdate);
                // Minimal delay to ensure commands are received
                await Task.Delay(1000);

                // Phase 4: Wait for all bots to idle (with 3 minute timeout)
                status.Stage = "waiting_idle";
                status.Message = "Waiting for all bots to finish current operations...";
                status.Progress = 30;

                var idleTimeout = DateTime.Now.AddMinutes(3);
                var allIdle = false;
                var lastIdleCheckTime = DateTime.Now;

                while (DateTime.Now < idleTimeout && !allIdle)
                {
                    allIdle = await CheckAllBotsIdleAsync(mainForm, currentPort);

                    if (!allIdle)
                    {
                        await Task.Delay(2000);
                        var elapsed = (DateTime.Now - status.StartTime).TotalSeconds;
                        var timeoutProgress = Math.Min(elapsed / 300 * 40, 40); // 300 seconds = 5 minutes
                        status.Progress = (int)(30 + timeoutProgress);

                        // Update message with time remaining
                        var remaining = (int)((300 - elapsed));
                        status.Message = $"Waiting for all bots to idle... ({remaining}s remaining)";

                        // Log every 10 seconds
                        if ((DateTime.Now - lastIdleCheckTime).TotalSeconds >= 10)
                        {
                            LogUtil.LogInfo($"Still waiting for bots to idle. {remaining}s remaining", "UpdateManager");
                            lastIdleCheckTime = DateTime.Now;
                        }
                    }
                }

                if (!allIdle)
                {
                    LogUtil.LogInfo("Timeout reached while waiting for bots to idle. FORCING update.", "UpdateManager");
                    status.Message = "Timeout reached. FORCING update despite active bots...";
                    status.Progress = 65;

                    // Force stop all bots that aren't idle
                    await ForceStopAllBots(mainForm, currentPort, instancesNeedingUpdate);
                    await Task.Delay(1000);
                }
                else
                {
                    LogUtil.LogInfo("All bots are idle. Proceeding with updates.", "UpdateManager");
                }

                // Phase 5: Update all slave instances first (in parallel)
                status.Stage = "updating";
                status.Message = "Updating slave instances...";
                status.Progress = 70;

                var slaveInstances = instancesNeedingUpdate.Where(i => i.ProcessId != Environment.ProcessId).ToList();
                var masterInstance = instancesNeedingUpdate.FirstOrDefault(i => i.ProcessId == Environment.ProcessId);

                LogUtil.LogInfo($"Updating {slaveInstances.Count} slave instances in parallel", "UpdateManager");

                // Update slaves sequentially with delay to avoid file conflicts
                var slaveResults = new List<InstanceUpdateResult>();

                for (int i = 0; i < slaveInstances.Count; i++)
                {
                    var slave = slaveInstances[i];
                    var instanceResult = new InstanceUpdateResult
                    {
                        Port = slave.Port,
                        ProcessId = slave.ProcessId,
                        CurrentVersion = slave.Version,
                        LatestVersion = latestVersion,
                        NeedsUpdate = true
                    };

                    try
                    {
                        LogUtil.LogInfo($"Triggering update for instance on port {slave.Port} ({i + 1}/{slaveInstances.Count})...", "UpdateManager");

                        // Update progress to show which slave is being updated
                        status.Message = $"Updating slave instance {i + 1} of {slaveInstances.Count} (Port: {slave.Port})";
                        status.Progress = 70 + (int)((i + 1) / (float)slaveInstances.Count * 20); // Progress from 70% to 90%

                        var updateResponse = BotServer.QueryRemote(slave.Port, "UPDATE");

                        if (!updateResponse.StartsWith("ERROR"))
                        {
                            instanceResult.UpdateStarted = true;
                            LogUtil.LogInfo($"Update triggered for instance on port {slave.Port}", "UpdateManager");

                            // Add delay between slave updates to avoid file conflicts
                            if (i < slaveInstances.Count - 1) // Don't delay after the last slave
                            {
                                LogUtil.LogInfo($"Waiting 3 seconds before next update to avoid file conflicts...", "UpdateManager");
                                await Task.Delay(3000); // 3 second delay between slaves
                            }
                        }
                        else
                        {
                            instanceResult.Error = $"Failed to start update: {updateResponse}";
                            LogUtil.LogError($"Failed to trigger update for port {slave.Port}: {updateResponse}", "UpdateManager");
                        }
                    }
                    catch (Exception ex)
                    {
                        instanceResult.Error = ex.Message;
                        LogUtil.LogError($"Error updating instance on port {slave.Port}: {ex.Message}", "UpdateManager");
                    }

                    slaveResults.Add(instanceResult);
                }

                status.Result.InstanceResults.AddRange(slaveResults);

                var successfulSlaves = slaveResults.Count(r => r.UpdateStarted);
                status.Result.UpdatesStarted = successfulSlaves;
                status.Result.UpdatesFailed = slaveResults.Count(r => !r.UpdateStarted);

                LogUtil.LogInfo($"Slave update results: {successfulSlaves} started, {status.Result.UpdatesFailed} failed", "UpdateManager");

                // Phase 6: Update master instance regardless of slave failures
                if (masterInstance.ProcessId != 0)
                {
                    status.Stage = "updating_master";
                    status.Message = "Updating master instance...";
                    status.Progress = 90;

                    if (status.Result.UpdatesFailed > 0)
                    {
                        LogUtil.LogInfo($"Proceeding with master update despite {status.Result.UpdatesFailed} slave failures", "UpdateManager");
                    }

                    // Create flag file for post-update startup
                    var updateFlagPath = Path.Combine(
                        Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory,
                        "update_in_progress.flag"
                    );
                    File.WriteAllText(updateFlagPath, DateTime.Now.ToString());

                    var masterResult = new InstanceUpdateResult
                    {
                        Port = currentPort,
                        ProcessId = masterInstance.ProcessId,
                        CurrentVersion = masterInstance.Version,
                        LatestVersion = latestVersion,
                        NeedsUpdate = true
                    };

                    try
                    {
                        mainForm.BeginInvoke((MethodInvoker)(() =>
                        {
                            var updateForm = new UpdateForm(false, latestVersion, true);
                            updateForm.PerformUpdate();
                        }));

                        masterResult.UpdateStarted = true;
                        status.Result.UpdatesStarted++;
                        LogUtil.LogInfo("Master instance update triggered", "UpdateManager");
                    }
                    catch (Exception ex)
                    {
                        masterResult.Error = ex.Message;
                        status.Result.UpdatesFailed++;
                        LogUtil.LogError($"Error updating master instance: {ex.Message}", "UpdateManager");
                    }

                    status.Result.InstanceResults.Add(masterResult);
                }
                else if (slaveInstances.Count > 0)
                {
                    // No master to update, wait a bit for slaves to restart then start all bots
                    LogUtil.LogInfo("No master instance to update. Waiting for slaves to restart...", "UpdateManager");
                    await Task.Delay(10000); // Give slaves time to restart

                    // Verify slaves came back online
                    var onlineCount = 0;
                    foreach (var slave in slaveInstances)
                    {
                        if (IsPortOpen(slave.Port))
                        {
                            onlineCount++;
                        }
                    }

                    LogUtil.LogInfo($"{onlineCount}/{slaveInstances.Count} slaves came back online", "UpdateManager");

                    await StartAllBots(mainForm, currentPort);
                }

                // Phase 7: Complete
                status.Stage = "complete";
                status.Success = status.Result.UpdatesStarted > 0; // Success if at least one update started
                status.Message = status.Success
                    ? $"Update commands sent to {status.Result.UpdatesStarted} instances. They are now updating..."
                    : $"Update failed - no instances were updated";
                status.Progress = 100;
                status.IsComplete = true;

                LogUtil.LogInfo($"Update initiation completed: {status.Message}", "UpdateManager");
            }
            catch (Exception ex)
            {
                status.Stage = "error";
                status.Message = $"Update failed: {ex.Message}";
                status.Progress = 0;
                status.IsComplete = true;
                status.Success = false;
                LogUtil.LogError($"Fire-and-forget update failed: {ex}", "UpdateManager");
            }
            finally
            {
                IsSystemUpdateInProgress = false;
            }
        });

        return status;
    }

    private static Task IdleAllInstances(Main mainForm, int currentPort, List<(int ProcessId, int Port, string Version)> instances)
    {
        // Send idle commands in parallel
        var tasks = instances.Select(async instance =>
        {
            try
            {
                if (instance.ProcessId == Environment.ProcessId)
                {
                    // Idle local bots
                    mainForm.BeginInvoke((MethodInvoker)(() =>
                    {
                        var sendAllMethod = mainForm.GetType().GetMethod("SendAll",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        sendAllMethod?.Invoke(mainForm, [BotControlCommand.Idle]);
                    }));
                }
                else
                {
                    // Idle remote bots
                    await Task.Run(() =>
                    {
                        var idleResponse = BotServer.QueryRemote(instance.Port, "IDLEALL");
                        if (idleResponse.StartsWith("ERROR"))
                        {
                            LogUtil.LogError($"Failed to send idle command to port {instance.Port}: {idleResponse}", "UpdateManager");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error idling instance on port {instance.Port}: {ex.Message}", "UpdateManager");
            }
        });

        return Task.WhenAll(tasks);
    }

    private static Task ForceStopAllBots(Main mainForm, int currentPort, List<(int ProcessId, int Port, string Version)> instances)
    {
        LogUtil.LogInfo("Force stopping all bots due to idle timeout", "UpdateManager");

        // Force stop in parallel
        var tasks = instances.Select(async instance =>
        {
            try
            {
                if (instance.ProcessId == Environment.ProcessId)
                {
                    // Stop local bots
                    mainForm.BeginInvoke((MethodInvoker)(() =>
                    {
                        var sendAllMethod = mainForm.GetType().GetMethod("SendAll",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        sendAllMethod?.Invoke(mainForm, [BotControlCommand.Stop]);
                    }));
                    LogUtil.LogInfo("Force stopped local bots", "UpdateManager");
                }
                else
                {
                    // Stop remote bots
                    await Task.Run(() =>
                    {
                        var stopResponse = BotServer.QueryRemote(instance.Port, "STOPALL");
                        if (!stopResponse.StartsWith("ERROR"))
                        {
                            LogUtil.LogInfo($"Force stopped bots on port {instance.Port}", "UpdateManager");
                        }
                        else
                        {
                            LogUtil.LogError($"Failed to force stop bots on port {instance.Port}: {stopResponse}", "UpdateManager");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error force stopping bots on port {instance.Port}: {ex.Message}", "UpdateManager");
            }
        });

        return Task.WhenAll(tasks);
    }

    private static async Task StartAllBots(Main mainForm, int currentPort)
    {
        // Wait 10 seconds before starting all bots
        await Task.Delay(10000);
        LogUtil.LogInfo("Starting all bots after update...", "UpdateManager");

        // Start local bots
        mainForm.BeginInvoke((MethodInvoker)(() =>
        {
            var sendAllMethod = mainForm.GetType().GetMethod("SendAll",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sendAllMethod?.Invoke(mainForm, [BotControlCommand.Start]);
        }));

        // Start remote bots in parallel
        var remoteInstances = GetAllInstances(currentPort).Where(i => i.ProcessId != Environment.ProcessId);
        var tasks = remoteInstances.Select(async instance =>
        {
            try
            {
                await Task.Run(() =>
                {
                    var response = BotServer.QueryRemote(instance.Port, "STARTALL");
                    LogUtil.LogInfo($"Start command sent to port {instance.Port}: {response}", "UpdateManager");
                });
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Failed to send start command to port {instance.Port}: {ex.Message}", "UpdateManager");
            }
        });

        await Task.WhenAll(tasks);
    }

    private static Task<bool> CheckAllBotsIdleAsync(Main mainForm, int currentPort)
    {
        try
        {
            // Check local bots
            var flpBotsField = mainForm.GetType().GetField("FLP_Bots",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (flpBotsField?.GetValue(mainForm) is FlowLayoutPanel flpBots)
            {
                var controllers = flpBots.Controls.OfType<BotController>().ToList();
                var anyActive = controllers.Any(c =>
                {
                    var state = c.ReadBotState();
                    return state != "IDLE" && state != "STOPPED";
                });
                if (anyActive) return Task.FromResult(false);
            }

            // Check remote instances
            var instances = GetAllInstances(currentPort);
            foreach (var (processId, port, version) in instances)
            {
                if (processId == Environment.ProcessId) continue;

                var botsResponse = BotServer.QueryRemote(port, "LISTBOTS");
                if (botsResponse.StartsWith("{") && botsResponse.Contains("Bots"))
                {
                    try
                    {
                        var botsData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, object>>>>(botsResponse);
                        if (botsData?.ContainsKey("Bots") == true)
                        {
                            var anyActive = botsData["Bots"].Any(b =>
                            {
                                if (b.TryGetValue("Status", out var status))
                                {
                                    var statusStr = status?.ToString()?.ToUpperInvariant() ?? "";
                                    return statusStr != "IDLE" && statusStr != "STOPPED";
                                }
                                return false;
                            });
                            if (anyActive) return Task.FromResult(false);
                        }
                    }
                    catch { }
                }
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static List<(int ProcessId, int Port, string Version)> GetAllInstances(int currentPort)
    {
        var instances = new List<(int, int, string)>
        {
            (Environment.ProcessId, currentPort, PokeBot.Version)
        };

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

                    if (!IsPortOpen(port))
                        continue;

                    var versionResponse = BotServer.QueryRemote(port, "VERSION");
                    var version = versionResponse.StartsWith("ERROR") ? "Unknown" : versionResponse.Trim();

                    instances.Add((process.Id, port, version));
                }
                catch { }
            }
        }
        catch { }

        return instances;
    }

    private static bool IsPortOpen(int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var result = client.BeginConnect("127.0.0.1", port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
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

    // Restart functionality
    public class RestartAllResult
    {
        public int TotalInstances { get; set; }
        public List<RestartInstanceResult> InstanceResults { get; set; } = [];
        public bool Success { get; set; }
        public string? Error { get; set; }
        public bool MasterRestarting { get; set; }
        public string? Message { get; set; }
    }

    public class RestartInstanceResult
    {
        public int Port { get; set; }
        public int ProcessId { get; set; }
        public bool RestartStarted { get; set; }
        public string? Error { get; set; }
    }

    public static async Task<RestartAllResult> RestartAllInstancesAsync(Main mainForm, int currentPort)
    {
        var result = new RestartAllResult();

        // Check if restart already in progress
        var lockFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? "", "restart.lock");
        try
        {
            // Try to create lock file exclusively
            using var fs = new FileStream(lockFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var writer = new StreamWriter(fs);
            writer.WriteLine($"{Environment.ProcessId}:{DateTime.Now}");
            writer.Flush();
        }
        catch (IOException)
        {
            // Lock file exists, restart already in progress
            result.Success = false;
            result.Error = "Restart already in progress by another instance";
            return result;
        }

        IsSystemRestartInProgress = true;

        try
        {
            var instances = GetAllInstances(currentPort);
            result.TotalInstances = instances.Count;

            LogUtil.LogInfo($"Preparing to restart {instances.Count} instances", "RestartManager");
            LogUtil.LogInfo("Idling all bots before restart...", "RestartManager");

            // Send idle commands to all instances
            await IdleAllInstances(mainForm, currentPort, instances);
            await Task.Delay(1000); // Give commands time to process

            // Wait for all bots to actually be idle (with timeout)
            LogUtil.LogInfo("Waiting for all bots to idle...", "RestartManager");
            var idleTimeout = DateTime.Now.AddMinutes(3);
            var allIdle = false;

            while (DateTime.Now < idleTimeout && !allIdle)
            {
                allIdle = await CheckAllBotsIdleAsync(mainForm, currentPort);

                if (!allIdle)
                {
                    await Task.Delay(2000);
                    var timeRemaining = (int)(idleTimeout - DateTime.Now).TotalSeconds;
                    LogUtil.LogInfo($"Still waiting for bots to idle... {timeRemaining}s remaining", "RestartManager");
                }
            }

            if (!allIdle)
            {
                LogUtil.LogInfo("Timeout reached while waiting for bots. FORCING stop on all bots...", "RestartManager");
                await ForceStopAllBots(mainForm, currentPort, instances);
                await Task.Delay(2000); // Give stop commands time to process
            }
            else
            {
                LogUtil.LogInfo("All bots are idle. Ready to proceed with restart.", "RestartManager");
            }

            result.Success = true;
            result.Message = allIdle ? "All bots idled successfully" : "Forced stop after timeout";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
        finally
        {
            IsSystemRestartInProgress = false;
        }
    }

    public static async Task<RestartAllResult> ProceedWithRestartsAsync(Main mainForm, int currentPort)
    {
        var result = new RestartAllResult();
        IsSystemRestartInProgress = true;

        try
        {
            var instances = GetAllInstances(currentPort);
            result.TotalInstances = instances.Count;

            var slaveInstances = instances.Where(i => i.ProcessId != Environment.ProcessId).ToList();
            var masterInstance = instances.FirstOrDefault(i => i.ProcessId == Environment.ProcessId);

            // Restart slaves one by one
            foreach (var instance in slaveInstances)
            {
                var instanceResult = new RestartInstanceResult
                {
                    Port = instance.Port,
                    ProcessId = instance.ProcessId
                };

                try
                {
                    LogUtil.LogInfo($"Sending restart command to instance on port {instance.Port}...", "RestartManager");

                    var restartResponse = BotServer.QueryRemote(instance.Port, "SELFRESTARTALL");
                    if (!restartResponse.StartsWith("ERROR"))
                    {
                        instanceResult.RestartStarted = true;
                        LogUtil.LogInfo($"Instance on port {instance.Port} restart command sent, waiting for termination...", "RestartManager");

                        // Wait for process to actually terminate
                        var terminated = await WaitForProcessTermination(instance.ProcessId, 30);
                        if (!terminated)
                        {
                            LogUtil.LogError($"Instance {instance.ProcessId} did not terminate in time", "RestartManager");
                        }
                        else
                        {
                            LogUtil.LogInfo($"Instance {instance.ProcessId} terminated successfully", "RestartManager");
                        }

                        // Wait a bit for cleanup
                        await Task.Delay(2000);

                        // Wait for instance to come back online
                        var backOnline = await WaitForInstanceOnline(instance.Port, 60);
                        if (backOnline)
                        {
                            LogUtil.LogInfo($"Instance on port {instance.Port} is back online", "RestartManager");
                        }
                    }
                    else
                    {
                        instanceResult.Error = $"Failed to send restart command: {restartResponse}";
                        LogUtil.LogError($"Failed to restart instance on port {instance.Port}: {restartResponse}", "RestartManager");
                    }
                }
                catch (Exception ex)
                {
                    instanceResult.Error = ex.Message;
                    LogUtil.LogError($"Error restarting instance on port {instance.Port}: {ex.Message}", "RestartManager");
                }

                result.InstanceResults.Add(instanceResult);
            }

            if (masterInstance.ProcessId != 0)
            {
                LogUtil.LogInfo("Preparing to restart master instance...", "RestartManager");
                result.MasterRestarting = true;

                var restartFlagPath = Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory,
                    "restart_in_progress.flag"
                );
                File.WriteAllText(restartFlagPath, DateTime.Now.ToString());

                await Task.Delay(2000);

                mainForm.BeginInvoke((MethodInvoker)(() =>
                {
                    Application.Restart();
                }));
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
        finally
        {
            IsSystemRestartInProgress = false;

            // Clean up lock file
            var lockFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? "", "restart.lock");
            try
            {
                if (File.Exists(lockFile))
                    File.Delete(lockFile);
            }
            catch { }
        }
    }

    private static async Task<bool> WaitForProcessTermination(int processId, int timeoutSeconds)
    {
        var endTime = DateTime.Now.AddSeconds(timeoutSeconds);

        while (DateTime.Now < endTime)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return true;
            }
            catch (ArgumentException)
            {
                // Process not found = terminated
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private static async Task<bool> WaitForInstanceOnline(int port, int timeoutSeconds)
    {
        var endTime = DateTime.Now.AddSeconds(timeoutSeconds);

        while (DateTime.Now < endTime)
        {
            if (IsPortOpen(port))
            {
                // Give it a moment to fully initialize
                await Task.Delay(1000);
                return true;
            }

            await Task.Delay(1000);
        }

        return false;
    }
}
