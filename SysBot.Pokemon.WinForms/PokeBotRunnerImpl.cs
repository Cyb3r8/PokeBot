using PKHeX.Core;
using SysBot.Pokemon.Discord;
using SysBot.Pokemon.Twitch;
using SysBot.Pokemon.WinForms;
using SysBot.Pokemon.YouTube;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

/// <summary>
/// Bot Environment implementation with Integrations added.
/// </summary>
public class PokeBotRunnerImpl<T> : PokeBotRunner<T> where T : PKM, new()
{
    private YouTubeBot<T>? YouTube;
    private static TwitchBot<T>? Twitch;
    private readonly ProgramConfig _config;
    private CancellationTokenSource? _twitchCancellationSource;

    public PokeBotRunnerImpl(PokeTradeHub<T> hub, BotFactory<T> fac, ProgramConfig config) : base(hub, fac)
    {
        _config = config;
    }

    public PokeBotRunnerImpl(PokeTradeHubConfig config, BotFactory<T> fac, ProgramConfig programConfig) : base(config, fac)
    {
        _config = programConfig;
    }

    protected override void AddIntegrations()
    {
        AddDiscordBot(Hub.Config.Discord.Token);
        AddTwitchBot(Hub.Config.Twitch);
        AddYouTubeBot(Hub.Config.YouTube);
    }

    private void AddDiscordBot(string apiToken)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
            return;
        var bot = new SysCord<T>(this, _config);
        Task.Run(() => bot.MainAsync(apiToken, CancellationToken.None));
    }

    private void AddTwitchBot(TwitchSettings config)
    {
        if (string.IsNullOrWhiteSpace(config.Token))
            return;
        if (Twitch != null)
            return; // already created

        if (string.IsNullOrWhiteSpace(config.Channel))
            return;
        if (string.IsNullOrWhiteSpace(config.Username))
            return;
        if (string.IsNullOrWhiteSpace(config.Token))
            return;

        Twitch = new TwitchBot<T>(Hub.Config.Twitch, Hub.Config);
        TwitchBot<T>.Hub = Hub;
        
        // Start Twitch Bot in isolated task with independent cancellation
        _twitchCancellationSource = new CancellationTokenSource();
        StartTwitchBotIsolated(_twitchCancellationSource.Token);
        
        if (Hub.Config.Twitch.DistributionCountDown)
            Hub.BotSync.BarrierReleasingActions.Add(() => Twitch?.StartingDistribution(config.MessageStart));
    }

    private void StartTwitchBotIsolated(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    SysBot.Base.LogUtil.LogInfo("Starting isolated Twitch Bot...", nameof(PokeBotRunnerImpl<T>));
                    
                    if (Twitch != null)
                    {
                        await Twitch.StartAsync(cancellationToken);
                        
                        // If we reach here, Twitch started successfully
                        // Keep monitoring connection status
                        while (!cancellationToken.IsCancellationRequested && Twitch.IsConnected)
                        {
                            await Task.Delay(30000, cancellationToken); // Check every 30 seconds
                        }
                        
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            SysBot.Base.LogUtil.LogInfo("Twitch connection lost, attempting restart...", nameof(PokeBotRunnerImpl<T>));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    SysBot.Base.LogUtil.LogInfo("Twitch Bot shutdown requested", nameof(PokeBotRunnerImpl<T>));
                    break;
                }
                catch (Exception ex)
                {
                    SysBot.Base.LogUtil.LogError($"Twitch Bot crashed: {ex.Message} - Restarting in 60 seconds", nameof(PokeBotRunnerImpl<T>));
                    
                    try
                    {
                        // Clean up current Twitch instance
                        Twitch?.Stop();
                        
                        // Wait before restart to avoid rapid reconnection attempts
                        await Task.Delay(60000, cancellationToken);
                        
                        // Recreate Twitch instance
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Twitch = new TwitchBot<T>(Hub.Config.Twitch, Hub.Config);
                            TwitchBot<T>.Hub = Hub;
                            SysBot.Base.LogUtil.LogInfo("Twitch Bot instance recreated, retrying connection...", nameof(PokeBotRunnerImpl<T>));
                        }
                    }
                    catch (Exception restartEx)
                    {
                        SysBot.Base.LogUtil.LogError($"Failed to restart Twitch Bot: {restartEx.Message}", nameof(PokeBotRunnerImpl<T>));
                    }
                }
            }
            
            SysBot.Base.LogUtil.LogInfo("Twitch Bot task terminated", nameof(PokeBotRunnerImpl<T>));
        }, cancellationToken);
    }

    private void AddYouTubeBot(YouTubeSettings config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientID))
            return;
        if (YouTube != null)
            return; // already created

        WinFormsUtil.Alert("Please Login with your Browser");
        if (string.IsNullOrWhiteSpace(config.ChannelID))
            return;
        if (string.IsNullOrWhiteSpace(config.ClientID))
            return;
        if (string.IsNullOrWhiteSpace(config.ClientSecret))
            return;

        YouTube = new YouTubeBot<T>(Hub.Config.YouTube, Hub);
        Hub.BotSync.BarrierReleasingActions.Add(() => YouTube.StartingDistribution(config.MessageStart));
    }
}