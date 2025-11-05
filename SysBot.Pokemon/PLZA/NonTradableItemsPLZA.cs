using PKHeX.Core;
using System;
using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public static class NonTradableItemsPLZA
    {
        // Names must match PKHeX's English item names (case-insensitive compare below)
        private static readonly HashSet<string> BlockedItemNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Gengarite",
            "Gardevoirite",
            "Ampharosite",
            "Venusaurite",
            "Charizardite X",
            "Blastoisinite",
            "Mewtwonite X",
            "Mewtwonite Y",
            "Blazikenite",
            "Medichamite",
            "Houndoominite",
            "Aggronite",
            "Banettite",
            "Tyranitarite",
            "Scizorite",
            "Pinsirite",
            "Aerodactylite",
            "Lucarionite",
            "Abomasite",
            "Kangaskhanite",
            "Gyaradosite",
            "Absolite",
            "Charizardite Y",
            "Alakazite",
            "Heracronite",
            "Mawilite",
            "Manectite",
            "Garchompite",
            "Latiasite",
            "Latiosite",
            "Swampertite",
            "Sceptilite",
            "Sablenite",
            "Altarianite",
            "Galladite",
            "Audinite",
            "Metagrossite",
            "Sharpedonite",
            "Slowbronite",
            "Steelixite",
            "Pidgeotite",
            "Glalitite",
            "Diancite",
            "Cameruptite",
            "Lopunnite",
            "Salamencite",
            "Beedrillite",
            "Clefablite",
            "Victreebelite",
            "Starminite",
            "Dragoninite",
            "Meganiumite",
            "Feraligite",
            "Skarmorite",
            "Froslassite",
            "Emboarite",
            "Excadrite",
            "Scolipite",
            "Scraftinite",
            "Eelektrossite",
            "Chandelurite",
            "Chesnaughtite",
            "Delphoxite",
            "Greninjite",
            "Pyroarite",
            "Floettite",
            "Malamarite",
            "Barbaracite",
            "Dragalgite",
            "Hawluchanite",
            "Zygardite",
            "Drampanite",
            "Falinksite",
            "Raichunite X",
            "Raichunite Y",
            "Shiny Charm",
            "Zygarde Cube",
            "Autographed Plush",
            "Blue Canari Plush Lv. 1",
            "Blue Canari Plush Lv. 2",
            "Blue Canari Plush Lv. 3",
            "Cherished Ring",
            "Elevator Key",
            "Gold Canari Plush Lv. 1",
            "Gold Canari Plush Lv. 2",
            "Gold Canari Plush Lv. 3",
            "Green Canari Plush Lv. 1",
            "Green Canari Plush Lv. 2",
            "Green Canari Plush Lv. 3",
            "Key to Room 202",
            "Lab Key Card A",
            "Lab Key Card B",
            "Lab Key Card C",
            "Lida’s Things",
            "Mega Ring",
            "Pebble",
            "Pink Canari Plush Lv. 1",
            "Pink Canari Plush Lv. 2",
            "Pink Canari Plush Lv. 3",
            "Red Canari Plush Lv. 1",
            "Red Canari Plush Lv. 2",
            "Red Canari Plush Lv. 3",
            "Revitalizing Twig",
            "Super Lumiose Galette",
            "Tasty Trash"
        };

        public static bool IsBlocked(PKM pkm)
        {
            var held = pkm.HeldItem;
            if (held <= 0)
                return false;

            var names = GameInfo.GetStrings("en");
            if (held >= 0 && held < names.Item.Count)
            {
                var itemName = names.Item[held];
                return BlockedItemNames.Contains(itemName);
            }

            return false;
        }

        public static bool IsPLZAMode<TPoke>(PokeTradeHub<TPoke> hub) where TPoke : PKM, new()
        {
            // Detect PLZA based on the generic type (used by hub runner)
            return typeof(TPoke) == typeof(PA9);
        }
    }
}


