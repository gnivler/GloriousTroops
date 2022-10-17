using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using static GloriousTroops.Globals;
using static GloriousTroops.Helper;


namespace GloriousTroops
{
    public class GloriousTroopsBehavior : CampaignBehaviorBase
    {
        private static readonly AccessTools.FieldRef<BasicCharacterObject, TextObject> BasicName =
            AccessTools.FieldRefAccess<BasicCharacterObject, TextObject>("_basicName");

        internal static readonly AccessTools.FieldRef<MBCharacterSkills, CharacterSkills> Skills =
            AccessTools.FieldRefAccess<MBCharacterSkills, CharacterSkills>("<Skills>k__BackingField");

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnPrisonerSoldEvent.AddNonSerializedListener(this, OnPrisonerSold);
        }

        private void OnPrisonerSold(MobileParty sellerParty, TroopRoster prisoners, Settlement settlement)
        {
            foreach (var troop in prisoners.GetTroopRoster())
                RemoveTracking(troop.Character, prisoners);
        }

        public static void OnDailyTick()
        {
            if (Environment.MachineName != "MEOWMEOW")
                return;
            var reallyOrphaned = CheckTracking(out var orphans, false);
            if (orphans.Count != 0 || reallyOrphaned.Count != 0)
                Debugger.Break();
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving) // && SubModule.MEOWMEOW)
            {
                CheckTracking(out _, true);
            }

            if (!dataStore.SyncData("Troops", ref Troops))
                Troops.Clear();
            if (!dataStore.SyncData("EquipmentMap", ref EquipmentMap))
                EquipmentMap.Clear();
            if (!dataStore.SyncData("SkillsMap", ref SkillsMap))
                SkillsMap.Clear();
            if (dataStore.IsLoading)
            {
                var tempList = new List<CharacterObject>(Troops);
                Troops.Clear();
                foreach (var troop in tempList)
                    RehydrateCharacterObject(troop);
                Log.Debug?.Log($"Rehydrated {tempList.Count} troops");
                OnDailyTick();
            }
        }

        private static void RehydrateCharacterObject(CharacterObject troop)
        {
            try
            {
                if (troop.OriginalCharacter is null)
                    return;
                troop.InitializeHeroCharacterOnAfterLoad();
                troop.Level = troop.OriginalCharacter.Level;
                Skills(EquipmentUpgrading.CharacterSkills(troop)) = SkillsMap[troop.StringId];
                var mbEquipmentRoster = new MBEquipmentRoster();
                Equipments(mbEquipmentRoster) = new List<Equipment> { EquipmentMap[troop.StringId] };
                EquipmentRoster(troop) = mbEquipmentRoster;
                troop.Age = troop.OriginalCharacter.Age;
                BasicName(troop) = new TextObject(@"{=GTTroop}Glorious {TROOP}").SetTextVariable("TROOP", troop.Name);
                HiddenInEncyclopedia(troop) = true;
                MBObjectManager.Instance.RegisterObject(troop);
                Troops.Add(troop);
                if (troop.UpgradeTargets is null)
                    Debugger.Break();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }
    }
}
