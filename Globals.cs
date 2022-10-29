using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;

// ReSharper disable InconsistentNaming

namespace GloriousTroops
{
    public struct Globals
    {
        // object tracking
        internal static List<CharacterObject> Troops = new();
        internal static readonly Dictionary<PartyBase, List<EquipmentElement>> LootRecord = new();
        internal static Dictionary<PartyBase, FlattenedTroopRoster> TroopKills = new();
        internal static Dictionary<string, int> KillCounters = new();
        internal static Dictionary<string, Equipment> EquipmentMap = new();
        internal static Dictionary<string, CharacterSkills> SkillsMap = new();

        // misc
        internal static DeferringLogger Log = new(Path.Combine(ModuleHelper.GetModuleFullPath("GloriousTroops"), "log.txt"));
        internal static Settings Settings;
        internal static Random Rng = new();
        internal static readonly Stopwatch T = new();
    }
}
