using Discord.WebSocket;
using Discord.Net;
using SysBot.Base;

using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class ChannelLogger(ulong ChannelID, ISocketMessageChannel Channel) : ILogForwarder
{
    public ulong ChannelID { get; } = ChannelID;

    public string ChannelName => Channel.Name;

    public void Forward(string message, string identity)
    {
        var text = GetMessage(message, identity);
        _ = SafeSendAsync(text, identity);
    }

    private async Task SafeSendAsync(string text, string identity)
    {
        try
        {
            await Channel.SendMessageAsync(text).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, identity);
        }
    }

    private static string GetMessage(ReadOnlySpan<char> msg, string identity)
        => $"> [{DateTime.Now:HH:mm:ss}] - {identity}: {msg}";
}
