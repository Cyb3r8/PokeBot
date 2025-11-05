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
// Based on PKHeX MyStatus9a structure:
// 0x00: ID32 (TID+SID), 0x04: Game, 0x05: Gender, 0x07: Language, 0x10: OT Name
public sealed class TradeMyStatusPLZA
{
    public readonly byte[] Data = new byte[0x30];

    public uint DisplaySID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) / 1_000_000;

    public uint DisplayTID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) % 1_000_000;

    public int Game => Data[0x04]; // Read from memory instead of hardcoding

    public int Gender => Data[0x05];

    public int Language => Data[0x07];

    public string OT => StringConverter8.GetString(Data.AsSpan(0x10, 0x1A));
}
