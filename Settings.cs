using System;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.InputSystem;


// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable ConvertToAutoProperty
// ReSharper disable InconsistentNaming    
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable PropertyCanBeMadeInitOnly.Local
// ReSharper disable UnusedMember.Global   
// ReSharper disable FieldCanBeMadeReadOnly.Global 
// ReSharper disable ConvertToConstant.Global

namespace GloriousTroops
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string FormatType => "json";
        public override string FolderName => "GloriousTroops";
        private const string id = "GloriousTroops";
        private string displayName = $"GloriousTroops {typeof(Settings).Assembly.GetName().Version.ToString(3)}";

        public override string Id => id;
        public override string DisplayName => displayName;

        // nice idea DasNeo, nice idea
        [SettingPropertyDropdown("Hotkey", HintText = "Select a hotkey to toggle the Glorious Troops UI.", Order = 0, RequireRestart = false)]
        public Dropdown<string> Hotkey { get; set; } = new(Enum.GetNames(typeof(InputKey)), 43 /* Z */);

        [SettingPropertyBool("Only Bandits", HintText = "Regular lord parties' troops will not upgrade their equipment.", Order = 1, RequireRestart = false)]
        public bool OnlyBandits { get; set; } = false;

        [SettingPropertyBool("Mounts", HintText = "Allow looting of mounts and saddles.", Order = 2, RequireRestart = false)]
        public bool Mounts { get; set; } = true;

        [SettingPropertyBool("Maintain Culture", HintText = "Troops won't loot equipment from other cultures.", Order = 3, RequireRestart = false)]
        public bool MaintainCulture { get; set; } = false;

        [SettingPropertyBool("Maintain Weapon Type", HintText = "Troops will only replace their main weapons with the same type (blunt/slashing/piercing).", Order = 4, RequireRestart = false)]
        public bool MaintainType { get; set; } = false;

        [SettingPropertyInteger("Drop Percent", 1, 100, HintText = "How likely each item is to become lootable.", Order = 5, RequireRestart = false)]
        public int DropPercent { get; set; } = 66;

        [SettingPropertyInteger("Minimum Loot Value", 1000, 100_000, HintText = "Only items at least this valuable will be kept for loot.", Order = 6, RequireRestart = false)]
        public int MinLootValue { get; set; } = 1000;

        [SettingPropertyBool("Party Screen Changes", HintText = "Compact the Party Screen UI widgets to show multiples (or singles).  Requires restart.", Order = 8, RequireRestart = true)]
        public bool PartyScreenChanges { get; set; } = true;

        [SettingPropertyBool("Debug Logging", HintText = "Log to mod folder, log.txt", Order = 9, RequireRestart = false)]
        public bool Debug { get; set; } = false;

        // [SettingPropertyBool("Save Recovery", HintText = "Removes excess CharacterSkill objects.  Save to NEW slot.  Then load.  Save again to see speed improvement.", Order = 8, RequireRestart = true)]
        // public bool SaveRecovery { get; set; } = false;
    }
}
