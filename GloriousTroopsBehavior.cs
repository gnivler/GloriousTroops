using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;
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
            {
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                foreach (var orphan in reallyOrphaned)
                {
                    var parties = FindParties(orphan);
                    foreach (var party in parties)
                    {
                        var number = party.MemberRoster.GetTroopRoster().WhereQ(e => e.Character == orphan).SelectQ(x => x.Number).FirstOrDefault();
                        Log.Debug?.Log($"Orphaned {orphan.Name} {orphan.StringId} {number}");
                    }
                }
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving && SubModule.MEOWMEOW)
                CheckTracking(out _, true);
            if (!dataStore.SyncData("Troops", ref Troops))
                Troops.Clear();
            if (!dataStore.SyncData("EquipmentMap", ref EquipmentMap))
                EquipmentMap.Clear();
            if (!dataStore.SyncData("SkillsMap", ref SkillsMap))
                SkillsMap.Clear();
            if (!dataStore.SyncData("TroopKills", ref TroopKills))
                SkillsMap.Clear();
            if (!dataStore.SyncData("KillCounters", ref KillCounters))
                SkillsMap.Clear();
            if (dataStore.IsLoading)
            {
                var tempList = new List<CharacterObject>(Troops);
                Troops.Clear();
                foreach (var troop in tempList)
                    RehydrateCharacterObject(troop);
                Log.Debug?.Log($"Rehydrated {tempList.Count} troops");
                try
                {
                    CheckTracking(out _, true);
                    OnDailyTick();
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }
            }
        }

        private static void RehydrateCharacterObject(CharacterObject troop)
        {
            try
            {
                troop.InitializeHeroCharacterOnAfterLoad();
                troop.Level = troop.OriginalCharacter.Level;
                EquipmentUpgrading.CharacterSkills(troop) = new MBCharacterSkills();
                // for older save compatibility
                if (SkillsMap.TryGetValue(troop.StringId, out var skills))
                    Skills(EquipmentUpgrading.CharacterSkills(troop)) = skills;
                else
                {
                    EquipmentUpgrading.CharacterSkills(troop) = new MBCharacterSkills();
                    EquipmentUpgrading.Attributes(EquipmentUpgrading.CharacterSkills(troop).Skills) =
                        new Dictionary<SkillObject, int>(EquipmentUpgrading.Attributes(EquipmentUpgrading.CharacterSkills(troop.OriginalCharacter).Skills));
                }

                var mbEquipmentRoster = new MBEquipmentRoster();
                Equipments(mbEquipmentRoster) = new List<Equipment> { EquipmentMap[troop.StringId] };
                EquipmentRoster(troop) = mbEquipmentRoster;
                troop.Age = troop.OriginalCharacter.Age;
                BasicName(troop) = new TextObject(@"{=GTTroop}Glorious {TROOP}").SetTextVariable("TROOP", troop.Name);
                HiddenInEncyclopedia(troop) = true;
                MBObjectManager.Instance.RegisterObject(troop);
                Troops.Add(troop);
                //Log.Debug?.Log($"Rehydrated {troop.Name} {troop.StringId}");
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }
    }
}
