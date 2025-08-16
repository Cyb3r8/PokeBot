using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;

namespace SysBot.Pokemon.Twitch
{
    public static class TwitchCommandsHelper<T> where T : PKM, new()
    {
        // Helper functions for commands
        public static bool AddToWaitingList(string setstring, string display, string username, ulong mUserId, bool sub, out string msg)
        {
            if (!TwitchBot<T>.Info.GetCanQueue())
            {
                msg = "Sorry, I am not currently accepting queue requests!";
                return false;
            }

            var set = ShowdownUtil.ConvertToShowdown(setstring);
            if (set == null)
            {
                msg = $"Skipping trade, @{username}: Empty nickname provided for the species.";
                return false;
            }
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (template.Species < 1)
            {
                msg = $"Skipping trade, @{username}: Please read what you are supposed to type as the command argument.";
                return false;
            }

            if (set.InvalidLines.Count != 0)
            {
                msg = $"Skipping trade, @{username}: Unable to parse Showdown Set:\n{string.Join("\n", set.InvalidLines)}";
                return false;
            }

            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                PKM pkm = sav.GetLegal(template, out var result);

                var nickname = pkm.Nickname.ToLower();
                if (nickname == "egg" && Breeding.CanHatchAsEgg(pkm.Species))
                    TradeExtensions<T>.EggTrade(pkm, template);

                if (pkm.Species == 132 && (nickname.Contains("atk") || nickname.Contains("spa") || nickname.Contains("spe") || nickname.Contains("6iv")))
                    TradeExtensions<T>.DittoTrade(pkm);

                if (!pkm.CanBeTraded())
                {
                    msg = $"Skipping trade, @{username}: Provided Pokémon content is blocked from trading!";
                    return false;
                }

                if (pkm is T pk)
                {
                    var valid = new LegalityAnalysis(pkm).Valid;
                    if (valid)
                    {
                        // Generate trade code directly here like Discord does
                        var code = TwitchBot<T>.Info.GetRandomTradeCode(mUserId);
                        List<Pictocodes>? lgCode = null;
                        
                        // Check if this is LGPE (PB7) - generate pictocodes automatically
                        if (typeof(T) == typeof(PB7))
                        {
                            lgCode = TwitchBot<T>.Info.GetRandomLGTradeCode();
                        }
                        
                        // Add to trade queue immediately
                        var trainer = new PokeTradeTrainerInfo(display, mUserId);
                        var notifier = new TwitchTradeNotifier<T>(pk, trainer, code, username, TwitchBot<T>.GetClient(), TwitchBot<T>.GetChannel(), TwitchBot<T>.Hub.Config.Twitch);
                        var tt = PokeTradeType.Specific;
                        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, tt, code, sub, lgCode);
                        var uniqueTradeID = TwitchBot<T>.GenerateUniqueTradeID();
                        var trade = new TradeEntry<T>(detail, mUserId, PokeRoutineType.LinkTrade, display, uniqueTradeID);

                        var added = TwitchBot<T>.Info.AddToTradeQueue(trade, mUserId, sub);

                        if (added == QueueResultAdd.AlreadyInQueue)
                        {
                            msg = $"@{username}: Sorry, you are already in the queue.";
                            return false;
                        }

                        var position = TwitchBot<T>.Info.CheckPosition(mUserId, uniqueTradeID, PokeRoutineType.LinkTrade);
                        msg = $"@{username}: Added to the LinkTrade queue, unique ID: {detail.ID}. Current Position: {(position.Position == -1 ? 1 : position.Position)}";

                        var botct = TwitchBot<T>.Info.Hub.Bots.Count;
                        if (position.Position > botct)
                        {
                            var eta = TwitchBot<T>.Info.Hub.Config.Queues.EstimateDelay(position.Position, botct);
                            msg += $". Estimated: {eta:F1} minutes.";
                        }
                        
                        // Automatically send trade code via whisper
                        try
                        {
                            var client = TwitchBot<T>.GetClient();
                            
                            // Check if this is LGPE (PB7) and send appropriate code
                            if (typeof(T) == typeof(PB7) && lgCode != null && lgCode.Count > 0)
                            {
                                var codeString = string.Join(", ", lgCode);
                                client.SendWhisper(username, $"Your LGPE trade code: {codeString}");
                                LogUtil.LogText($"[TwitchBot] Sent LGPE whisper to {username}: {codeString}");
                            }
                            else
                            {
                                client.SendWhisper(username, $"Your trade code is {code:0000 0000}");
                                LogUtil.LogText($"[TwitchBot] Sent whisper to {username}: {code:0000 0000}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogUtil.LogError($"Failed to send whisper to {username}: {ex.Message}", "TwitchBot");
                        }
                        
                        msg += " Check your whispers for your trade code!";
                        
                        return true;
                    }
                }

                var reason = result == "Timeout" ? "Set took too long to generate." : "Unable to legalize the Pokémon.";
                msg = $"Skipping trade, @{username}: {reason}";
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TwitchCommandsHelper<T>));
                msg = $"Skipping trade, @{username}: An unexpected problem occurred.";
            }
            return false;
        }

        public static string ClearTrade(string user)
        {
            var result = TwitchBot<T>.Info.ClearTrade(user);
            return GetClearTradeMessage(result);
        }

        public static string ClearTrade(ulong userID)
        {
            var result = TwitchBot<T>.Info.ClearTrade(userID);
            return GetClearTradeMessage(result);
        }

        public static string GetCode(ulong parse)
        {
            var detail = TwitchBot<T>.Info.GetDetail(parse);
            return detail == null
                ? "Sorry, you are not currently in the queue."
                : $"Your trade code is {detail.Trade.Code:0000 0000}";
        }

        private static string GetClearTradeMessage(QueueResultRemove result)
        {
            return result switch
            {
                QueueResultRemove.CurrentlyProcessing => "Looks like you're currently being processed! Did not remove from queue.",
                QueueResultRemove.CurrentlyProcessingRemoved => "Looks like you're currently being processed! Removed from queue.",
                QueueResultRemove.Removed => "Removed you from the queue.",
                _ => "Sorry, you are not currently in the queue.",
            };
        }
    }
}
