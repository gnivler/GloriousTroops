using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using static UniqueTroopsGoneWild.Globals;
using static UniqueTroopsGoneWild.Helper;

// ReSharper disable InconsistentNaming

namespace UniqueTroopsGoneWild
{
    public class UniqueTroopsBehavior : CampaignBehaviorBase
    {
        private static readonly AccessTools.FieldRef<BasicCharacterObject, TextObject> basicName =
            AccessTools.FieldRefAccess<BasicCharacterObject, TextObject>("_basicName");

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public static void OnDailyTick()
        {
            if (Environment.MachineName != "MEOWMEOW")
                return;
            var reallyOrphaned = CheckTracking(out var orphans);
            if (orphans.Count != 0 || reallyOrphaned.Count != 0)
                Debugger.Break();
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving && Environment.MachineName == "MEOWMEOW")
            {
                CheckTracking(out var orphaned);
                if (orphaned.Count != 0)
                    Debugger.Break();
            }

            if (!dataStore.SyncData("Troops", ref Troops))
                Troops.Clear();
            if (!dataStore.SyncData("EquipmentMap", ref EquipmentMap))
                EquipmentMap.Clear();
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
                if (troop.OriginalCharacter == null)
                    return;
                //Log.Debug?.Log($"Rehydrating {troop.OriginalCharacter.Name} - {troop.StringId}");
                troop.InitializeHeroCharacterOnAfterLoad();
                basicName(troop) = new TextObject(@"{=BMTroops}Upgraded " + troop.OriginalCharacter.Name);
                HiddenInEncyclopedia(troop) = true;
                MBObjectManager.Instance.RegisterObject(troop);
                Troops.Add(troop);
                if (troop.UpgradeTargets is null)
                    Debugger.Break();
            }
            catch (Exception ex)
            {
                Log.Debug?.Log(ex);
            }
        }
    }
}
