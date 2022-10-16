using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
using static GloriousTroops.Globals;

// ReSharper disable InconsistentNaming  

namespace GloriousTroops
{
    public static class Helper
    {
        internal static readonly AccessTools.FieldRef<BasicCharacterObject, MBEquipmentRoster> EquipmentRoster =
            AccessTools.FieldRefAccess<BasicCharacterObject, MBEquipmentRoster>("_equipmentRoster");

        internal static readonly AccessTools.FieldRef<MBEquipmentRoster, List<Equipment>> Equipments =
            AccessTools.FieldRefAccess<MBEquipmentRoster, List<Equipment>>("_equipments");

        // ReSharper disable once StringLiteralTypo
        internal static readonly AccessTools.FieldRef<CharacterObject, bool> HiddenInEncyclopedia =
            AccessTools.FieldRefAccess<CharacterObject, bool>("<HiddenInEncylopedia>k__BackingField");

        internal static PartyVM PartyViewModel => (PartyVM)SubModule.dataSource.GetValue(ScreenManager.TopScreen as GauntletPartyScreen);

        internal static void Restore()
        {
            try
            {
                Log.Debug?.Log("Restoring all original troops, this might take a minute...");
                RestorePatches(true);
                if (ScreenManager.TopScreen is GauntletPartyScreen)
                    PartyViewModel.ExecuteCancel();
                if (Settlement.CurrentSettlement is not null)
                    GameMenu.ExitToLast();
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                LootRecord.Clear();
                EquipmentMap.Clear();
                SkillsMap.Clear();
                RestoreAllOriginalTroops();
                Troops.Clear();
                MBInformationManager.AddQuickInformation(new TextObject("All original troops restored."));
                RestorePatches(false);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        private static void RestorePatches(bool apply)
        {
            var methodInfos = new[]
            {
                AccessTools.Method(typeof(WarPartyComponent), "OnFinalize"),
            };

            var finalizer = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.Finalizer));
            foreach (var method in methodInfos)
                if (apply)
                    SubModule.harmony.Patch(method, finalizer: new HarmonyMethod(finalizer));
                else
                    SubModule.harmony.Unpatch(method, finalizer);
        }

        internal static void RemoveTracking(CharacterObject troop, TroopRoster troopRoster)
        {
            if (troop.OriginalCharacter is null)
                return;
            Troops.Remove(troop);
            EquipmentMap.Remove(troop.StringId);
            MBObjectManager.Instance.UnregisterObject(troop);
            EquipmentUpgrading.CharacterSkills(troop) = null;
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
            CheckTracking(out _, true);
        }

        public static List<CharacterObject> CheckTracking(out List<CharacterObject> orphaned, bool prune)
        {
            var allGloriousTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().WhereQ(c => c is not null && !c.IsHero && c.OriginalCharacter is not null).ToListQ();
            var allRosters = MobileParty.All.SelectQ(m => m.MemberRoster).Concat(MobileParty.All.SelectQ(m => m.PrisonRoster)
                .Concat(Settlement.All.SelectQ(s => s.Party.MemberRoster).Concat(Settlement.All.SelectQ(s => s.Party.PrisonRoster)))).ToListQ();
            var enumeratedGloriousTroops = allRosters.SelectMany(r => r.ToFlattenedRoster().Troops).WhereQ(c => !c.IsHero && c.OriginalCharacter is not null).ToListQ();
            orphaned = allGloriousTroops.Except(Globals.Troops).ToListQ();
            var reallyOrphaned = enumeratedGloriousTroops.Except(Troops).ToListQ();
            var headless = Troops.Except(allGloriousTroops).ToListQ();
            foreach (var troop in orphaned)
                Log.Debug?.Log($"Orphaned: {troop.Name} {troop.StringId}");
            foreach (var troop in reallyOrphaned)
                Log.Debug?.Log($"Really orphaned: {troop.Name} {troop.StringId}");

            Log.Debug?.Log($"Found {orphaned.Count} orphaned troops out of {allGloriousTroops.CountQ()}");
            Log.Debug?.Log($"Found {reallyOrphaned.Count} really orphaned troops out of {allRosters.SumQ(r => r.TotalRegulars)}");
            Log.Debug?.Log($"Found {headless.Count} headless troops out of {allGloriousTroops.CountQ()}");

            if (prune && orphaned.Concat(reallyOrphaned).ToListQ() is var toPrune && toPrune.Any())
            {
                Log.Debug?.Log($"Pruning {toPrune.Count} orphaned troops");
                for (var index = 0; index < allRosters.Count; index++)
                {
                    var roster = allRosters[index];
                    foreach (var troop in roster.ToFlattenedRoster())
                    {
                        if (toPrune.ContainsQ(troop.Troop))
                        {
                            var ownerParty = (PartyBase)AccessTools.Field(typeof(TroopRoster), "<OwnerParty>k__BackingField").GetValue(roster);
                            roster.RemoveTroop(troop.Troop);
                            if (ownerParty.MapEvent is null)
                            {
                                Log.Debug?.Log($"Restored {troop.Troop.OriginalCharacter.Name} from {troop.Troop.StringId} in {ownerParty.Name}");
                                roster.AddToCounts(troop.Troop.OriginalCharacter, 1);
                            }
                            else
                                Log.Debug?.Log($"Pruned {troop.Troop.Name} {troop.Troop.StringId} from {ownerParty.Name}");
                        }
                    }
                }
            }

            return reallyOrphaned;
        }


        // troops with missing data causing lots of NREs elsewhere
        private static void RestoreAllOriginalTroops()
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
                while (rosters.AnyQ(r => r.GetTroopRoster().AnyQ(t => t.Character is not null && !t.Character.IsHero && t.Character.OriginalCharacter is not null)))
                {
                    try
                    {
                        foreach (var roster in rosters)
                        {
                            for (var index2 = 0; index2 < roster.GetTroopRoster().CountQ(); index2++)
                            {
                                var troop = roster.GetTroopRoster()[index2];
                                if (!troop.Character.IsHero && (troop.Character.OriginalCharacter is not null || troop.Character.Name == null))
                                {
                                    RemoveTracking(troop.Character, roster);
                                    try
                                    {
                                        roster.RemoveTroop(troop.Character);
                                    }
                                    catch (Exception ex)
                                    {
                                        roster.RemoveZeroCounts();
                                        LogException(ex);
                                    }

                                    if (party.MapEvent is null)
                                    {
                                        roster.AddToCounts(CharacterObject.Find(troop.Character.OriginalCharacter.StringId), 1);
                                        Log.Debug?.Log($"!!!!! Restored original troop {troop.Character.OriginalCharacter} from {troop.Character.StringId} in {party.Name}.");
                                    }
                                    else
                                    {
                                        party.MapEvent.FinalizeEvent();
                                        Traverse.Create(party.MapEventSide).Field<MapEvent>("_mapEvent").Value = null;
                                        roster.AddToCounts(CharacterObject.Find(troop.Character.OriginalCharacter.StringId), 1);
                                        Log.Debug?.Log($"!!!!! Battle-Restored Glorious Troop {troop.Character.OriginalCharacter} in {party.Name}.");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogException(ex);
                    }
                }
            }


            var allCOs = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().WhereQ(c => c is not null && !c.IsHero && c.OriginalCharacter is not null);
            while (allCOs is not null && allCOs.Any())
            {
                foreach (var troop in allCOs)
                    MBObjectManager.Instance.UnregisterObject(troop);
                allCOs = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().WhereQ(c => c is not null && !c.IsHero && c.OriginalCharacter is not null);
            }
        }

        internal static int FindIndexOrSimilarIndex(TroopRoster troopRoster, CharacterObject characterObject, bool woundedFirst = true)
        {
            int index;
            var troops = troopRoster.GetTroopRoster().WhereQ(e => e.Character.Name.Equals(characterObject.Name)).ToListQ();
            if (!woundedFirst)
            {
                var co = troops.FirstOrDefaultQ(e => e.WoundedNumber == 0 && e.Number > 0).Character;
                index = troopRoster.FindIndexOfTroop(co);
            }
            else // wounded or empty troops
            {
                var co = troops.FirstOrDefaultQ(e => e.WoundedNumber > 0).Character;
                index = troopRoster.FindIndexOfTroop(co);
            }

            if (index == -1)
            {
                var sameName = troops.FirstOrDefault().Character;
                return troopRoster.FindIndexOfTroop(sameName);
            }

            return index;
        }

        internal static void OnFocusCharacter(PartyCharacterVM partyCharacterVm)
        {
            AccessTools.Method(typeof(PartyVM), "OnFocusCharacter").Invoke(PartyViewModel, new object[] { partyCharacterVm });
        }

        internal static void SetSelectedCharacter(PartyCharacterVM partyCharacterVm)
        {
            AccessTools.Method(typeof(PartyVM), "SetSelectedCharacter").Invoke(PartyViewModel, new object[] { partyCharacterVm });
        }

        internal static void ProcessCharacterLock(PartyCharacterVM partyCharacterVm, bool b)
        {
            AccessTools.Method(typeof(PartyVM), "ProcessCharacterLock").Invoke(PartyViewModel, new object[] { partyCharacterVm, b });
        }

        internal static void OnTransferTroop(PartyCharacterVM partyCharacterVm, int i, int arg3, PartyScreenLogic.PartyRosterSide arg4)
        {
            AccessTools.Method(typeof(PartyVM), "OnTransferTroop").Invoke(PartyViewModel, new object[] { partyCharacterVm, i, arg3, arg4 });
        }

        internal static void ResetPartyCharacterVm(PartyCharacterVM __instance, PartyVM partyVm = null)
        {
            if (__instance.Character.IsHero || __instance.Character.OriginalCharacter is null)
                return;
            // limited context so check both rosters
            var isPrisoner = __instance.IsPrisoner;
            var roster = isPrisoner
                ? partyVm!.PartyScreenLogic.PrisonerRosters[(int)__instance.Side]
                : partyVm!.PartyScreenLogic.MemberRosters[(int)__instance.Side];
            var index = FindIndexOrSimilarIndex(roster, __instance.Character);
            if (index == -1)
                return;

            var co = roster.GetElementCopyAtIndex(index);
            var element = co.GetNewAggregateTroopRosterElement(roster).GetValueOrDefault();
            if (element.Character is not null)
            {
                __instance.Troop = element;
                __instance.Character = co.Character;
                __instance.TroopID = co.Character.StringId;
                Traverse.Create(partyVm).Method("SetSelectedCharacter", __instance).GetValue();
            }
        }

        internal static int WoundedFirst(PartyScreenLogic.PartyCommand command) => command.WoundedNumber > 0 ? 1 : 0;
        internal static int GetSimilarElementXp(TroopRoster roster, int index) => roster.GetElementCopyAtIndex(index).GetNewAggregateTroopRosterElement(roster).GetValueOrDefault().Xp;

        internal static void LogException(Exception ex)
        {
            TaleWorlds.Library.Debug.DebugManager.Print($"GloriousTroops exception\n{ex}");
            Log.Debug?.Log(ex);
        }
    }
}
