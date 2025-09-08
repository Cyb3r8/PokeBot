using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;

namespace SysBot.Pokemon.Twitch;

public class TwitchBot<T> : IChatBot where T : PKM, new()
{
    internal static readonly List<TwitchQueue<T>> QueuePool = new();
    private static readonly Dictionary<ulong, DateTime> UserLastCommand = new();

    public static PokeTradeHub<T> Hub = default!;
    private readonly PokeTradeHubConfig Config;
    private readonly TwitchSettings Settings;

    private TwitchClient? client;
    private bool isConnected = false;
    private bool isConnecting = false;
    private readonly object connectionLock = new();
    private CancellationToken cancellationToken;
    private Action<string>? echoForwarder;
    private System.Timers.Timer? userCommandCleanupTimer;

    public TwitchBot(TwitchSettings settings, PokeTradeHubConfig config)
    {
        Settings = settings;
        Config = config;
        
        // Setup cleanup timer for UserLastCommand dictionary
        userCommandCleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(10).TotalMilliseconds);
        userCommandCleanupTimer.Elapsed += CleanupUserCommands;
        userCommandCleanupTimer.Start();
    }

    public bool IsConnected => isConnected && client?.IsConnected == true;

    public async Task StartAsync(CancellationToken token)
    {
        cancellationToken = token;
        
        try
        {
            if (string.IsNullOrEmpty(Settings.Token) || string.IsNullOrEmpty(Settings.Channel))
            {
                LogUtil.LogError("Twitch Token oder Channel nicht konfiguriert - Twitch Bot wird übersprungen", nameof(TwitchBot<T>));
                return;
            }

            if (string.IsNullOrEmpty(Settings.Username))
            {
                LogUtil.LogError("Twitch Username nicht konfiguriert - Twitch Bot wird übersprungen", nameof(TwitchBot<T>));
                return;
            }

            await ConnectWithRetry();
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Twitch Bot konnte nicht gestartet werden: {ex.Message} - Bot läuft ohne Twitch weiter", nameof(TwitchBot<T>));
        }
    }

    private async Task ConnectWithRetry(int maxRetries = 10)
    {
        lock (connectionLock)
        {
            if (isConnecting || isConnected)
                return;
            isConnecting = true;
        }

        try
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    LogUtil.LogInfo($"Twitch Verbindungsversuch {attempt}/{maxRetries}...", nameof(TwitchBot<T>));
                    
                    await ConnectInternal();
                    
                    LogUtil.LogInfo("Twitch Bot erfolgreich verbunden!", nameof(TwitchBot<T>));
                    isConnected = true;
                    return;
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Twitch Verbindungsversuch {attempt} fehlgeschlagen: {ex.Message}", nameof(TwitchBot<T>));
                    
                    if (attempt < maxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(5 * attempt);
                        LogUtil.LogInfo($"Warte {delay.TotalSeconds} Sekunden vor nächstem Versuch...", nameof(TwitchBot<T>));
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }
            
            LogUtil.LogError($"Alle {maxRetries} Twitch Verbindungsversuche fehlgeschlagen - Twitch wird durch Supervisor neu gestartet", nameof(TwitchBot<T>));
            // Don't throw exception, let supervisor handle restart
            isConnected = false;
        }
        finally
        {
            lock (connectionLock)
            {
                isConnecting = false;
            }
        }
    }

    private async Task ConnectInternal()
    {
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = Settings.ThrottleMessages,
            ThrottlingPeriod = TimeSpan.FromSeconds(Settings.ThrottleSeconds),
            WhispersAllowedInPeriod = Settings.ThrottleWhispers,
            WhisperThrottlingPeriod = TimeSpan.FromSeconds(Settings.ThrottleWhispersSeconds)
        };
        
        var customClient = new WebSocketClient(clientOptions);
        client = new TwitchClient(customClient);

        var credentials = new ConnectionCredentials(Settings.Username.ToLower(), Settings.Token);
        var cmd = Settings.CommandPrefix;
        client.Initialize(credentials, Settings.Channel, cmd, cmd);

        client.OnLog += OnLog;
        client.OnJoinedChannel += OnJoinedChannel;
        client.OnMessageReceived += OnMessageReceived;
        client.OnWhisperReceived += OnWhisperReceived;
        client.OnChatCommandReceived += OnChatCommandReceived;
        client.OnWhisperCommandReceived += OnWhisperCommandReceived;
        client.OnConnected += OnConnected;
        client.OnIncorrectLogin += OnIncorrectLogin;
        client.OnConnectionError += OnConnectionError;
        client.OnDisconnected += OnDisconnected;
        client.OnFailureToReceiveJoinConfirmation += OnFailureToReceiveJoinConfirmation;
        client.OnLeftChannel += OnLeftChannel;

        client.OnMessageSent += (_, e)
            => LogUtil.LogText($"[{client.TwitchUsername}] - Message Sent in {e.SentMessage.Channel}: {e.SentMessage.Message}");
        client.OnWhisperSent += (_, e)
            => LogUtil.LogText($"[{client.TwitchUsername}] - Whisper Sent to @{e.Receiver}: {e.Message}");
        client.OnMessageThrottled += (_, e)
            => LogUtil.LogError($"Message Throttled: {e.Message}", "TwitchBot");
        client.OnWhisperThrottled += (_, e)
            => LogUtil.LogError($"Whisper Throttled: {e.Message}", "TwitchBot");
        client.OnError += (_, e) =>
            LogUtil.LogError(e.Exception.Message + Environment.NewLine + e.Exception.StackTrace, "TwitchBot");

        // Store the forwarder reference so we can remove it later
        echoForwarder = msg => SendMessage(msg);

        var connectTask = Task.Run(() => client.Connect());
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        
        var completedTask = await Task.WhenAny(connectTask, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            throw new TimeoutException("Twitch Verbindung Timeout nach 30 Sekunden");
        }
        
        if (connectTask.IsFaulted)
        {
            throw connectTask.Exception?.GetBaseException() ?? new Exception("Unbekannter Verbindungsfehler");
        }

        var joinWaitTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        await joinWaitTask;

        if (echoForwarder != null)
            EchoUtil.Forwarders.Add(echoForwarder);
    }

    private void OnConnected(object? sender, OnConnectedArgs e)
    {
        LogUtil.LogInfo($"Twitch Bot verbunden mit: {e.AutoJoinChannel}", nameof(TwitchBot<T>));
    }

    private void OnIncorrectLogin(object? sender, OnIncorrectLoginArgs e)
    {
        LogUtil.LogError($"Twitch Login fehlgeschlagen: {e.Exception.Message}", nameof(TwitchBot<T>));
    }

    private void OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        LogUtil.LogError($"Twitch Verbindungsfehler: {e.Error.Message}", nameof(TwitchBot<T>));
        isConnected = false;
        
        // Let supervisor handle reconnection, don't auto-reconnect here
        LogUtil.LogInfo("Twitch connection error - supervisor will handle restart", nameof(TwitchBot<T>));
    }

    private void OnDisconnected(object? sender, OnDisconnectedEventArgs e)
    {
        LogUtil.LogInfo("Twitch Verbindung getrennt", nameof(TwitchBot<T>));
        isConnected = false;
        
        // Let supervisor handle reconnection after disconnect
        LogUtil.LogInfo("Twitch disconnect detected - supervisor will handle restart", nameof(TwitchBot<T>));
    }

    private void OnFailureToReceiveJoinConfirmation(object? sender, OnFailureToReceiveJoinConfirmationArgs e)
    {
        LogUtil.LogError($"Twitch Join-Bestätigung fehlgeschlagen für Channel: {e.Exception.Channel}", nameof(TwitchBot<T>));
    }

    private void OnLog(object? sender, OnLogArgs e)
    {
        // Filter out ping/pong and other routine messages to reduce log spam
        var data = e.Data?.ToLower() ?? "";
        if (data.Contains("ping") || data.Contains("pong") || 
            data.Contains("privmsg") || data.Contains("usernotice") ||
            data.Contains("roomstate") || data.Contains("userstate"))
            return;
            
        // Only log important events like errors or connection status
        if (data.Contains("error") || data.Contains("disconnect") || 
            data.Contains("connect") || data.Contains("join") || 
            data.Contains("notice"))
        {
            LogUtil.LogInfo($"Twitch: {e.Data}", nameof(TwitchBot<T>));
        }
    }

    private void OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        LogUtil.LogInfo($"Twitch Bot joined channel: {e.Channel}", nameof(TwitchBot<T>));
        // Don't add forwarder again - already added in ConnectInternal
    }

    private void OnLeftChannel(object? sender, OnLeftChannelArgs e)
    {
        LogUtil.LogText($"[{client?.TwitchUsername}] - Left channel {e.Channel}");
        try
        {
            if (client?.IsConnected == true)
                client.JoinChannel(e.Channel);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to rejoin channel {e.Channel}: {ex.Message}", "TwitchBot");
        }
    }

    private void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        LogUtil.LogText($"[{client?.TwitchUsername}] - @{e.ChatMessage.Username}: {e.ChatMessage.Message}");
        
        var msg = e.ChatMessage;
        if (msg.Message.StartsWith(Settings.CommandPrefix) && Settings.AllowCommandsViaChannel)
        {
            var cmd = msg.Message.Substring(Settings.CommandPrefix.ToString().Length);
            ExecuteCommand(msg.Username, cmd, msg.Channel);
        }
        
        try
        {
            if (client?.JoinedChannels.Count == 0 && client?.IsConnected == true)
                client.JoinChannel(e.ChatMessage.Channel);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to join channel {e.ChatMessage.Channel}: {ex.Message}", "TwitchBot");
        }
    }

    private void OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
    {
        LogUtil.LogText($"[{client?.TwitchUsername}] - @{e.WhisperMessage.Username}: {e.WhisperMessage.Message}");
        
        var msg = e.WhisperMessage;
        if (msg.Message.StartsWith(Settings.CommandPrefix) && Settings.AllowCommandsViaWhisper)
        {
            var cmd = msg.Message.Substring(Settings.CommandPrefix.ToString().Length);
            ExecuteCommand(msg.Username, cmd, msg.Username);
        }
    }

    private void OnChatCommandReceived(object? sender, OnChatCommandReceivedArgs e)
    {
        if (!Settings.AllowCommandsViaChannel)
            return;

        var msg = e.Command.ChatMessage;
        var c = e.Command.CommandText.ToLower();
        var args = e.Command.ArgumentsAsString;
        var response = HandleCommand(msg, c, args, false);
        if (response.Length == 0)
            return;

        var channel = e.Command.ChatMessage.Channel;
        SendMessage(response);
    }

    private void OnWhisperCommandReceived(object? sender, OnWhisperCommandReceivedArgs e)
    {
        if (!Settings.AllowCommandsViaWhisper)
            return;

        var msg = e.Command.WhisperMessage;
        var c = e.Command.CommandText.ToLower();
        var args = e.Command.ArgumentsAsString;
        var response = HandleCommand(msg, c, args, true);
        if (response.Length == 0)
            return;

        SendWhisper(msg.Username, response);
    }

    internal static TradeQueueInfo<T> Info => Hub.Queues.Info;

    public void SendMessage(string message)
    {
        try
        {
            if (isConnected && client?.IsConnected == true)
            {
                client.SendMessage(Settings.Channel, message);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Fehler beim Senden der Twitch Nachricht: {ex.Message}", nameof(TwitchBot<T>));
        }
    }

    private void SendWhisper(string user, string message)
    {
        try
        {
            if (isConnected && client?.IsConnected == true)
            {
                client.SendWhisper(user, message);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Fehler beim Senden der Twitch Whisper: {ex.Message}", nameof(TwitchBot<T>));
        }
    }

    public void StartingDistribution(string message)
    {
        if (isConnected && client?.IsConnected == true)
            SendMessage(message);
    }

    private void ExecuteCommand(string username, string cmd, string channel)
    {
        try
        {
            var now = DateTime.UtcNow;
            var userId = (ulong)username.GetHashCode();
            
            if (UserLastCommand.TryGetValue(userId, out var lastCommand))
            {
                var timeSince = now - lastCommand;
                if (timeSince < TimeSpan.FromSeconds(Settings.ThrottleSeconds))
                    return;
            }
            
            UserLastCommand[userId] = now;

            // TODO: Implement command handling
            // This is a simplified version that removes complex command processing
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Fehler bei Twitch Kommando-Verarbeitung: {ex.Message}", nameof(TwitchBot<T>));
        }
    }


    public Task StopAsync()
    {
        try
        {
            CleanupResources();
            LogUtil.LogInfo("Twitch Bot gestoppt", nameof(TwitchBot<T>));
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Fehler beim Stoppen des Twitch Bots: {ex.Message}", nameof(TwitchBot<T>));
        }
        return Task.CompletedTask;
    }

    public void Stop()
    {
        try
        {
            CleanupResources();
            LogUtil.LogInfo("Twitch Bot gestoppt", nameof(TwitchBot<T>));
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Fehler beim Stoppen des Twitch Bots: {ex.Message}", nameof(TwitchBot<T>));
        }
    }

    public void Dispose()
    {
        try
        {
            CleanupResources();
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Fehler beim Dispose des Twitch Bots: {ex.Message}", nameof(TwitchBot<T>));
        }
    }

    private void CleanupResources()
    {
        isConnected = false;
        
        // Cleanup event handlers before disconnecting
        UnsubscribeEventHandlers();
        
        // Disconnect and dispose client
        client?.Disconnect();
        client = null;
        
        // Remove echo forwarder
        if (echoForwarder != null)
        {
            try
            {
                EchoUtil.Forwarders.Remove(echoForwarder);
                echoForwarder = null;
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error removing echo forwarder: {ex.Message}", nameof(TwitchBot<T>));
            }
        }
        
        // Cleanup timer
        userCommandCleanupTimer?.Stop();
        userCommandCleanupTimer?.Dispose();
        userCommandCleanupTimer = null;
        
        // Clear user command cache
        UserLastCommand.Clear();
    }

    private void UnsubscribeEventHandlers()
    {
        if (client != null)
        {
            try
            {
                client.OnLog -= OnLog;
                client.OnJoinedChannel -= OnJoinedChannel;
                client.OnMessageReceived -= OnMessageReceived;
                client.OnWhisperReceived -= OnWhisperReceived;
                client.OnChatCommandReceived -= OnChatCommandReceived;
                client.OnWhisperCommandReceived -= OnWhisperCommandReceived;
                client.OnConnected -= OnConnected;
                client.OnIncorrectLogin -= OnIncorrectLogin;
                client.OnConnectionError -= OnConnectionError;
                client.OnDisconnected -= OnDisconnected;
                client.OnFailureToReceiveJoinConfirmation -= OnFailureToReceiveJoinConfirmation;
                client.OnLeftChannel -= OnLeftChannel;
                
                // Remove anonymous event handlers
                client.OnMessageSent -= (_, e) => LogUtil.LogText($"[{client.TwitchUsername}] - Message Sent in {e.SentMessage.Channel}: {e.SentMessage.Message}");
                client.OnWhisperSent -= (_, e) => LogUtil.LogText($"[{client.TwitchUsername}] - Whisper Sent to @{e.Receiver}: {e.Message}");
                client.OnMessageThrottled -= (_, e) => LogUtil.LogError($"Message Throttled: {e.Message}", "TwitchBot");
                client.OnWhisperThrottled -= (_, e) => LogUtil.LogError($"Whisper Throttled: {e.Message}", "TwitchBot");
                client.OnError -= (_, e) => LogUtil.LogError(e.Exception.Message + Environment.NewLine + e.Exception.StackTrace, "TwitchBot");
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error unsubscribing event handlers: {ex.Message}", nameof(TwitchBot<T>));
            }
        }
    }

    private void CleanupUserCommands(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-1); // Remove entries older than 1 hour
            var keysToRemove = UserLastCommand.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
            
            foreach (var key in keysToRemove)
            {
                UserLastCommand.Remove(key);
            }
            
            if (keysToRemove.Count > 0)
            {
                LogUtil.LogInfo($"Cleaned up {keysToRemove.Count} old user command entries", nameof(TwitchBot<T>));
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error during user command cleanup: {ex.Message}", nameof(TwitchBot<T>));
        }
    }



    private string HandleCommand(TwitchLibMessage m, string c, string args, bool whisper)
    {
        bool sudo() => m is ChatMessage ch && (ch.IsBroadcaster || Settings.IsSudo(m.Username));
        bool subscriber() => m is ChatMessage { IsSubscriber: true };

        switch (c)
        {
            case "donate":
                return Settings.DonationLink.Length > 0 ? $"Here's the donation link! Thank you for your support :3 {Settings.DonationLink}" : string.Empty;

            case "discord":
                return Settings.DiscordLink.Length > 0 ? $"Here's the Discord Server Link, have a nice stay :3 {Settings.DiscordLink}" : string.Empty;

            case "tutorial":
            case "help":
                return $"{Settings.TutorialText} {Settings.TutorialLink}";

            case "trade":
            case "t":
                var _ = TwitchCommandsHelper<T>.AddToWaitingList(args, m.DisplayName, m.Username, ulong.Parse(m.UserId), subscriber(), out string msg);
                if (msg.Contains("Please read what you are supposed to type") && Settings.TutorialLink.Length > 0)
                    msg += $"\nUsage Tutorial: {Settings.TutorialLink}";
                return msg;

            case "ts":
            case "queue":
            case "position":
                var userID = ulong.Parse(m.UserId);
                var tradeEntry = Info.GetDetail(userID);
                if (tradeEntry != null)
                {
                    var uniqueTradeID = tradeEntry.UniqueTradeID;
                    return $"@{m.Username}: {Info.GetPositionString(userID, uniqueTradeID)}";
                }
                else
                {
                    return $"@{m.Username}: You are not currently in the queue.";
                }
            case "tc":
            case "cancel":
            case "remove":
                return $"@{m.Username}: {TwitchCommandsHelper<T>.ClearTrade(ulong.Parse(m.UserId))}";

            case "code" when whisper:
                return TwitchCommandsHelper<T>.GetCode(ulong.Parse(m.UserId));

            case "tca" when !sudo():
            case "pr" when !sudo():
            case "pc" when !sudo():
            case "tt" when !sudo():
            case "tcu" when !sudo():
                return "This command is locked for sudo users only!";

            case "tca":
                Info.ClearAllQueues();
                return "Cleared all queues!";

            case "pr":
                return Info.Hub.Ledy.Pool.Reload(Hub.Config.Folder.DistributeFolder) ? $"Reloaded from folder. Pool count: {Info.Hub.Ledy.Pool.Count}" : "Failed to reload from folder.";

            case "pc":
                return $"The pool count is: {Info.Hub.Ledy.Pool.Count}";

            case "tt":
                return Info.Hub.Queues.Info.ToggleQueue()
                    ? "Users are now able to join the trade queue."
                    : "Changed queue settings: **Users CANNOT join the queue until it is turned back on.**";

            case "tcu":
                return TwitchCommandsHelper<T>.ClearTrade(args);

            default: return string.Empty;
        }
    }
}
