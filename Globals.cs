using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

// ReSharper disable InconsistentNaming

namespace UniqueTroopsGoneWild
{
    public struct Globals
    {
        // object tracking
        internal static List<CharacterObject> Troops = new();
        internal static readonly Dictionary<PartyBase, List<EquipmentElement>> LootRecord = new();
        internal static Dictionary<string, Equipment> EquipmentMap = new();

        // misc
        internal static DeferringLogger Log = new();
        internal static Settings Settings;
        internal static Random Rng = new();
        internal static readonly Stopwatch T = new();
    }
}
