using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.ObjectSystem;
using static UniqueTroopsGoneWild.Globals;

// ReSharper disable InconsistentNaming  

namespace UniqueTroopsGoneWild
{
    internal static class Helper
    {
        internal static readonly AccessTools.FieldRef<BasicCharacterObject, MBEquipmentRoster> EquipmentRoster =
            AccessTools.FieldRefAccess<BasicCharacterObject, MBEquipmentRoster>("_equipmentRoster");

        internal static readonly AccessTools.FieldRef<MBEquipmentRoster, List<Equipment>> Equipments =
            AccessTools.FieldRefAccess<MBEquipmentRoster, List<Equipment>>("_equipments");

        // ReSharper disable once StringLiteralTypo
        internal static readonly AccessTools.FieldRef<CharacterObject, bool> HiddenInEncyclopedia =
            AccessTools.FieldRefAccess<CharacterObject, bool>("<HiddenInEncylopedia>k__BackingField");

        internal static void Nuke()
        {
            try
            {
                Log.Debug?.Log("Destroying all upgraded troops, this might take a minute...");
                NukePatches(true);
                if (Settlement.CurrentSettlement == null)
                    GameMenu.ExitToLast();
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                LootRecord.Clear();
                EquipmentMap.Clear();
                DestroyAllUpgradedTroops();
                Troops.Clear();
                InformationManager.DisplayMessage(new InformationMessage("All unique troops destroyed."));
                NukePatches(false);
            }
            catch (Exception ex)
            {
                Log.Debug?.Log(ex);
            }
        }

        private static void NukePatches(bool apply)
        {
            var methodInfos = new[]
            {
                AccessTools.Method("WarPartyComponent:OnFinalize"),
            };

            var finalizer = AccessTools.Method("MiscPatches:Finalizer");
            foreach (var method in methodInfos)
                if (apply)
                    SubModule.harmony.Patch(method, finalizer: new HarmonyMethod(finalizer));
                else
                    SubModule.harmony.Unpatch(method, finalizer);
        }

        internal static void RemoveTracking(CharacterObject troop, TroopRoster troopRoster)
        {
            if (troop.OriginalCharacter is null)
                Debugger.Break();
            Troops.Remove(troop);
            EquipmentMap.Remove(troop.StringId);
            MBObjectManager.Instance.UnregisterObject(troop);
            //Log.Debug?.Log($"<<< Removed tracking {troop.Name} {troop.StringId}");
            if (troopRoster is not null)
            {
                var ownerParty = (PartyBase)AccessTools.Field(typeof(TroopRoster), "<OwnerParty>k__BackingField").GetValue(troopRoster);
                if (ownerParty is not null && ownerParty.IsMobile && ownerParty.MemberRoster.TotalManCount == 0)
                {
                    Log.Debug?.Log($"<<< Removing empty party {ownerParty.Name}");
                    DestroyPartyAction.Apply(null, ownerParty.MobileParty);
                }
            }

            //new StackTrace().GetFrames()?.Skip(1).Take(3).Do(f => Log.Debug?.Log(f.GetMethod().Name));
            //Log.Debug?.Log("\n");
        }

        public static void CheckTracking()
        {
            CheckTracking(out _);
        }

        public static List<CharacterObject> CheckTracking(out List<CharacterObject> orphaned)
        {
            var allUpgradedTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().WhereQ(c => !c.IsHero && c.OriginalCharacter is not null).ToListQ();
            var allRosters = MobileParty.All.SelectQ(m => m.MemberRoster).Concat(MobileParty.All.SelectQ(m => m.PrisonRoster)
                .Concat(Settlement.All.SelectQ(s => s.Party.MemberRoster).Concat(Settlement.All.SelectQ(s => s.Party.PrisonRoster)))).ToListQ();
            var enumeratedUpgradedTroops = allRosters.SelectMany(r => r.ToFlattenedRoster().Troops).WhereQ(c => !c.IsHero && c.OriginalCharacter is not null).ToListQ();
            orphaned = allUpgradedTroops.Except(Globals.Troops).ToListQ();
            var reallyOrphaned = enumeratedUpgradedTroops.Except(Troops).ToListQ();
            var headless = Troops.Except(allUpgradedTroops).ToListQ();
            foreach (var troop in orphaned)
                Log.Debug?.Log($"Orphaned: {troop.Name} {troop.StringId}");
            foreach (var troop in reallyOrphaned)
                Log.Debug?.Log($"Really orphaned: {troop.Name} {troop.StringId}");
            //foreach (var troop in headless)
            //{
            //    Log.Debug?.Log($"Removing headless: {troop.Name} {troop.StringId}");
            //    Troops.Remove(troop);
            //}

            Log.Debug?.Log($"Found {orphaned.Count} orphaned troops out of {allUpgradedTroops.CountQ()}");
            Log.Debug?.Log($"Found {reallyOrphaned.Count} really orphaned troops out of {allRosters.SumQ(r => r.TotalRegulars)}");
            Log.Debug?.Log($"Found {headless.Count} headless troops out of {allUpgradedTroops.CountQ()}");
            return reallyOrphaned;
        }

        // troops with missing data causing lots of NREs elsewhere
        private static void DestroyAllUpgradedTroops()
        {
            for (var index = 0; index < MobileParty.All.Count; index++)
            {
                var party = MobileParty.All.ToListQ()[index];
                IterateParties(party.Party);
            }

            for (var index2 = 0; index2 < Settlement.All.Count; index2++)
            {
                var party = Settlement.All.SelectQ(s => s.Party).ToListQ()[index2];
                IterateParties(party);
            }

            void IterateParties(PartyBase party)
            {
                var rosters = new[] { party.PrisonRoster, party.MemberRoster };
                while (rosters.AnyQ(r => r.GetTroopRoster().AnyQ(t => t.Character.Name == null || t.Character.Name.Contains("Upgraded"))))
                {
                    foreach (var roster in rosters)
                    {
                        for (var index2 = 0; index2 < roster.GetTroopRoster().CountQ(); index2++)
                        {
                            var troop = roster.GetTroopRoster()[index2];
                            if (troop.Character.Name == null || troop.Character.Name.Contains("Upgraded"))
                            {
                                Log.Debug?.Log($"!!!!! Destroying upgraded troop {troop.Character.OriginalCharacter} in {party.Name}.");
                                roster.RemoveTroop(troop.Character);
                                RemoveTracking(troop.Character, roster);
                            }
                        }
                    }
                }
            }

            var allCOs = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().WhereQ(c => c.Name.Contains("Upgraded") || c.Name == null).ToListQ();
            while (allCOs.Any())
            {
                foreach (var troop in allCOs)
                    MBObjectManager.Instance.UnregisterObject(troop);
                allCOs = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().WhereQ(c => c.Name.Contains("Upgraded") || c.Name == null).ToListQ();
            }
        }
    }
}
