using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.MountAndBlade.ViewModelCollection.Scoreboard;
using static UniqueTroopsGoneWild.Globals;

// ReSharper disable CommentTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable RedundantAssignment
// ReSharper disable InconsistentNaming

namespace UniqueTroopsGoneWild
{
    public static class MiscPatches
    {
        [HarmonyPatch(typeof(BattleCampaignBehavior), "CollectLoots")]
        public static class BattleCampaignBehaviorCollectLoots
        {
            public static void Prefix(MapEvent mapEvent, PartyBase winnerParty)
            {
                if (!mapEvent.HasWinner || !winnerParty.IsMobile)
                    return;
                if (LootRecord.TryGetValue(winnerParty, out var equipment))
                {
                    var itemRoster = new ItemRoster();
                    foreach (var e in equipment)
                        itemRoster.AddToCounts(e, 1);
                    EquipmentUpgrading.UpgradeEquipment(winnerParty, itemRoster);
                }

                var parties = mapEvent.InvolvedParties;
                foreach (var party in parties)
                    LootRecord.Remove(party);
            }
        }

        // kinda anti-DRY here but...
        public static bool UpdateTooltipArmyMemberReplacement(ref TroopRoster __result, Army ___army)
        {
            __result = TroopRoster.CreateDummyTroopRoster();
            Dictionary<string, Tuple<CharacterObject, int, int>> compactList = new();
            foreach (var leaderPartyAndAttachedParty in ___army.LeaderPartyAndAttachedParties)
            {
                foreach (var element in leaderPartyAndAttachedParty.MemberRoster.GetTroopRoster())
                {
                    var key = element.Character.Name.ToString();
                    if (compactList.TryGetValue(key, out var data))
                        compactList[key] = new Tuple<CharacterObject, int, int>(element.Character, data.Item2 + element.Number, data.Item3 + element.WoundedNumber);
                    else
                        compactList.Add(key, new Tuple<CharacterObject, int, int>(element.Character, element.Number, element.WoundedNumber));
                }
            }

            foreach (var troop in compactList)
                __result.AddToCounts(compactList[troop.Key].Item1, compactList[troop.Key].Item2, false, compactList[troop.Key].Item3);
            return false;
        }

        public static bool UpdateTooltipArmyPrisonerReplacement(ref TroopRoster __result, Army ___army)
        {
            __result = TroopRoster.CreateDummyTroopRoster();
            Dictionary<string, Tuple<CharacterObject, int, int>> compactList = new();
            foreach (var leaderPartyAndAttachedParty in ___army.LeaderPartyAndAttachedParties)
            {
                foreach (var element in leaderPartyAndAttachedParty.PrisonRoster.GetTroopRoster())
                {
                    var key = element.Character.Name.ToString();
                    if (compactList.TryGetValue(key, out var data))
                        compactList[key] = new Tuple<CharacterObject, int, int>(element.Character, data.Item2 + element.Number, data.Item3 + element.WoundedNumber);
                    else
                        compactList.Add(key, new Tuple<CharacterObject, int, int>(element.Character, element.Number, element.WoundedNumber));
                }
            }

            foreach (var troop in compactList)
                __result.AddToCounts(compactList[troop.Key].Item1, compactList[troop.Key].Item2, false, compactList[troop.Key].Item3);
            return false;
        }

        public static bool UpdateTooltipSettlementMemberReplacement(ref TroopRoster __result, Settlement ___settlement)
        {
            Dictionary<string, Tuple<CharacterObject, int, int>> compactList = new();
            __result = TroopRoster.CreateDummyTroopRoster();
            foreach (var party in ___settlement.Parties)
            {
                if ((!(party.Aggressiveness < 0.01f) || party.IsGarrison || party.IsMilitia) && !party.IsMainParty)
                {
                    foreach (var element in party.MemberRoster.GetTroopRoster())
                    {
                        try
                        {
                            var key = element.Character.Name.ToString();
                            if (compactList.TryGetValue(key, out var data))
                                compactList[key] = new Tuple<CharacterObject, int, int>(element.Character, data.Item2 + element.Number, data.Item3 + element.WoundedNumber);
                            else
                                compactList.Add(key, new Tuple<CharacterObject, int, int>(element.Character, element.Number, element.WoundedNumber));
                        }
                        catch (Exception ex)
                        {
                            Log.Debug?.Log(ex);
                        }
                    }
                }
            }

            foreach (var troop in compactList)
                __result.AddToCounts(compactList[troop.Key].Item1, compactList[troop.Key].Item2, false, compactList[troop.Key].Item3);
            return false;
        }

        public static bool UpdateTooltipSettlementPrisonerReplacement(ref TroopRoster __result, Settlement ___settlement, PartyBase ___settlementAsParty)
        {
            var troopRoster = TroopRoster.CreateDummyTroopRoster();
            Dictionary<string, Tuple<CharacterObject, int, int>> compactList = new();
            __result = TroopRoster.CreateDummyTroopRoster();
            foreach (var party in ___settlement.Parties)
            {
                if (!party.IsMainParty && !FactionManager.IsAtWarAgainstFaction(party.MapFaction, ___settlementAsParty.MapFaction))
                {
                    foreach (var element in party.PrisonRoster.GetTroopRoster())
                    {
                        try
                        {
                            var key = element.Character.Name.ToString();
                            if (compactList.TryGetValue(key, out var data))
                                compactList[key] = new Tuple<CharacterObject, int, int>(element.Character, data.Item2 + element.Number, data.Item3 + element.WoundedNumber);
                            else
                                compactList.Add(key, new Tuple<CharacterObject, int, int>(element.Character, element.Number, element.WoundedNumber));
                        }
                        catch (Exception ex)
                        {
                            Log.Debug?.Log(ex);
                        }
                    }
                }
            }

            for (var j = 0; j < ___settlementAsParty.PrisonRoster.Count; j++)
            {
                var elementCopyAtIndex2 = ___settlementAsParty.PrisonRoster.GetElementCopyAtIndex(j);
                troopRoster.AddToCounts(elementCopyAtIndex2.Character, elementCopyAtIndex2.Number, insertAtFront: false, elementCopyAtIndex2.WoundedNumber);
            }

            foreach (var troop in compactList)
                __result.AddToCounts(compactList[troop.Key].Item1, compactList[troop.Key].Item2, false, compactList[troop.Key].Item3);
            return false;
        }

        // aggregate the tooltip to show only one element per upgraded CO type
        public static bool UpdateTooltipPartyMemberReplacement(ref TroopRoster __result, MobileParty ___mobileParty)
        {
            __result = TroopRoster.CreateDummyTroopRoster();
            Dictionary<string, Tuple<CharacterObject, int, int>> compactList = new();
            foreach (var element in ___mobileParty.MemberRoster.GetTroopRoster())
            {
                var key = element.Character.Name.ToString();
                if (compactList.TryGetValue(key, out var data))
                    compactList[key] = new Tuple<CharacterObject, int, int>(element.Character, data.Item2 + element.Number, data.Item3 + element.WoundedNumber);
                else
                    compactList.Add(key, new Tuple<CharacterObject, int, int>(element.Character, element.Number, element.WoundedNumber));
            }

            foreach (var troop in compactList)
                __result.AddToCounts(compactList[troop.Key].Item1, compactList[troop.Key].Item2, false, compactList[troop.Key].Item3);
            return false;
        }

        // identical copy :(
        // aggregate the tooltip to show only one element per upgraded CO type
        public static bool UpdateTooltipPartyPrisonerReplacement(ref TroopRoster __result, MobileParty ___mobileParty)
        {
            __result = TroopRoster.CreateDummyTroopRoster();
            Dictionary<string, Tuple<CharacterObject, int, int>> compactList = new();
            foreach (var element in ___mobileParty.PrisonRoster.GetTroopRoster())
            {
                var key = element.Character.Name.ToString();
                if (compactList.TryGetValue(key, out var data))
                    compactList[key] = new Tuple<CharacterObject, int, int>(element.Character, data.Item2 + element.Number, data.Item3 + element.WoundedNumber);
                else
                    compactList.Add(key, new Tuple<CharacterObject, int, int>(element.Character, element.Number, element.WoundedNumber));
            }

            foreach (var troop in compactList)
                __result.AddToCounts(compactList[troop.Key].Item1, compactList[troop.Key].Item2, false, compactList[troop.Key].Item3);
            return false;
        }

        // aggregate the scoreboard to show only one element per upgraded CO type
        [HarmonyPatch(typeof(SPScoreboardPartyVM), "GetUnitAddIfNotExists")]
        public static class SPScoreboardPartyVMGetUnitAddIfNotExists
        {
            public static bool Prefix(SPScoreboardPartyVM __instance, BasicCharacterObject character,
                SPScoreboardStatsVM ____score, MBBindingList<SPScoreboardUnitVM> ____members, ref SPScoreboardUnitVM __result)
            {
                if (character == Game.Current.PlayerTroop)
                    ____score.IsMainParty = true;

                // matches any eg: Upgraded Looter
                var sPScoreboardUnitVM = ____members.FirstOrDefault(p => p.Character.Name.Equals(character.Name));
                if (sPScoreboardUnitVM == null)
                {
                    sPScoreboardUnitVM = new SPScoreboardUnitVM(character);
                    ____members.Add(sPScoreboardUnitVM);
                }
                // leveraging the fact that the MemberRoster doesn't change during combat, so we can use it as a gate
                // meaning that everything has been initially setup, and stop aggregating while this method is called after that
                else if (Troops.ContainsQ(character)
                         && ((PartyBase)__instance.BattleCombatant).MemberRoster.GetTroopCount((CharacterObject)character) >
                         sPScoreboardUnitVM.Score.Dead + sPScoreboardUnitVM.Score.Wounded + sPScoreboardUnitVM.Score.Routed + sPScoreboardUnitVM.Score.Remaining)
                {
                    // Members already has this unitVM so we just change it
                    sPScoreboardUnitVM.Score.Remaining++;
                }

                ____members.Sort(new SPScoreboardSortControllerVM.ItemMemberComparer());
                __result = sPScoreboardUnitVM;
                return false;
            }
        }

        // compact the party screen display
        [HarmonyPatch(typeof(PartyVM), "InitializePartyList")]
        public static class PartyVMInitializePartyList
        {
            private static bool Prefix(PartyVM __instance,
                MBBindingList<PartyCharacterVM> partyList,
                TroopRoster currentTroopRoster,
                PartyScreenLogic.TroopType type,
                int side,
                string ____fiveStackShortcutkeyText,
                string ____entireStackShortcutkeyText)
            {
                partyList.Clear();
                var map = new Dictionary<string, PartyCharacterVM>();
                for (var index = 0; index < currentTroopRoster.Count; ++index)
                {
                    var elementCopyAtIndex = currentTroopRoster.GetElementCopyAtIndex(index);
                    if (!map.TryGetValue(elementCopyAtIndex.Character.Name.ToString(), out var partyCharacterVm))
                    {
                        partyCharacterVm = new PartyCharacterVM(__instance.PartyScreenLogic, ProcessCharacterLock, SetSelectedCharacter, OnTransferTroop, null, OnFocusCharacter, __instance, currentTroopRoster, index, type, (PartyScreenLogic.PartyRosterSide)side, __instance.PartyScreenLogic.IsTroopTransferable(type, elementCopyAtIndex.Character, side), ____fiveStackShortcutkeyText, ____entireStackShortcutkeyText);
                        map.Add(elementCopyAtIndex.Character.Name.ToString(), partyCharacterVm);
                        partyList.Add(partyCharacterVm);
                    }
                    else
                    {
                        elementCopyAtIndex.Number += partyCharacterVm.Number;
                        partyCharacterVm.Troop = elementCopyAtIndex;
                    }

                    partyCharacterVm.ThrowOnPropertyChanged();
                    partyCharacterVm.IsLocked = partyCharacterVm.Side == PartyScreenLogic.PartyRosterSide.Right && (bool)Traverse.Create(__instance).Method("IsTroopLocked", partyCharacterVm.Troop, partyCharacterVm.IsPrisoner).GetValue();
                }

                return false;

                void OnFocusCharacter(PartyCharacterVM partyCharacterVm)
                {
                    AccessTools.Method(typeof(PartyVM), "OnFocusCharacter").Invoke(__instance, new object[] { partyCharacterVm });
                }

                void SetSelectedCharacter(PartyCharacterVM partyCharacterVm)
                {
                    AccessTools.Method(typeof(PartyVM), "SetSelectedCharacter").Invoke(__instance, new object[] { partyCharacterVm });
                }

                void ProcessCharacterLock(PartyCharacterVM partyCharacterVm, bool b)
                {
                    AccessTools.Method(typeof(PartyVM), "ProcessCharacterLock").Invoke(__instance, new object[] { partyCharacterVm, b });
                }

                void OnTransferTroop(PartyCharacterVM partyCharacterVm, int i, int arg3, PartyScreenLogic.PartyRosterSide arg4)
                {
                    AccessTools.Method(typeof(PartyVM), "OnTransferTroop").Invoke(__instance, new object[] { partyCharacterVm, i, arg3, arg4 });
                }
            }
        }

        // makes the PartyTradeVM (troop widgets) show the proper number of troops by patching its sole instantiation, in this ctor
        public class PartyCharacterVMConstructor
        {
            private static readonly ConstructorInfo from = AccessTools.FirstConstructor(typeof(PartyTradeVM), c => c.GetParameters().Length > 0);
            private static readonly MethodInfo helper = AccessTools.Method(typeof(PartyCharacterVMConstructor), nameof(TrickCtor));

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToListQ();
                int index = -1;
                for (var i = 0; i < codes.Count; i++)
                {
                    // new PartyTradeVM
                    if (codes[i].OperandIs(from))
                        index = i;
                }

                codes.Insert(index, new CodeInstruction(OpCodes.Ldarg_S, 8)); // TroopRoster
                codes[index + 1].opcode = OpCodes.Call;
                codes[index + 1].operand = helper;
                return codes.AsEnumerable();
            }

            private static PartyTradeVM TrickCtor(PartyScreenLogic partyScreenLogic, TroopRosterElement troopRoster,
                PartyScreenLogic.PartyRosterSide side, bool isTransfarable, bool isPrisoner,
                Action<int, bool> onApplyTransaction, TroopRoster roster)
            {
                var element = troopRoster.AllSimilar(roster).GetValueOrDefault();
                var sum = element.Number + element.WoundedNumber;
                troopRoster.Number = sum;
                return new PartyTradeVM(partyScreenLogic, troopRoster, side, isTransfarable, isPrisoner, onApplyTransaction);
            }
        }

        // this will fallback to find a similar unique troop (with the same Name)
        // it's a modified copy of the original
        [HarmonyPatch(typeof(TroopRoster), "FindIndexOfTroop")]
        public class TroopRosterFindIndexOfTroop
        {
            public static void Postfix(CharacterObject character, ref int __result, int ____count, TroopRosterElement[] ___data)
            {
                if (__result >= 0)
                    return;
                for (var indexOfTroop = 0; indexOfTroop < ____count; ++indexOfTroop)
                    if (___data[indexOfTroop].Character.Name.Equals(character.Name))
                    {
                        __result = indexOfTroop;
                        return;
                    }

                __result = -1;
            }
        }

        // facilitates the widgets updating the displayed numbers
        [HarmonyPatch(typeof(PartyTradeVM), "UpdateTroopData")]
        public class PartyTradeVMUpdateTroopData
        {
            public static void Prefix(PartyScreenLogic ____partyScreenLogic, bool ____isPrisoner, ref TroopRosterElement troopRoster, PartyScreenLogic.PartyRosterSide side)
            {
                var roster = ____isPrisoner ? ____partyScreenLogic.PrisonerRosters[(int)side] : ____partyScreenLogic.MemberRosters[(int)side];
                var element = troopRoster.AllSimilar(roster).GetValueOrDefault();
                troopRoster.Number = element.Number;
                troopRoster.WoundedNumber = element.WoundedNumber;
            }
        }

        [HarmonyPatch(typeof(SaveableCampaignTypeDefiner), "DefineContainerDefinitions")]
        public class SaveableCampaignTypeDefinerDefineContainerDefinitions
        {
            public static void Postfix(SaveableCampaignTypeDefiner __instance)
            {
                AccessTools.Method(typeof(CampaignBehaviorBase.SaveableCampaignBehaviorTypeDefiner),
                    "ConstructContainerDefinition").Invoke(__instance, new object[] { typeof(Dictionary<string, Equipment>) });
            }
        }

        [HarmonyPatch(typeof(TroopRoster), "ClampXp")]
        public static class TroopRosterClampXp
        {
            public static bool Prefix(int index, TroopRosterElement[] ___data)
            {
                // comes through without UpgradeTargets.  Faster than recreating.
                var character = ___data[index].Character;
                if (!character.IsHero)
                {
                    if (character.UpgradeTargets is null)
                        return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(DefaultPartyTroopUpgradeModel), "CanTroopGainXp")]
        public static class DefaultPartyTroopUpgradeModelCanTroopGainXp
        {
            public static bool Prefix(CharacterObject character, ref bool __result)
            {
                // comes through without UpgradeTargets.  Faster than recreating.
                if (character.UpgradeTargets is null)
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(DefaultPartyTroopUpgradeModel), "IsTroopUpgradeable")]
        public static class DefaultPartyTroopUpgradeModelIsTroopUpgradeable
        {
            public static bool Prefix(CharacterObject character, ref bool __result)
            {
                // comes through without UpgradeTargets.  Faster than recreating.
                if (character.UpgradeTargets is null)
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        // hideout crashes
        // the scoreboard rollup shows one but the game tries to remove each CO, crashing here
        // it doesn't matter if it's ignored
        [HarmonyPatch(typeof(SPScoreboardSideVM), "RemoveTroop")]
        public class SPScoreboardSideVMRemoveTroop
        {
            public static Exception Finalizer() => null;
        }
        
        [HarmonyPatch(typeof(SPScoreboardSideVM), "AddTroop")]
        public class SPScoreboardSideVMAddTroop
        {
            public static Exception Finalizer() => null;
        }

        [HarmonyPatch(typeof(SPScoreboardPartyVM), "AddUnit")]
        public class SPScoreboardPartyVMAddUnit
        {
            public static Exception Finalizer() => null;
        }

        internal static Exception Finalizer() => null;
    }
}
