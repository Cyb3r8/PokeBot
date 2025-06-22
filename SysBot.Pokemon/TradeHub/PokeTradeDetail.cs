using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SysBot.Pokemon
{
    public class PokeTradeDetail<TPoke> : IEquatable<PokeTradeDetail<TPoke>>, IFavoredEntry where TPoke : PKM, new()
    {
        private static readonly string TradeCountFile = "trade_count.txt";
        private static int CreatedCount;

        static PokeTradeDetail()
        {
            CreatedCount = LoadTradeCount();
        }

        public bool IsFavored { get; }
        public Dictionary<string, object> Context = [];
        public readonly int Code;
        public TPoke TradeData;
        public readonly PokeTradeTrainerInfo Trainer;
        public readonly IPokeTradeNotifier<TPoke> Notifier;
        public readonly PokeTradeType Type;
        public readonly DateTime Time;
        public readonly int ID;
        public bool IsSynchronized => Type == PokeTradeType.Random;
        public bool IsRetry;
        public bool IsProcessing;
        public List<Pictocodes> LGPETradeCode;
        public readonly int BatchTradeNumber;
        public readonly int TotalBatchTrades;
        public readonly int UniqueTradeID;
        public bool IsCanceled { get; set; }
        public bool IsMysteryEgg { get; }
        public bool IgnoreAutoOT { get; }
        public bool SetEdited { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public PokeTradeDetail(TPoke pkm, PokeTradeTrainerInfo info, IPokeTradeNotifier<TPoke> notifier, PokeTradeType type, int code, bool favored = false, List<Pictocodes>? lgcode = null, int batchTradeNumber = 0, int totalBatchTrades = 0, bool isMysteryEgg = false, int uniqueTradeID = 0, bool ignoreAutoOT = false, bool setEdited = false)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            ID = GetNextTradeID();
            Code = code;
            TradeData = pkm;
            Trainer = info;
            Notifier = notifier;
            Type = type;
            Time = DateTime.Now;
            IsFavored = favored;
#pragma warning disable CS8601 // Possible null reference assignment.
            LGPETradeCode = lgcode;
#pragma warning restore CS8601 // Possible null reference assignment.
            BatchTradeNumber = batchTradeNumber;
            TotalBatchTrades = totalBatchTrades;
            IsMysteryEgg = isMysteryEgg;
            UniqueTradeID = uniqueTradeID;
            IgnoreAutoOT = ignoreAutoOT;
            SetEdited = setEdited;
        }

        private static int LoadTradeCount()
        {
            if (!File.Exists(TradeCountFile))
            {
                try
                {
                    File.WriteAllText(TradeCountFile, "0");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating trade count file: {ex.Message}");
                }
                return 0;
            }

            try
            {
                string content = File.ReadAllText(TradeCountFile);
                if (int.TryParse(content, out int count))
                    return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading trade count file: {ex.Message}");
            }

            return 0;
        }

        private static int GetNextTradeID()
        {
            int newCount = Interlocked.Increment(ref CreatedCount);

            try
            {
                File.WriteAllText(TradeCountFile, newCount.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing trade count file: {ex.Message}");
            }

            return newCount;
        }

        public void TradeInitialize(PokeRoutineExecutor<TPoke> routine) => Notifier.TradeInitialize(routine, this);

        public void TradeSearching(PokeRoutineExecutor<TPoke> routine) => Notifier.TradeSearching(routine, this);

        public void TradeCanceled(PokeRoutineExecutor<TPoke> routine, PokeTradeResult msg) => Notifier.TradeCanceled(routine, this, msg);

        public virtual void TradeFinished(PokeRoutineExecutor<TPoke> routine, TPoke result) => Notifier.TradeFinished(routine, this, result);

        public void SendNotification(PokeRoutineExecutor<TPoke> routine, string message) => Notifier.SendNotification(routine, this, message);

        public void SendNotification(PokeRoutineExecutor<TPoke> routine, PokeTradeSummary obj) => Notifier.SendNotification(routine, this, obj);

        public void SendNotification(PokeRoutineExecutor<TPoke> routine, TPoke obj, string message) => Notifier.SendNotification(routine, this, obj, message);

        public bool Equals(PokeTradeDetail<TPoke>? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Trainer.ID == other.Trainer.ID && UniqueTradeID == other.UniqueTradeID;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PokeTradeDetail<TPoke>)obj);
        }

        public override int GetHashCode() => HashCode.Combine(Trainer.ID, UniqueTradeID);

        public override string ToString() => $"{Trainer.TrainerName} - {Code}";

        public string Summary(int queuePosition)
        {
            if (TradeData.Species == 0)
                return $"{queuePosition:00}: {Trainer.TrainerName}";
            return $"{queuePosition:00}: {Trainer.TrainerName}, {(Species)TradeData.Species}";
        }
    }

    public enum Pictocodes
    {
        Pikachu,

        Eevee,

        Bulbasaur,

        Charmander,

        Squirtle,

        Pidgey,

        Caterpie,

        Rattata,

        Jigglypuff,

        Diglett
    }
}
