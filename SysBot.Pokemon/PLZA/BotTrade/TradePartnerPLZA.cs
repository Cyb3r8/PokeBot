using PKHeX.Core;
using System;
using System.Buffers.Binary;

namespace SysBot.Pokemon;

public sealed class TradePartnerPLZA(TradeMyStatusPLZA Info)
{
    public int Game { get; } = Info.Game;

    public int Gender { get; } = Info.Gender;

    public int Language { get; } = Info.Language;

    public string SID7 { get; } = Info.DisplaySID.ToString("D4");

    public string TID7 { get; } = Info.DisplayTID.ToString("D6");

    public string TrainerName { get; } = Info.OT;
}

// Trade session structure from trade partner (48 bytes)
// 0x00: ID32, 0x04: Gender, 0x05: Language, 0x08: OT Name
public sealed class TradeMyStatusPLZA
{
    public readonly byte[] Data = new byte[0x30];

    public uint DisplaySID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) / 1_000_000;

    public uint DisplayTID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) % 1_000_000;

    public int Game => 52; // PLZA game version

    public int Gender => Data[4];

    public int Language => Data[5];

    public string OT => StringConverter8.GetString(Data.AsSpan(0x08, 0x1A));
}
