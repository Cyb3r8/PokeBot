using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using SysBot.Base;
using SysBot.Pokemon.Helpers;

namespace SysBot.Pokemon.WinForms.WebApi;

public static class UpdateManager
{
    public static bool IsSystemUpdateInProgress { get; private set; }
    public static bool IsSystemRestartInProgress { get; private set; }

    private static readonly ConcurrentDictionary<string, UpdateStatus> _activeUpdates = new();
    
    // Security constants
    private const long MAX_DOWNLOAD_SIZE = 500 * 1024 * 1024; // 500MB
    private const int MAX_CONCURRENT_UPDATES = 3;
    private const int COMMAND_TIMEOUT_MS = 30000; // 30 seconds
    
    // Input validation
    private static readonly Regex ValidBotTypeRegex = new(@"^[A-Za-z0-9]+$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedBotTypes = new() { "PokeBot", "RaidBot" };
    
    // Security helper methods
    private static bool ValidateBotType(string botType)
    {
        return !string.IsNullOrWhiteSpace(botType) && 
               ValidBotTypeRegex.IsMatch(botType) && 
               AllowedBotTypes.Contains(botType);
    }
    
    private static string SanitizeForFilename(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Unknown";
            
        var sanitized = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
                sanitized.Append(c);
        }
        return sanitized.Length > 0 ? sanitized.ToString() : "Unknown";
    }
    
    private static bool ValidateFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
            
        try
        {
            var fullPath = Path.GetFullPath(path);
            return !path.Contains("..") && 
                   !path.Contains(":") || path.Length > 3; // Allow drive letters
        }
        catch
        {
            return false;
        }
    }
    
    private static bool IsPortValid(int port)
    {
        return port > 0 && port <= 65535;
    }
    
    // Helper methods for staged update process
    private static bool SendLocalIdleCommand(Main mainForm)
    {
        try
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
            {
                var sendAllMethod = mainForm.GetType().GetMethod("SendAll",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                sendAllMethod?.Invoke(mainForm, new object[] { BotControlCommand.Idle });
            }));
            return true;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to send local idle command: {ex.Message}", "UpdateManager");
            return false;
        }
    }
    
    private static string GetExecutablePathForInstance(int processId, int port)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private static async Task<bool> StopBotInstance(int port, int processId, bool isLocalInstance, Main mainForm)
    {
        try
        {
            LogUtil.LogInfo($"Stopping bot instance on port {port}", "UpdateManager");
            
            if (isLocalInstance)
            {
                // For local instance, we'll shut down after all remote instances are updated
                return true;
            }
            else
            {
                // For remote instances, send stop command and wait for process to end
                var response = QueryRemote(port, "STOP");
                if (response.StartsWith("ERROR"))
                {
                    LogUtil.LogError($"Failed to send stop command to port {port}: {response}", "UpdateManager");
                    return false;
                }
                
                // Wait for process to actually stop
                await Task.Delay(3000);
                
                // Verify process is stopped
                try
                {
                    var process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        LogUtil.LogInfo($"Force killing process {processId}", "UpdateManager");
                        process.Kill();
                        await Task.Delay(2000);
                    }
                }
                catch
                {
                    // Process is already dead, which is what we want
                }
                
                return true;
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error stopping bot instance on port {port}: {ex.Message}", "UpdateManager");
            return false;
        }
    }
    
    private static async Task<bool> PerformBotUpdate(string botType, string executablePath)
    {
        try
        {
            LogUtil.LogInfo($"Downloading and installing {botType} update", "UpdateManager");
            
            // Get download URL
            string downloadUrl;
            if (botType == "PokeBot")
            {
                downloadUrl = await UpdateChecker.FetchDownloadUrlAsync();
            }
            else if (botType == "RaidBot")
            {
                downloadUrl = await RaidBotUpdateChecker.FetchDownloadUrlAsync();
            }
            else
            {
                LogUtil.LogError($"Unknown bot type: {botType}", "UpdateManager");
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                LogUtil.LogError($"Failed to get download URL for {botType}", "UpdateManager");
                return false;
            }
            
            // Download update
            var tempPath = await DownloadUpdateAsync(downloadUrl, botType);
            if (string.IsNullOrEmpty(tempPath))
            {
                LogUtil.LogError($"Failed to download {botType} update", "UpdateManager");
                return false;
            }
            
            // Install update by replacing executable
            var executableDir = Path.GetDirectoryName(executablePath);
            var backupPath = Path.Combine(executableDir, Path.GetFileName(executablePath) + ".backup");
            
            // Create backup
            if (File.Exists(executablePath))
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(executablePath, backupPath);
            }
            
            // Install new version
            File.Move(tempPath, executablePath);
            
            LogUtil.LogInfo($"Successfully installed {botType} update", "UpdateManager");
            return true;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error performing bot update: {ex.Message}", "UpdateManager");
            return false;
        }
    }
    
    private static async Task<bool> RestartBotInstance(string executablePath, int expectedPort)
    {
        try
        {
            LogUtil.LogInfo($"Restarting bot instance: {executablePath}", "UpdateManager");
            
            var workingDirectory = Path.GetDirectoryName(executablePath);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
            
            var process = Process.Start(startInfo);
            if (process == null)
            {
                LogUtil.LogError($"Failed to start process: {executablePath}", "UpdateManager");
                return false;
            }
            
            // Wait for bot to start and create port file
            await Task.Delay(5000);
            
            // Verify it's running by checking if port is responding
            for (int i = 0; i < 10; i++)
            {
                if (IsPortOpen(expectedPort))
                {
                    LogUtil.LogInfo($"Bot successfully restarted on port {expectedPort}", "UpdateManager");
                    return true;
                }
                await Task.Delay(1000);
            }
            
            LogUtil.LogError($"Bot started but port {expectedPort} is not responding", "UpdateManager");
            return false;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error restarting bot instance: {ex.Message}", "UpdateManager");
            return false;
        }
    }
    
    private static bool IsPortOpen(int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var result = client.BeginConnect("127.0.0.1", port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1000));
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
        public string BotType { get; set; } = string.Empty;
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
        if (string.IsNullOrWhiteSpace(updateId) || updateId.Length > 100)
            return null;
            
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
            _activeUpdates.TryRemove(key, out _);
        }

        return _activeUpdates.Values.ToList();
    }
    
    private static List<(int ProcessId, int Port, string Version, string BotType)> GetAllInstances(int currentPort)
    {
        var instances = new List<(int, int, string, string)>();
        
        // Add current instance
        var currentBotType = DetectCurrentBotType();
        var currentVersion = GetVersionForCurrentBotType(currentBotType);
        
        instances.Add((Environment.ProcessId, currentPort, currentVersion, currentBotType));

        try
        {
            // Scan for other bot processes
            var processes = new List<Process>();
            
            // Look for PokeBot processes
            try
            {
                processes.AddRange(Process.GetProcessesByName("PokeBot")
                    .Where(p => p.Id != Environment.ProcessId));
                processes.AddRange(Process.GetProcessesByName("SysBot.Pokemon.WinForms")
                    .Where(p => p.Id != Environment.ProcessId));
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error scanning PokeBot processes: {ex.Message}", "UpdateManager");
            }
            
            // Look for RaidBot/SysBot processes
            try
            {
                processes.AddRange(Process.GetProcessesByName("SysBot")
                    .Where(p => p.Id != Environment.ProcessId));
                processes.AddRange(Process.GetProcessesByName("SVRaidBot")
                    .Where(p => p.Id != Environment.ProcessId));
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error scanning RaidBot processes: {ex.Message}", "UpdateManager");
            }

            foreach (var process in processes)
            {
                try
                {
                    var instance = TryGetInstanceInfo(process);
                    if (instance.HasValue)
                        instances.Add(instance.Value);
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Error getting instance info for process {process.Id}: {ex.Message}", "UpdateManager");
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error scanning for instances: {ex.Message}", "UpdateManager");
        }

        return instances;
    }

    private static async Task<string> GetLatestVersionForBotType(string botType)
    {
        try
        {
            if (botType == "PokeBot")
            {
                var (updateAvailable, _, latestVersion) = await UpdateChecker.CheckForUpdatesAsync(false);
                return updateAvailable ? latestVersion ?? "Unknown" : "Unknown";
            }
            else if (botType == "RaidBot")
            {
                var (updateAvailable, _, latestVersion) = await RaidBotUpdateChecker.CheckForUpdatesAsync(false);
                return updateAvailable ? latestVersion ?? "Unknown" : "Unknown";
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error getting latest version for {botType}: {ex.Message}", "UpdateManager");
        }
        return "Unknown";
    }

    private static async Task<(bool updateAvailable, string currentVersion, string latestVersion)> CheckForUpdatesForBotType(string botType)
    {
        try
        {
            if (botType == "PokeBot")
            {
                var (updateAvailable, _, newVersion) = await UpdateChecker.CheckForUpdatesAsync(false);
                return (updateAvailable, "Unknown", newVersion);
            }
            else if (botType == "RaidBot")
            {
                var (updateAvailable, _, newVersion) = await RaidBotUpdateChecker.CheckForUpdatesAsync(false);
                return (updateAvailable, "Unknown", newVersion);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error checking updates for {botType}: {ex.Message}", "UpdateManager");
        }
        return (false, "Unknown", "Unknown");
    }

    // New staged update process implementation
    public static async Task<UpdateAllResult> StartUpdateProcessAsync(Main mainForm, int currentPort)
    {
        var result = new UpdateAllResult();
        
        try
        {
            LogUtil.LogInfo("Starting staged update process - Phase 1: Going to Idle", "UpdateManager");
            
            var instances = GetAllInstances(currentPort);
            result.TotalInstances = instances.Count;
            
            var instancesNeedingUpdate = new List<(int ProcessId, int Port, string Version, string BotType)>();
            
            // Check which instances need updates
            foreach (var instance in instances)
            {
                try
                {
                    var (updateAvailable, _, latestVersion) = await CheckForUpdatesForBotType(instance.BotType);
                    if (updateAvailable && !string.IsNullOrEmpty(latestVersion) && instance.Version != latestVersion)
                    {
                        instancesNeedingUpdate.Add((instance.ProcessId, instance.Port, instance.Version, instance.BotType));
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Error checking updates for instance {instance.Port}: {ex.Message}", "UpdateManager");
                }
            }
            
            result.UpdatesNeeded = instancesNeedingUpdate.Count;
            
            if (instancesNeedingUpdate.Count == 0)
            {
                LogUtil.LogInfo("All instances are up to date", "UpdateManager");
                return result;
            }
            
            LogUtil.LogInfo($"Found {instancesNeedingUpdate.Count} instances that need updates - sending idle commands", "UpdateManager");
            
            // Phase 1: Send idle commands to all instances
            foreach (var instance in instancesNeedingUpdate)
            {
                try
                {
                    bool idleSuccess = false;
                    if (instance.Port == currentPort)
                    {
                        // Local instance - use direct method call
                        idleSuccess = SendLocalIdleCommand(mainForm);
                    }
                    else
                    {
                        // Remote instance - use TCP command
                        var response = QueryRemote(instance.Port, "IDLE");
                        idleSuccess = !response.StartsWith("ERROR");
                    }
                    
                    var instanceResult = new InstanceUpdateResult
                    {
                        Port = instance.Port,
                        ProcessId = instance.ProcessId,
                        CurrentVersion = instance.Version,
                        LatestVersion = await GetLatestVersionForBotType(instance.BotType),
                        NeedsUpdate = true,
                        UpdateStarted = idleSuccess,
                        BotType = instance.BotType,
                        Error = idleSuccess ? null : "Failed to send idle command"
                    };
                    result.InstanceResults.Add(instanceResult);
                    
                    if (idleSuccess)
                    {
                        result.UpdatesStarted++;
                        LogUtil.LogInfo($"Sent idle command to {instance.BotType} instance on port {instance.Port}", "UpdateManager");
                    }
                    else
                    {
                        result.UpdatesFailed++;
                        LogUtil.LogError($"Failed to send idle command to instance on port {instance.Port}", "UpdateManager");
                    }
                }
                catch (Exception ex)
                {
                    result.UpdatesFailed++;
                    LogUtil.LogError($"Error sending idle command to instance {instance.Port}: {ex.Message}", "UpdateManager");
                    
                    var instanceResult = new InstanceUpdateResult
                    {
                        Port = instance.Port,
                        ProcessId = instance.ProcessId,
                        CurrentVersion = instance.Version,
                        LatestVersion = "Unknown",
                        NeedsUpdate = true,
                        UpdateStarted = false,
                        BotType = instance.BotType,
                        Error = ex.Message
                    };
                    result.InstanceResults.Add(instanceResult);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in StartUpdateProcessAsync: {ex.Message}", "UpdateManager");
            result.UpdatesFailed = 1;
            return result;
        }
    }
    
    public static async Task<UpdateAllResult> ProceedWithUpdatesAsync(Main mainForm, int currentPort)
    {
        var result = new UpdateAllResult();
        
        try
        {
            LogUtil.LogInfo("Starting sequential bot-by-bot update process - Phase 2: Execute Updates", "UpdateManager");
            
            var instances = GetAllInstances(currentPort);
            result.TotalInstances = instances.Count;
            
            var instancesNeedingUpdate = new List<(int ProcessId, int Port, string Version, string BotType, string ExecutablePath)>();
            
            // Get instances that need updates with their executable paths
            foreach (var instance in instances)
            {
                try
                {
                    var (updateAvailable, _, latestVersion) = await CheckForUpdatesForBotType(instance.BotType);
                    if (updateAvailable && !string.IsNullOrEmpty(latestVersion) && instance.Version != latestVersion)
                    {
                        // Get executable path for this instance
                        var executablePath = GetExecutablePathForInstance(instance.ProcessId, instance.Port);
                        if (!string.IsNullOrEmpty(executablePath))
                        {
                            instancesNeedingUpdate.Add((instance.ProcessId, instance.Port, instance.Version, instance.BotType, executablePath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Error checking updates for instance {instance.Port}: {ex.Message}", "UpdateManager");
                }
            }
            
            result.UpdatesNeeded = instancesNeedingUpdate.Count;
            
            if (instancesNeedingUpdate.Count == 0)
            {
                LogUtil.LogInfo("No instances need updates or could not determine executable paths", "UpdateManager");
                return result;
            }
            
            LogUtil.LogInfo($"Starting sequential updates for {instancesNeedingUpdate.Count} instances", "UpdateManager");
            
            // Process each instance sequentially: Stop → Update → Restart
            foreach (var (processId, port, currentVersion, botType, executablePath) in instancesNeedingUpdate)
            {
                var instanceResult = new InstanceUpdateResult
                {
                    Port = port,
                    ProcessId = processId,
                    CurrentVersion = currentVersion,
                    LatestVersion = await GetLatestVersionForBotType(botType),
                    NeedsUpdate = true,
                    BotType = botType
                };
                
                try
                {
                    LogUtil.LogInfo($"Starting update sequence for {botType} on port {port}", "UpdateManager");
                    
                    // Step 1: Verify bot is idle and stop it
                    bool stopSuccess = await StopBotInstance(port, processId, currentPort == port, mainForm);
                    if (!stopSuccess)
                    {
                        instanceResult.Error = "Failed to stop bot instance";
                        instanceResult.UpdateStarted = false;
                        result.UpdatesFailed++;
                        result.InstanceResults.Add(instanceResult);
                        continue;
                    }
                    
                    // Step 2: Download and install update
                    bool updateSuccess = await PerformBotUpdate(botType, executablePath);
                    if (!updateSuccess)
                    {
                        instanceResult.Error = "Failed to download or install update";
                        instanceResult.UpdateStarted = false;
                        result.UpdatesFailed++;
                        
                        // Try to restart the old version
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(2000);
                            await RestartBotInstance(executablePath, port);
                        });
                        
                        result.InstanceResults.Add(instanceResult);
                        continue;
                    }
                    
                    // Step 3: Restart with new version
                    bool restartSuccess = await RestartBotInstance(executablePath, port);
                    if (!restartSuccess)
                    {
                        instanceResult.Error = "Update completed but failed to restart bot";
                        instanceResult.UpdateStarted = true; // Update was successful
                        result.UpdatesStarted++;
                        result.InstanceResults.Add(instanceResult);
                        continue;
                    }
                    
                    // Success!
                    instanceResult.UpdateStarted = true;
                    result.UpdatesStarted++;
                    LogUtil.LogInfo($"Successfully updated and restarted {botType} on port {port}", "UpdateManager");
                    
                    // Wait a bit before processing next bot
                    await Task.Delay(3000);
                }
                catch (Exception ex)
                {
                    result.UpdatesFailed++;
                    instanceResult.Error = ex.Message;
                    instanceResult.UpdateStarted = false;
                    LogUtil.LogError($"Error updating instance {port}: {ex.Message}", "UpdateManager");
                }
                
                result.InstanceResults.Add(instanceResult);
            }
            
            LogUtil.LogInfo($"Update process completed. Success: {result.UpdatesStarted}, Failed: {result.UpdatesFailed}", "UpdateManager");
            return result;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in ProceedWithUpdatesAsync: {ex.Message}", "UpdateManager");
            result.UpdatesFailed = 1;
            return result;
        }
    }
    
    // Legacy method - kept for compatibility
    public static async Task<bool> PerformAutomaticUpdate(string botType, string latestVersion)
    {
        try
        {
            LogUtil.LogInfo($"Starting automatic {botType} update to version {latestVersion}", "UpdateManager");

            string? downloadUrl = null;
            
            // Get download URL based on bot type
            if (botType == "PokeBot")
            {
                downloadUrl = await UpdateChecker.FetchDownloadUrlAsync();
            }
            else if (botType == "RaidBot")
            {
                downloadUrl = await RaidBotUpdateChecker.FetchDownloadUrlAsync();
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                LogUtil.LogError($"Failed to fetch download URL for {botType}", "UpdateManager");
                return false;
            }

            LogUtil.LogInfo($"Downloading {botType} update from: {downloadUrl}", "UpdateManager");

            // Download new version
            string tempPath = await DownloadUpdateAsync(downloadUrl, botType);
            if (string.IsNullOrEmpty(tempPath))
            {
                LogUtil.LogError($"Failed to download {botType} update", "UpdateManager");
                return false;
            }

            LogUtil.LogInfo($"Download completed: {tempPath}", "UpdateManager");

            // Install update automatically
            bool installSuccess = await InstallUpdateAutomatically(tempPath, botType);
            
            if (installSuccess)
            {
                LogUtil.LogInfo($"Automatic {botType} update completed successfully", "UpdateManager");
            }
            else
            {
                LogUtil.LogError($"Failed to install {botType} update", "UpdateManager");
            }

            return installSuccess;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Automatic {botType} update failed: {ex.Message}", "UpdateManager");
            return false;
        }
    }

    private static async Task<string> DownloadUpdateAsync(string downloadUrl, string botType)
    {
        try
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(downloadUrl) || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
            {
                LogUtil.LogError("Invalid download URL provided", "UpdateManager");
                return string.Empty;
            }

            if (!ValidateBotType(botType))
            {
                LogUtil.LogError($"Invalid bot type: {botType}", "UpdateManager");
                return string.Empty;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), $"SysBot_{SanitizeForFilename(botType)}_{Guid.NewGuid()}.tmp");

            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "SysBot-AutoUpdate/1.0");
                client.Timeout = TimeSpan.FromMinutes(5); // Reduced timeout
                
                LogUtil.LogInfo($"Starting download to: {tempPath}", "UpdateManager");
                
                var response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                
                // Check content length before downloading
                if (response.Content.Headers.ContentLength > MAX_DOWNLOAD_SIZE)
                {
                    LogUtil.LogError($"Download size {response.Content.Headers.ContentLength} exceeds maximum allowed size {MAX_DOWNLOAD_SIZE}", "UpdateManager");
                    return string.Empty;
                }
                
                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                
                // Additional size check after download
                if (fileBytes.Length > MAX_DOWNLOAD_SIZE)
                {
                    LogUtil.LogError($"Downloaded file size {fileBytes.Length} exceeds maximum allowed size {MAX_DOWNLOAD_SIZE}", "UpdateManager");
                    return string.Empty;
                }
                
                await File.WriteAllBytesAsync(tempPath, fileBytes);
                
                LogUtil.LogInfo($"Download completed successfully. File size: {fileBytes.Length} bytes", "UpdateManager");
                return tempPath;
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Download failed: {ex.Message}", "UpdateManager");
            return string.Empty;
        }
    }

    private static async Task<bool> InstallUpdateAutomatically(string downloadedFilePath, string botType)
    {
        try
        {
            string currentExePath = Application.ExecutablePath;
            string applicationDirectory = Path.GetDirectoryName(currentExePath) ?? "";
            string executableName = Path.GetFileName(currentExePath);
            string backupPath = Path.Combine(applicationDirectory, $"{executableName}.backup");

            LogUtil.LogInfo($"Installing {botType} update: {downloadedFilePath} -> {currentExePath}", "UpdateManager");

            // Use safer .NET process management instead of batch files
            LogUtil.LogInfo($"Installing {botType} update using managed process approach", "UpdateManager");
            
            // Create backup
            if (File.Exists(currentExePath))
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Move(currentExePath, backupPath);
                LogUtil.LogInfo("Backup created successfully", "UpdateManager");
            }
            
            // Rename temp file to final executable name
            string finalPath = Path.ChangeExtension(downloadedFilePath, ".exe");
            File.Move(downloadedFilePath, finalPath);
            
            // Move to final location
            File.Move(finalPath, currentExePath);
            
            LogUtil.LogInfo("Update files installed successfully", "UpdateManager");

            // Schedule restart after brief delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000); // Wait 3 seconds
                
                try
                {
                    LogUtil.LogInfo("Starting updated application", "UpdateManager");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = currentExePath,
                        UseShellExecute = true,
                        WorkingDirectory = applicationDirectory
                    });
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Failed to start updated application: {ex.Message}", "UpdateManager");
                    
                    // Restore backup on failure
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            if (File.Exists(currentExePath))
                                File.Delete(currentExePath);
                            File.Move(backupPath, currentExePath);
                            Process.Start(currentExePath);
                            LogUtil.LogInfo("Backup restored and application restarted", "UpdateManager");
                        }
                        catch (Exception restoreEx)
                        {
                            LogUtil.LogError($"Failed to restore backup: {restoreEx.Message}", "UpdateManager");
                        }
                    }
                }
            });

            // Schedule clean shutdown
            LogUtil.LogInfo($"Scheduling clean shutdown for {botType} update", "UpdateManager");
            
            await Task.Delay(1000); // Brief delay to ensure file operations complete
            
            // Use safer shutdown approach
            if (Application.OpenForms.Count > 0 && Application.OpenForms[0] is Main mainForm)
            {
                mainForm.BeginInvoke((MethodInvoker)(() =>
                {
                    try
                    {
                        // Use reflection more safely
                        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        var isReallyClosingField = mainForm.GetType().GetField("_isReallyClosing", flags);
                        if (isReallyClosingField != null && isReallyClosingField.FieldType == typeof(bool))
                        {
                            isReallyClosingField.SetValue(mainForm, true);
                        }
                        
                        // Close the form properly
                        mainForm.Close();
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error during shutdown: {ex.Message}", "UpdateManager");
                        Application.Exit();
                    }
                }));
            }
            else
            {
                LogUtil.LogInfo("Using Application.Exit for shutdown", "UpdateManager");
                Application.Exit();
            }

            return true;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to install {botType} update: {ex.Message}", "UpdateManager");
            return false;
        }
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

                // Phase 2: Identify instances and check bot-type-specific updates
                status.Stage = "scanning";
                status.Message = "Scanning instances...";
                status.Progress = 10;

                var instances = GetAllInstancesWithBotType(currentPort);
                LogUtil.LogInfo($"Found {instances.Count} total instances", "UpdateManager");

                // Group instances by bot type and check updates for each type
                var pokeBotInstances = instances.Where(i => i.BotType == "PokeBot").ToList();
                var raidBotInstances = instances.Where(i => i.BotType == "RaidBot").ToList();

                var instancesNeedingUpdate = new List<(int ProcessId, int Port, string Version, string BotType)>();

                // Check PokeBot updates
                if (pokeBotInstances.Count > 0)
                {
                    LogUtil.LogInfo($"Checking updates for {pokeBotInstances.Count} PokeBot instances", "UpdateManager");
                    var (pokeBotUpdateAvailable, _, pokeBotLatestVersion) = await UpdateChecker.CheckForUpdatesAsync(false);
                    if (pokeBotUpdateAvailable && !string.IsNullOrEmpty(pokeBotLatestVersion))
                    {
                        var pokeBotNeedingUpdate = pokeBotInstances.Where(i => i.Version != pokeBotLatestVersion).ToList();
                        instancesNeedingUpdate.AddRange(pokeBotNeedingUpdate);
                        LogUtil.LogInfo($"{pokeBotNeedingUpdate.Count} PokeBot instances need updating to {pokeBotLatestVersion}", "UpdateManager");
                    }
                }

                // Check RaidBot updates
                if (raidBotInstances.Count > 0)
                {
                    LogUtil.LogInfo($"Checking updates for {raidBotInstances.Count} RaidBot instances", "UpdateManager");
                    try
                    {
                        var (raidBotUpdateAvailable, _, raidBotLatestVersion) = await RaidBotUpdateChecker.CheckForUpdatesAsync(false);
                        
                        if (raidBotUpdateAvailable && !string.IsNullOrEmpty(raidBotLatestVersion))
                        {
                            var raidBotNeedingUpdate = raidBotInstances.Where(i => i.Version != raidBotLatestVersion).ToList();
                            instancesNeedingUpdate.AddRange(raidBotNeedingUpdate);
                            LogUtil.LogInfo($"{raidBotNeedingUpdate.Count} RaidBot instances need updating to {raidBotLatestVersion}", "UpdateManager");
                        }
                        else
                        {
                            LogUtil.LogInfo("No RaidBot updates available", "UpdateManager");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"Error checking RaidBot updates: {ex.Message}", "UpdateManager");
                    }
                }

                if (instancesNeedingUpdate.Count == 0)
                {
                    status.Stage = "complete";
                    status.Message = "All instances are already up to date";
                    status.Progress = 100;
                    status.IsComplete = true;
                    status.Success = true;
                    LogUtil.LogInfo("No updates needed for any bot type", "UpdateManager");
                    return;
                }

                LogUtil.LogInfo($"{instancesNeedingUpdate.Count} total instances need updating", "UpdateManager");

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
                    var latestVersion = await GetLatestVersionForBotType(slave.BotType);
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

                    var masterLatestVersion = await GetLatestVersionForBotType(masterInstance.BotType);
                    var masterResult = new InstanceUpdateResult
                    {
                        Port = currentPort,
                        ProcessId = masterInstance.ProcessId,
                        CurrentVersion = masterInstance.Version,
                        LatestVersion = masterLatestVersion,
                        NeedsUpdate = true
                    };

                    try
                    {
                        // Start automatic update without dialog
                        _ = Task.Run(async () =>
                        {
                            await PerformAutomaticUpdate(masterInstance.BotType, masterLatestVersion);
                        });

                        masterResult.UpdateStarted = true;
                        status.Result.UpdatesStarted++;
                        LogUtil.LogInfo("Master instance automatic update triggered", "UpdateManager");
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

    public static UpdateStatus StartPokeBotUpdate(Main mainForm, int currentPort)
    {
        return StartSpecificBotTypeUpdate(mainForm, currentPort, "PokeBot");
    }

    public static UpdateStatus StartRaidBotUpdate(Main mainForm, int currentPort)
    {
        return StartSpecificBotTypeUpdate(mainForm, currentPort, "RaidBot");
    }

    private static UpdateStatus StartSpecificBotTypeUpdate(Main mainForm, int currentPort, string botType)
    {
        var status = new UpdateStatus();
        _activeUpdates[status.Id] = status;

        _ = Task.Run(async () =>
        {
            try
            {
                LogUtil.LogInfo($"Starting {botType} update process with ID: {status.Id}", "UpdateManager");

                // Phase 1: Check for updates
                status.Stage = "checking";
                status.Message = $"Checking for {botType} updates...";
                status.Progress = 10;

                var instances = GetAllInstancesWithBotType(currentPort);
                LogUtil.LogInfo($"Found {instances.Count} total instances", "UpdateManager");

                var targetInstances = instances.Where(i => i.BotType == botType).ToList();
                
                if (targetInstances.Count == 0)
                {
                    status.Stage = "complete";
                    status.Message = $"No {botType} instances found";
                    status.Progress = 100;
                    status.IsComplete = true;
                    status.Success = false;
                    LogUtil.LogInfo($"No {botType} instances found", "UpdateManager");
                    return;
                }

                LogUtil.LogInfo($"Found {targetInstances.Count} {botType} instances", "UpdateManager");

                // Check for updates for this specific bot type
                var instancesNeedingUpdate = new List<(int ProcessId, int Port, string Version, string BotType)>();
                
                if (botType == "PokeBot")
                {
                    var (pokeBotUpdateAvailable, _, pokeBotLatestVersion) = await UpdateChecker.CheckForUpdatesAsync(false);
                    if (pokeBotUpdateAvailable && !string.IsNullOrEmpty(pokeBotLatestVersion))
                    {
                        var pokeBotNeedingUpdate = targetInstances.Where(i => i.Version != pokeBotLatestVersion).ToList();
                        instancesNeedingUpdate.AddRange(pokeBotNeedingUpdate);
                        LogUtil.LogInfo($"{pokeBotNeedingUpdate.Count} PokeBot instances need updating to {pokeBotLatestVersion}", "UpdateManager");
                    }
                }
                else if (botType == "RaidBot")
                {
                    var (raidBotUpdateAvailable, _, raidBotLatestVersion) = await RaidBotUpdateChecker.CheckForUpdatesAsync(false);
                    if (raidBotUpdateAvailable && !string.IsNullOrEmpty(raidBotLatestVersion))
                    {
                        var raidBotNeedingUpdate = targetInstances.Where(i => i.Version != raidBotLatestVersion).ToList();
                        instancesNeedingUpdate.AddRange(raidBotNeedingUpdate);
                        LogUtil.LogInfo($"{raidBotNeedingUpdate.Count} RaidBot instances need updating to {raidBotLatestVersion}", "UpdateManager");
                    }
                }

                if (instancesNeedingUpdate.Count == 0)
                {
                    status.Stage = "complete";
                    status.Message = $"All {botType} instances are already up to date";
                    status.Progress = 100;
                    status.IsComplete = true;
                    status.Success = true;
                    LogUtil.LogInfo($"No {botType} updates needed", "UpdateManager");
                    return;
                }

                LogUtil.LogInfo($"{instancesNeedingUpdate.Count} {botType} instances need updating", "UpdateManager");

                status.Result = new UpdateAllResult
                {
                    TotalInstances = targetInstances.Count,
                    UpdatesNeeded = instancesNeedingUpdate.Count
                };

                IsSystemUpdateInProgress = true;

                // Phase 2: Send idle command to relevant instances
                status.Stage = "idling";
                status.Message = $"Idling {botType} bots across {instancesNeedingUpdate.Count} instances...";
                status.Progress = 20;

                await IdleSpecificInstances(mainForm, currentPort, instancesNeedingUpdate);
                await Task.Delay(1000);

                // Phase 3: Wait for bots to idle (shorter timeout for specific bot type)
                status.Stage = "waiting_idle";
                status.Message = $"Waiting for {botType} bots to finish current operations...";
                status.Progress = 40;

                var idleTimeout = DateTime.Now.AddMinutes(2); // Shorter timeout for specific bot types
                var allIdle = false;
                var lastIdleCheckTime = DateTime.Now;

                while (DateTime.Now < idleTimeout && !allIdle)
                {
                    allIdle = await CheckSpecificBotsIdleAsync(mainForm, currentPort, instancesNeedingUpdate);

                    if (!allIdle)
                    {
                        await Task.Delay(2000);
                        var elapsed = (DateTime.Now - status.StartTime).TotalSeconds;
                        var timeoutProgress = Math.Min(elapsed / 120 * 30, 30); // 120 seconds = 2 minutes
                        status.Progress = (int)(40 + timeoutProgress);

                        var remaining = (int)((120 - elapsed));
                        status.Message = $"Waiting for {botType} bots to idle... ({remaining}s remaining)";

                        if ((DateTime.Now - lastIdleCheckTime).TotalSeconds >= 10)
                        {
                            LogUtil.LogInfo($"Still waiting for {botType} bots to idle. {remaining}s remaining", "UpdateManager");
                            lastIdleCheckTime = DateTime.Now;
                        }
                    }
                }

                if (!allIdle)
                {
                    LogUtil.LogInfo($"Timeout reached while waiting for {botType} bots to idle. FORCING update.", "UpdateManager");
                    status.Message = $"Timeout reached. FORCING {botType} update despite active bots...";
                    status.Progress = 70;

                    await ForceStopSpecificBots(mainForm, currentPort, instancesNeedingUpdate);
                    await Task.Delay(1000);
                }
                else
                {
                    LogUtil.LogInfo($"All {botType} bots are idle. Proceeding with updates.", "UpdateManager");
                }

                // Phase 4: Update instances
                status.Stage = "updating";
                status.Message = $"Updating {botType} instances...";
                status.Progress = 80;

                var slaveInstances = instancesNeedingUpdate.Where(i => i.ProcessId != Environment.ProcessId).ToList();
                var masterInstance = instancesNeedingUpdate.FirstOrDefault(i => i.ProcessId == Environment.ProcessId);

                var slaveResults = new List<InstanceUpdateResult>();

                // Update slaves
                for (int i = 0; i < slaveInstances.Count; i++)
                {
                    var slave = slaveInstances[i];
                    var latestVersion = await GetLatestVersionForBotType(slave.BotType);
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
                        LogUtil.LogInfo($"Triggering {botType} update for instance on port {slave.Port} ({i + 1}/{slaveInstances.Count})...", "UpdateManager");

                        var updateResponse = BotServer.QueryRemote(slave.Port, "UPDATE");

                        if (!updateResponse.StartsWith("ERROR"))
                        {
                            instanceResult.UpdateStarted = true;
                            LogUtil.LogInfo($"{botType} update triggered for instance on port {slave.Port}", "UpdateManager");

                            if (i < slaveInstances.Count - 1)
                            {
                                await Task.Delay(3000);
                            }
                        }
                        else
                        {
                            instanceResult.Error = $"Failed to start update: {updateResponse}";
                            LogUtil.LogError($"Failed to trigger {botType} update for port {slave.Port}: {updateResponse}", "UpdateManager");
                        }
                    }
                    catch (Exception ex)
                    {
                        instanceResult.Error = ex.Message;
                        LogUtil.LogError($"Error updating {botType} instance on port {slave.Port}: {ex.Message}", "UpdateManager");
                    }

                    slaveResults.Add(instanceResult);
                }

                status.Result.InstanceResults.AddRange(slaveResults);

                var successfulSlaves = slaveResults.Count(r => r.UpdateStarted);
                status.Result.UpdatesStarted = successfulSlaves;
                status.Result.UpdatesFailed = slaveResults.Count(r => !r.UpdateStarted);

                // Update master if needed
                if (masterInstance.ProcessId != 0)
                {
                    status.Stage = "updating_master";
                    status.Message = $"Updating master {botType} instance...";
                    status.Progress = 95;

                    var updateFlagPath = Path.Combine(
                        Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory,
                        "update_in_progress.flag"
                    );
                    File.WriteAllText(updateFlagPath, DateTime.Now.ToString());

                    var masterLatestVersion = await GetLatestVersionForBotType(masterInstance.BotType);
                    var masterResult = new InstanceUpdateResult
                    {
                        Port = currentPort,
                        ProcessId = masterInstance.ProcessId,
                        CurrentVersion = masterInstance.Version,
                        LatestVersion = masterLatestVersion,
                        NeedsUpdate = true
                    };

                    try
                    {
                        // Start automatic update without dialog
                        _ = Task.Run(async () =>
                        {
                            await PerformAutomaticUpdate(masterInstance.BotType, masterLatestVersion);
                        });

                        masterResult.UpdateStarted = true;
                        status.Result.UpdatesStarted++;
                        LogUtil.LogInfo($"Master {botType} instance automatic update triggered", "UpdateManager");
                    }
                    catch (Exception ex)
                    {
                        masterResult.Error = ex.Message;
                        status.Result.UpdatesFailed++;
                        LogUtil.LogError($"Error updating master {botType} instance: {ex.Message}", "UpdateManager");
                    }

                    status.Result.InstanceResults.Add(masterResult);
                }

                // Complete
                status.Stage = "complete";
                status.Success = status.Result.UpdatesStarted > 0;
                status.Message = status.Success
                    ? $"{botType} update commands sent to {status.Result.UpdatesStarted} instances. They are now updating..."
                    : $"{botType} update failed - no instances were updated";
                status.Progress = 100;
                status.IsComplete = true;

                LogUtil.LogInfo($"{botType} update initiation completed: {status.Message}", "UpdateManager");
            }
            catch (Exception ex)
            {
                status.Stage = "error";
                status.Message = $"{botType} update failed: {ex.Message}";
                status.Progress = 0;
                status.IsComplete = true;
                status.Success = false;
                LogUtil.LogError($"{botType} update failed: {ex}", "UpdateManager");
            }
            finally
            {
                IsSystemUpdateInProgress = false;
            }
        });

        return status;
    }

    private static Task IdleAllInstances(Main mainForm, int currentPort, List<(int ProcessId, int Port, string Version, string BotType)> instances)
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

    private static Task IdleSpecificInstances(Main mainForm, int currentPort, List<(int ProcessId, int Port, string Version, string BotType)> instances)
    {
        var tasks = instances.Select(async instance =>
        {
            try
            {
                if (instance.ProcessId == Environment.ProcessId)
                {
                    mainForm.BeginInvoke((MethodInvoker)(() =>
                    {
                        var sendAllMethod = mainForm.GetType().GetMethod("SendAll",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        sendAllMethod?.Invoke(mainForm, [BotControlCommand.Idle]);
                    }));
                }
                else
                {
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

    private static Task ForceStopAllBots(Main mainForm, int currentPort, List<(int ProcessId, int Port, string Version, string BotType)> instances)
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

    private static Task ForceStopSpecificBots(Main mainForm, int currentPort, List<(int ProcessId, int Port, string Version, string BotType)> instances)
    {
        LogUtil.LogInfo("Force stopping specific bots due to idle timeout", "UpdateManager");

        var tasks = instances.Select(async instance =>
        {
            try
            {
                if (instance.ProcessId == Environment.ProcessId)
                {
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
        var remoteInstances = GetAllInstancesWithBotType(currentPort).Where(i => i.ProcessId != Environment.ProcessId);
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
            var instances = GetAllInstancesWithBotType(currentPort);
            foreach (var (processId, port, version, botType) in instances)
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
                    catch (Exception ex)
            {
                LogUtil.LogError($"Error in process operation: {ex.Message}", "UpdateManager");
            }
                }
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static Task<bool> CheckSpecificBotsIdleAsync(Main mainForm, int currentPort, List<(int ProcessId, int Port, string Version, string BotType)> instances)
    {
        try
        {
            // Check local bots if this process is in the list
            var localInstance = instances.FirstOrDefault(i => i.ProcessId == Environment.ProcessId);
            if (localInstance.ProcessId != 0)
            {
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
            }

            // Check remote instances
            foreach (var (processId, port, version, botType) in instances)
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
                    catch (Exception ex)
            {
                LogUtil.LogError($"Error in process operation: {ex.Message}", "UpdateManager");
            }
                }
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }


    private static (int ProcessId, int Port, string Version)? TryGetInstanceInfo(Process process, string botType)
    {
        try
        {
            var exePath = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                return null;

            var exeDir = Path.GetDirectoryName(exePath)!;
            
            // Try to find the appropriate port file
            var portFile = "";
            if (botType == "RaidBot")
            {
                portFile = Path.Combine(exeDir, $"SVRaidBot_{process.Id}.port");
                if (!File.Exists(portFile))
                {
                    // Sometimes RaidBots might also use PokeBot naming
                    portFile = Path.Combine(exeDir, $"PokeBot_{process.Id}.port");
                }
            }
            else
            {
                portFile = Path.Combine(exeDir, $"PokeBot_{process.Id}.port");
            }

            if (!File.Exists(portFile))
                return null;

            var portText = File.ReadAllText(portFile).Trim();
            if (!int.TryParse(portText, out var port))
                return null;

            if (!IsPortOpen(port))
                return null;

            var versionResponse = BotServer.QueryRemote(port, "VERSION");
            var version = versionResponse.StartsWith("ERROR") ? "Unknown" : versionResponse.Trim();

            return (process.Id, port, version);
        }
        catch
        {
            return null;
        }
    }

    private static List<(int ProcessId, int Port, string Version, string BotType)> GetAllInstancesWithBotType(int currentPort)
    {
        var instances = new List<(int, int, string, string)>();

        // Add current instance
        var currentBotType = DetectCurrentBotType();
        var currentVersion = GetVersionForCurrentBotType(currentBotType);
        instances.Add((Environment.ProcessId, currentPort, currentVersion, currentBotType));

        try
        {
            // Scan for PokeBot processes
            var pokeBotProcesses = Process.GetProcessesByName("PokeBot")
                .Where(p => p.Id != Environment.ProcessId);

            foreach (var process in pokeBotProcesses)
            {
                try
                {
                    var instance = TryGetInstanceInfoWithBotType(process, "PokeBot");
                    if (instance.HasValue)
                        instances.Add(instance.Value);
                }
                catch (Exception ex)
            {
                LogUtil.LogError($"Error in process operation: {ex.Message}", "UpdateManager");
            }
            }

            // Scan for RaidBot processes
            var raidBotProcesses = Process.GetProcessesByName("SysBot")
                .Where(p => p.Id != Environment.ProcessId);

            foreach (var process in raidBotProcesses)
            {
                try
                {
                    var instance = TryGetInstanceInfoWithBotType(process, "RaidBot");
                    if (instance.HasValue)
                        instances.Add(instance.Value);
                }
                catch (Exception ex)
            {
                LogUtil.LogError($"Error in process operation: {ex.Message}", "UpdateManager");
            }
            }
        }
        catch { }

        return instances;
    }

    private static (int ProcessId, int Port, string Version, string BotType)? TryGetInstanceInfoWithBotType(Process process, string botType)
    {
        try
        {
            var exePath = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                return null;

            var exeDir = Path.GetDirectoryName(exePath)!;
            
            // Try to find the appropriate port file
            var portFile = "";
            if (botType == "RaidBot")
            {
                portFile = Path.Combine(exeDir, $"SVRaidBot_{process.Id}.port");
                if (!File.Exists(portFile))
                {
                    // Sometimes RaidBots might also use PokeBot naming
                    portFile = Path.Combine(exeDir, $"PokeBot_{process.Id}.port");
                }
            }
            else
            {
                portFile = Path.Combine(exeDir, $"PokeBot_{process.Id}.port");
            }

            if (!File.Exists(portFile))
                return null;

            var portText = File.ReadAllText(portFile).Trim();
            if (!int.TryParse(portText, out var port))
                return null;

            if (!IsPortOpen(port))
                return null;

            var versionResponse = BotServer.QueryRemote(port, "VERSION");
            var version = versionResponse.StartsWith("ERROR") ? "Unknown" : versionResponse.Trim();

            return (process.Id, port, version, botType);
        }
        catch
        {
            return null;
        }
    }

    private static string DetectCurrentBotType()
    {
        try
        {
            // Check if we're in SVRaidBot by looking for specific RaidBot assemblies
            var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var assemblyName = currentAssembly.GetName().Name;
            
            // Check assembly name or location
            if (assemblyName?.Contains("RaidBot") == true || 
                currentAssembly.Location.Contains("SVRaidBot"))
            {
                return "RaidBot";
            }
            
            // Try to detect RaidBot by type availability
            var raidBotType = Type.GetType("SysBot.Pokemon.SV.BotRaid.Helpers.SVRaidBot, SysBot.Pokemon");
            if (raidBotType != null)
                return "RaidBot";
            
            // Try to detect PokeBot by type availability
            var pokeBotType = Type.GetType("SysBot.Pokemon.Helpers.PokeBot, SysBot.Pokemon");
            if (pokeBotType != null)
                return "PokeBot";

            // Fallback: check executable name
            var exeName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
            if (exeName.Contains("SVRaidBot", StringComparison.OrdinalIgnoreCase) || 
                exeName.Contains("RaidBot", StringComparison.OrdinalIgnoreCase))
                return "RaidBot";
            
            // Default to PokeBot
            return "PokeBot";
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error detecting bot type: {ex.Message}", "UpdateManager");
            return "PokeBot";
        }
    }

    private static string GetVersionForCurrentBotType(string botType)
    {
        try
        {
            if (botType == "RaidBot")
            {
                // Try multiple ways to get RaidBot version
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
                
                // Fallback: try to get from assembly version
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            else if (botType == "PokeBot")
            {
                // Try to get PokeBot version
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
                }
                catch
                {
                    // Ignore and fallback
                }
            }
            
            // Final fallback: assembly version
            var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            return currentAssembly.GetName().Version?.ToString() ?? "1.0.0";
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error getting version for {botType}: {ex.Message}", "UpdateManager");
            return "Unknown";
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
            var instances = GetAllInstancesWithBotType(currentPort);
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
            var instances = GetAllInstancesWithBotType(currentPort);
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
            catch (Exception ex)
            {
                LogUtil.LogError($"Error in process operation: {ex.Message}", "UpdateManager");
            }
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
    
    private static (int ProcessId, int Port, string Version, string BotType)? TryGetInstanceInfo(Process process)
    {
        try
        {
            var exePath = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                return null;

            var processName = process.ProcessName.ToLowerInvariant();
            var botType = processName.Contains("raid") || processName.Contains("sv") ? "RaidBot" : "PokeBot";
            
            var exeDir = Path.GetDirectoryName(exePath)!;
            
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

            if (string.IsNullOrEmpty(portFile))
                return null;

            var portText = File.ReadAllText(portFile).Trim();
            if (!int.TryParse(portText, out var port) || !IsPortValid(port))
                return null;

            if (!IsPortOpen(port))
                return null;

            // Get version via TCP query
            var versionResponse = QueryRemote(port, "INFO");
            var version = versionResponse.StartsWith("ERROR") ? "Unknown" : ExtractVersionFromResponse(versionResponse);

            return (process.Id, port, version, botType);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in TryGetInstanceInfo: {ex.Message}", "UpdateManager");
            return null;
        }
    }
    
    private static string ExtractVersionFromResponse(string response)
    {
        try
        {
            if (response.StartsWith("{"))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("Version", out var versionElement))
                {
                    return versionElement.GetString() ?? "Unknown";
                }
            }
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
    
    public static string QueryRemote(int port, string command)
    {
        return BotServer.QueryRemote(port, command);
    }
}
