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
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.Scoreboard;
using static GloriousTroops.Globals;
using static GloriousTroops.Helper;
using Debug = TaleWorlds.Library.Debug;

// ReSharper disable CommentTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable RedundantAssignment
// ReSharper disable InconsistentNaming

namespace GloriousTroops
{
    public static class MiscPatches
    {
        private static readonly FieldInfo MemberRosters = AccessTools.Field(typeof(PartyScreenLogic), nameof(PartyScreenLogic.MemberRosters));

        [HarmonyPatch(typeof(BattleCampaignBehavior), "CollectLoots")]
        public static class BattleCampaignBehaviorCollectLoots
        {
            public static void Prefix(MapEvent mapEvent, PartyBase winnerParty)
            {
                if (!mapEvent.HasWinner || !winnerParty.IsMobile)
                    return;
                var loserParties = mapEvent.PartiesOnSide(winnerParty.OpponentSide);
                var itemRoster = new ItemRoster();
                foreach (var loserParty in loserParties)
                {
                    if (LootRecord.TryGetValue(loserParty.Party, out var equipment))
                    {
                        foreach (var e in equipment)
                            itemRoster.AddToCounts(e, 1);
                    }
                }

                EquipmentUpgrading.UpgradeEquipment(winnerParty, itemRoster);
                var parties = mapEvent.InvolvedParties;
                foreach (var party in parties)
                    LootRecord.Remove(party);
            }
        }

        #region Tooltips

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
                            LogException(ex);
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
                            LogException(ex);
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

                // matches any eg: Glorious Looter
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

        #endregion

        // compact the party screen display
        [HarmonyPatch(typeof(PartyVM), "InitializePartyList")]
        public static class PartyVMInitializePartyList
        {
            private static bool Prefix(PartyVM __instance,
                ref MBBindingList<PartyCharacterVM> partyList,
                TroopRoster currentTroopRoster,
                PartyScreenLogic.TroopType type,
                int side,
                string ____fiveStackShortcutkeyText,
                string ____entireStackShortcutkeyText)
            {
                if (!Globals.Settings.PartyScreenChanges)
                    return true;
                partyList.Clear();
                try
                {
                    var added = new HashSet<string>();
                    for (var index = 0; index < currentTroopRoster.Count; ++index)
                    {
                        var elementCopyAtIndex = currentTroopRoster.GetElementCopyAtIndex(index);
                        var name = elementCopyAtIndex.Character.Name.ToString();
                        if (added.Add(name))
                        {
                            try
                            {
                                // ctor patch makes it show aggregate count
                                var partyCharacterVm = new PartyCharacterVM(__instance.PartyScreenLogic,
                                    ProcessCharacterLock, SetSelectedCharacter, OnTransferTroop, null, OnFocusCharacter,
                                    __instance, currentTroopRoster, index, type, (PartyScreenLogic.PartyRosterSide)side,
                                    __instance.PartyScreenLogic.IsTroopTransferable(type, elementCopyAtIndex.Character, side),
                                    ____fiveStackShortcutkeyText, ____entireStackShortcutkeyText);
                                partyList.Add(partyCharacterVm);
                                partyCharacterVm.ThrowOnPropertyChanged();
                                partyCharacterVm.IsLocked = partyCharacterVm.Side == PartyScreenLogic.PartyRosterSide.Right
                                                            && (bool)Traverse.Create(__instance).Method("IsTroopLocked", partyCharacterVm.Troop, partyCharacterVm.IsPrisoner).GetValue();
                            }
                            catch (Exception ex)
                            {
                                LogException(ex);
                            }
                        }
                    }

                    __instance.MainPartyTroops.Do(p => p.RefreshValues());
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }

                return false;
            }
        }

        // instantiate the PartyCharacterVM with the aggregate number
        public class PartyCharacterVMConstructor
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (!Globals.Settings.PartyScreenChanges)
                    return instructions;
                var codes = instructions.ToListQ();
                int index = default;
                for (var i = 0; i < codes.Count; i++)
                {
                    // this.Troop = troops.GetElementCopyAtIndex(index);
                    if (codes[i].opcode == OpCodes.Callvirt
                        && codes[i + 1].opcode == OpCodes.Call
                        && codes[i + 2].opcode == OpCodes.Ldarg_0
                        && codes[i + 3].opcode == OpCodes.Ldarg_S
                        && codes[i + 4].opcode == OpCodes.Call
                        && codes[i + 5].opcode == OpCodes.Ldarg_0
                        && codes[i + 6].opcode == OpCodes.Ldarg_0)
                    {
                        index = i + 1;
                        break;
                    }
                }

                var stack = new List<CodeInstruction>
                {
                    new(OpCodes.Ldarg_S, 8),
                    new(OpCodes.Call, AccessTools.Method(typeof(PartyCharacterVMConstructor), nameof(GetAggregatedElement))),
                };
                codes.InsertRange(index, stack);
                return codes.AsEnumerable();
            }

            // can't figure out how to call the extension method directly so just use this
            private static TroopRosterElement GetAggregatedElement(TroopRosterElement element, TroopRoster roster)
            {
                if (element.Character.IsHero || element.Character.OriginalCharacter is null)
                    return element;
                var aggregate = element.GetNewAggregateTroopRosterElement(roster).GetValueOrDefault();
                return aggregate;
            }
        }

        // modified assembly copy 1.8.1
        [HarmonyPatch(typeof(PartyVM), "OnTransferTroop")]
        public class PartyVMOnTransferTroop
        {
            public static bool Prefix(PartyVM __instance, PartyCharacterVM troop, int newIndex, int transferAmount, PartyScreenLogic.PartyRosterSide fromSide)
            {
                if (!Globals.Settings.PartyScreenChanges)
                    return true;
                try
                {
                    if (troop.Side == PartyScreenLogic.PartyRosterSide.None || fromSide == PartyScreenLogic.PartyRosterSide.None)
                        return false;
                    Traverse.Create(__instance).Method("SetSelectedCharacter", troop).GetValue();
                    Log.Debug?.Log("\n");
                    Log.Debug?.Log($"Set OnTransfer {troop.Character.Name} {troop.Character.StringId}");
                    var partyCommand = new PartyScreenLogic.PartyCommand();
                    if (transferAmount <= 0)
                        return false;
                    if (troop.Character.IsHero || troop.Character.OriginalCharacter is null)
                    {
                        var numberOfHealthyTroopNumberForSide = (int)Traverse.Create(__instance).Method("GetNumberOfHealthyTroopNumberForSide", troop.Troop.Character, fromSide, troop.IsPrisoner).GetValue();
                        var numberOfWoundedTroopNumberForSide = (int)Traverse.Create(__instance).Method("GetNumberOfWoundedTroopNumberForSide", troop.Troop.Character, fromSide, troop.IsPrisoner).GetValue();
                        if ((__instance.PartyScreenLogic.TransferHealthiesGetWoundedsFirst && fromSide == PartyScreenLogic.PartyRosterSide.Right) || (!__instance.PartyScreenLogic.TransferHealthiesGetWoundedsFirst && fromSide == PartyScreenLogic.PartyRosterSide.Left))
                        {
                            var num = transferAmount > numberOfHealthyTroopNumberForSide ? transferAmount - numberOfHealthyTroopNumberForSide : 0;
                            num = (int)MathF.Clamp(num, 0f, numberOfWoundedTroopNumberForSide);
                            partyCommand.FillForTransferTroop(fromSide, troop.Type, troop.Character, transferAmount, num, newIndex);
                        }
                        else
                            partyCommand.FillForTransferTroop(fromSide, troop.Type, troop.Character, transferAmount, numberOfWoundedTroopNumberForSide >= transferAmount ? transferAmount : numberOfWoundedTroopNumberForSide, newIndex);

                        __instance.PartyScreenLogic.AddCommand(partyCommand);
                    }
                    else
                    {
                        for (var i = 0; i < transferAmount; i++)
                        {
                            var numberOfHealthyTroopNumberForSide = (int)Traverse.Create(__instance).Method("GetNumberOfHealthyTroopNumberForSide", troop.Character, fromSide, troop.IsPrisoner).GetValue();
                            var numberOfWoundedTroopNumberForSide = (int)Traverse.Create(__instance).Method("GetNumberOfWoundedTroopNumberForSide", troop.Character, fromSide, troop.IsPrisoner).GetValue();
                            // asymmetrical wounded transferring
                            if ((__instance.PartyScreenLogic.TransferHealthiesGetWoundedsFirst && fromSide == PartyScreenLogic.PartyRosterSide.Right)
                                || (!__instance.PartyScreenLogic.TransferHealthiesGetWoundedsFirst && fromSide == PartyScreenLogic.PartyRosterSide.Left))
                            {
                                var numWounded = transferAmount > numberOfHealthyTroopNumberForSide ? transferAmount - numberOfHealthyTroopNumberForSide : 0;
                                numWounded = (int)MathF.Clamp(numWounded, 0, 1);
                                numWounded = numberOfWoundedTroopNumberForSide;
                                if (numWounded == 0)
                                {
                                    var element = troop.Troops.GetTroopRoster().First(e => e.Character.Name.Equals(troop.Character.Name) && e.WoundedNumber == 0);
                                    troop.Troop = element;
                                    troop.Character = element.Character;
                                    troop.TroopID = element.Character.StringId;
                                }

                                partyCommand.FillForTransferTroop(fromSide, troop.Type, troop.Character, 1, numWounded, newIndex);
                            }

                            else
                                partyCommand.FillForTransferTroop(fromSide, troop.Type, troop.Character, 1, numberOfWoundedTroopNumberForSide, newIndex);

                            __instance.PartyScreenLogic.AddCommand(partyCommand);
                            // end original

                            var roster = troop.IsPrisoner
                                ? fromSide == PartyScreenLogic.PartyRosterSide.Left
                                    ? __instance.PartyScreenLogic.PrisonerRosters[(int)fromSide]
                                    : __instance.PartyScreenLogic.PrisonerRosters[(int)(1 - fromSide)]
                                : fromSide == PartyScreenLogic.PartyRosterSide.Left
                                    ? __instance.PartyScreenLogic.MemberRosters[(int)fromSide]
                                    : __instance.PartyScreenLogic.MemberRosters[(int)(1 - fromSide)];

                            var index = FindIndexOrSimilarIndex(roster, troop.Character);
                            var partyCharacter = new PartyCharacterVM(__instance.PartyScreenLogic, ProcessCharacterLock, SetSelectedCharacter, OnTransferTroop, null, OnFocusCharacter,
                                PartyViewModel, roster, index, troop.Type, fromSide, true, Traverse.Create(PartyViewModel).Field<string>("_fiveStackShortcutkeyText").Value,
                                Traverse.Create(PartyViewModel).Field<string>("_entireStackShortcutkeyText").Value);
                            Traverse.Create(__instance).Method("SetSelectedCharacter", partyCharacter).GetValue();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(PartyTradeVM), "FindTroopFromSide")]
        public class PartyTradeVMFindTroopsFromSidePatch
        {
            private static void Postfix(CharacterObject character, PartyScreenLogic.PartyRosterSide side, PartyScreenLogic ____partyScreenLogic, ref TroopRosterElement? __result)
            {
                if (!Globals.Settings.PartyScreenChanges)
                    return;
                try
                {
                    if (character.IsHero || character.OriginalCharacter is null)
                        return;
                    var roster = ____partyScreenLogic.MemberRosters[(int)side];
                    __result = roster.GetTroopRoster().FirstOrDefaultQ(e => e.Character == character).GetNewAggregateTroopRosterElement(roster).GetValueOrDefault();
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }
            }
        }

        // replace all calls to FindIndexOfTroop to FindIndexOrSimilarIndex
        public class PartyScreenLogicValidateCommand
        {
            private static readonly MethodBase findIndex = AccessTools.Method(typeof(TroopRoster), nameof(TroopRoster.FindIndexOfTroop));
            private static readonly MethodBase findSimilarIndex = AccessTools.Method(typeof(Helper), nameof(FindIndexOrSimilarIndex));
            private static readonly MethodBase woundedFirst = AccessTools.Method(typeof(Helper), nameof(WoundedFirst));
            private static readonly MethodBase getSimilarElementXp = AccessTools.Method(typeof(PartyScreenLogicValidateCommand), nameof(GetSimilarElementXp));

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToListQ();
                var stack = new List<CodeInstruction>
                {
                    new(OpCodes.Ldarg_1),
                    new(OpCodes.Call, woundedFirst),
                    new(OpCodes.Call, findSimilarIndex),
                };
                for (var index = 0; index < codes.Count; index++)
                {
                    var code = codes[index];
                    if (code.opcode == OpCodes.Ldelem_Ref
                        && codes[index + 2].OperandIs(findIndex))
                    {
                        codes[index + 2].opcode = OpCodes.Nop;
                        codes.InsertRange(index + 3, stack);
                        index += stack.Count; // hop to avoid recursion
                    }
                }

                // replace GetElementXp with GetSimilarElementXp
                for (var index = 0; index < codes.Count; index++)
                {
                    var code = codes[index];
                    if (code.opcode == OpCodes.Callvirt
                        && codes[index + 1].opcode == OpCodes.Ldloc_S
                        && codes[index + 2].opcode == OpCodes.Ldarg_1
                        && codes[index + 3].opcode == OpCodes.Callvirt)
                    {
                        codes[index].opcode = OpCodes.Call;
                        codes[index].operand = getSimilarElementXp;
                    }
                }

                return codes.AsEnumerable();
            }

            private static int GetSimilarElementXp(TroopRoster roster, int index)
            {
                return roster.GetElementCopyAtIndex(index).GetNewAggregateTroopRosterElement(roster).GetValueOrDefault().Xp;
            }
        }

        [HarmonyPatch(typeof(PartyVM), "UpgradeTroop")]
        public class PartyVMUpgradeTroop
        {
            private static readonly MethodBase getElementCopy = AccessTools.Method(typeof(TroopRoster), nameof(TroopRoster.GetElementCopyAtIndex));
            private static readonly MethodBase getSimilarElementCopy = AccessTools.Method(typeof(PartyVMUpgradeTroop), nameof(GetSimilarElementCopy));
            private static readonly MethodBase findIndex = AccessTools.Method(typeof(TroopRoster), nameof(TroopRoster.FindIndexOfTroop));
            private static readonly MethodBase findSimilarIndex = AccessTools.Method(typeof(Helper), nameof(FindIndexOrSimilarIndex));
            private static readonly MethodBase woundedFirst = AccessTools.Method(typeof(Helper), nameof(WoundedFirst));

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToListQ();
                var skipFirst = true;
                for (var i = 0; i < codes.Count; i++)
                {
                    var code = codes[i];
                    if (code.opcode == OpCodes.Callvirt && code.OperandIs(findIndex))
                    {
                        if (skipFirst)
                        {
                            skipFirst = false;
                            continue;
                        }

                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = findSimilarIndex;
                        codes.Insert(i, new(OpCodes.Ldarg_1)); // false, default value
                        codes.Insert(i + 1, new(OpCodes.Call, woundedFirst)); // false, default value
                    }
                }

                for (var i = 0; i < codes.Count; i++)
                {
                    var code = codes[i];
                    if (code.opcode == OpCodes.Callvirt && code.OperandIs(getElementCopy))
                    {
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = getSimilarElementCopy;
                    }
                }

                return codes.AsEnumerable();
            }

            private static TroopRosterElement GetSimilarElementCopy(TroopRoster roster, int index)
            {
                return roster.GetElementCopyAtIndex(index).GetNewAggregateTroopRosterElement(roster).GetValueOrDefault();
            }
        }

        // modified assembly copy 1.8.1
        [HarmonyPatch(typeof(PartyScreenLogic), "TransferTroop")]
        public class PartyScreenLogicTransferTroop
        {
            public static bool Prefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, PartyScreenLogic.PresentationUpdate ___Update)
            {
                if (!Globals.Settings.PartyScreenChanges)
                    return true;
                try
                {
                    if (!__instance.ValidateCommand(command))
                        return false;
                    var commandTroop = command.Character;
                    if (command.Type == PartyScreenLogic.TroopType.Member)
                    {
                        var roster = __instance.MemberRosters[(int)command.RosterSide];
                        var otherRoster = __instance.MemberRosters[(int)(1 - command.RosterSide)];
                        var index2 = FindIndexOrSimilarIndex(roster, commandTroop, command.WoundedNumber > 0);
                        var elementCopyAtIndex = roster.GetElementCopyAtIndex(index2).GetNewAggregateTroopRosterElement(roster).GetValueOrDefault();
                        var num1 = commandTroop.UpgradeTargets.Length != 0 ? commandTroop.UpgradeTargets.Max(x => Campaign.Current.Models.PartyTroopUpgradeModel.GetXpCostForUpgrade(PartyBase.MainParty, commandTroop, x)) : 0;
                        int xpAmount;
                        if (command.RosterSide == PartyScreenLogic.PartyRosterSide.Right)
                        {
                            var num2 = (elementCopyAtIndex.Number - command.TotalNumber) * num1;
                            xpAmount = elementCopyAtIndex.Xp < num2 || num2 < 0 ? 0 : elementCopyAtIndex.Xp - num2;
                        }
                        else
                        {
                            var num3 = command.TotalNumber * num1;
                            xpAmount = elementCopyAtIndex.Xp <= num3 || num3 < 0 ? elementCopyAtIndex.Xp : num3;
                            __instance.MemberRosters[(int)command.RosterSide].AddXpToTroop(-xpAmount, commandTroop);
                        }


                        var commandWounded = command.WoundedNumber;
                        for (var n = 0; n < command.TotalNumber; n++)
                        {
                            var troop = elementCopyAtIndex.Character; // roster.GetElementCopyAtIndex(index2).Character;
                            var numWounded = commandWounded-- > 0 ? 1 : 0;
                            try
                            {
                                Log.Debug?.Log($"Moving {troop.StringId}");
                                roster.AddToCounts(troop, -1, woundedCount: -numWounded, removeDepleted: false, index: index2);
                                otherRoster.AddToCounts(troop, 1, woundedCount: numWounded, removeDepleted: false, index: command.Index);
                                otherRoster.AddXpToTroop(xpAmount, troop);
                            }
                            catch (Exception ex)
                            {
                                LogException(ex);
                            }
                        }
                    }
                    else
                    {
                        var roster = __instance.PrisonerRosters[(int)command.RosterSide];
                        var otherRoster = __instance.PrisonerRosters[(int)(1 - command.RosterSide)];
                        var indexOfTroop = FindIndexOrSimilarIndex(roster, commandTroop, command.WoundedNumber > 0);
                        var elementCopyAtIndex = roster.GetElementCopyAtIndex(indexOfTroop).GetNewAggregateTroopRosterElement(roster).GetValueOrDefault();
                        var toRecruitPrisoner = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetConformityNeededToRecruitPrisoner(elementCopyAtIndex.Character);
                        int xpAmount;
                        if (command.RosterSide == PartyScreenLogic.PartyRosterSide.Right)
                        {
                            Traverse.Create(__instance).Method("UpdatePrisonerTransferHistory", commandTroop, -command.TotalNumber).GetValue();
                            var num = (elementCopyAtIndex.Number - command.TotalNumber) * toRecruitPrisoner;
                            xpAmount = elementCopyAtIndex.Xp < num || num < 0 ? 0 : elementCopyAtIndex.Xp - num;
                        }
                        else
                        {
                            Traverse.Create(__instance).Method("UpdatePrisonerTransferHistory", commandTroop, command.TotalNumber).GetValue();
                            var num = command.TotalNumber * toRecruitPrisoner;
                            xpAmount = elementCopyAtIndex.Xp <= num || num < 0 ? elementCopyAtIndex.Xp : num;
                            __instance.PrisonerRosters[(int)command.RosterSide].AddXpToTroop(-xpAmount, commandTroop);
                        }

                        // BUG? commandTroop showing the last moved prisoner
                        // have to reestablish the index for some reason that escapes me now
                        var transferIndex = FindIndexOrSimilarIndex(roster, commandTroop, false);
                        var transferTroop = roster.GetElementCopyAtIndex(transferIndex).Character;
                        roster.AddToCounts(transferTroop, -command.TotalNumber, woundedCount: (-command.WoundedNumber), removeDepleted: false, index: command.Index);
                        otherRoster.AddToCounts(transferTroop, command.TotalNumber, woundedCount: command.WoundedNumber, removeDepleted: false, index: command.Index);
                        otherRoster.AddXpToTroop(xpAmount, commandTroop);
                        if (__instance.CurrentData.RightRecruitableData.ContainsKey(commandTroop))
                            __instance.CurrentData.RightRecruitableData[commandTroop] = MathF.Max(MathF.Min(__instance.CurrentData.RightRecruitableData[commandTroop], __instance.PrisonerRosters[1].GetElementNumber(commandTroop)), Campaign.Current.Models.PrisonerRecruitmentCalculationModel.CalculateRecruitableNumber(PartyBase.MainParty, commandTroop));
                    }

                    if (__instance.PrisonerTransferState == PartyScreenLogic.TransferState.TransferableWithTrade && command.Type == PartyScreenLogic.TroopType.Prisoner)
                    {
                        if (command.RosterSide == PartyScreenLogic.PartyRosterSide.Right)
                            Traverse.Create(__instance).Method("SetPartyGoldChangeAmount", __instance.CurrentData.PartyGoldChangeAmount + Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(command.Character, Hero.MainHero) * command.TotalNumber).GetValue();
                        else
                            Traverse.Create(__instance).Method("SetPartyGoldChangeAmount", __instance.CurrentData.PartyGoldChangeAmount - Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(command.Character, Hero.MainHero) * command.TotalNumber).GetValue();
                    }

                    if (PartyScreenManager.Instance.IsDonating)
                    {
                        var currentSettlement = Hero.MainHero.CurrentSettlement;
                        var heroInfluence = 0.0f;
                        var troopInfluence = 0.0f;
                        var prisonerInfluence = 0.0f;
                        foreach (var troopTradeDifference in Traverse.Create(__instance).Field<PartyScreenData>("_initialData").Value.GetTroopTradeDifferencesFromTo(__instance.CurrentData))
                        {
                            var num = troopTradeDifference.FromCount - troopTradeDifference.ToCount;
                            if (num > 0)
                            {
                                if (!troopTradeDifference.IsPrisoner)
                                    troopInfluence += num * Campaign.Current.Models.PrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation(PartyBase.MainParty, troopTradeDifference.Troop, currentSettlement);
                                else if (troopTradeDifference.Troop.IsHero)
                                    heroInfluence += Campaign.Current.Models.PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation(PartyBase.MainParty, troopTradeDifference.Troop, currentSettlement);
                                else
                                    prisonerInfluence += num * Campaign.Current.Models.PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation(PartyBase.MainParty, troopTradeDifference.Troop, currentSettlement);
                            }
                        }

                        Traverse.Create(__instance).Method("SetInfluenceChangeAmount", ((int)heroInfluence, (int)troopInfluence, (int)prisonerInfluence)).GetValue();
                    }

                    var updateDelegate = __instance.UpdateDelegate;
                    updateDelegate?.Invoke(command);
                    ___Update?.Invoke(command);
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(PartyVM), "TransferTroop")]
        public class PartyVMTransferTroop
        {
            private static bool Prefix(PartyVM __instance, PartyScreenLogic.PartyCommand command,
                PartyCharacterVM ____currentCharacter, string ____fiveStackShortcutkeyText, string ____entireStackShortcutkeyText)
            {
                if (!Globals.Settings.PartyScreenChanges)
                    return true;
                try
                {
                    if (!____currentCharacter.Character.IsHero && ____currentCharacter.Character.OriginalCharacter is null)
                        return true;
                    var index1 = PartyScreenLogic.PartyRosterSide.None;
                    switch (command.RosterSide)
                    {
                        case PartyScreenLogic.PartyRosterSide.Left:
                            index1 = PartyScreenLogic.PartyRosterSide.Right;
                            break;
                        case PartyScreenLogic.PartyRosterSide.Right:
                            index1 = PartyScreenLogic.PartyRosterSide.Left;
                            break;
                    }

                    // out args need to be declared for reflection call
                    MBBindingList<PartyCharacterVM> commandSide = default;
                    // ReSharper disable once ExpressionIsAlwaysNull
                    var args = new object[] { commandSide, command.RosterSide, command.Type };
                    AccessTools.Method(typeof(PartyVM), "GetPartyCharacterVMList").Invoke(__instance, args);
                    commandSide = (MBBindingList<PartyCharacterVM>)args[0];
                    MBBindingList<PartyCharacterVM> otherSide = default;
                    // ReSharper disable once ExpressionIsAlwaysNull
                    var args2 = new object[] { otherSide, index1, command.Type };
                    AccessTools.Method(typeof(PartyVM), "GetPartyCharacterVMList").Invoke(__instance, args2);
                    otherSide = (MBBindingList<PartyCharacterVM>)args2[0];
                    var memberRoster = __instance.PartyScreenLogic.MemberRosters[(int)command.RosterSide];
                    var prisonerRoster = __instance.PartyScreenLogic.PrisonerRosters[(int)command.RosterSide];
                    if (command.Type == PartyScreenLogic.TroopType.Member)
                    {
                        var index = FindIndexOrSimilarIndex(memberRoster, __instance.CurrentCharacter.Character, command.WoundedNumber > 0);
                        var troop = memberRoster.GetElementCopyAtIndex(index);
                        ____currentCharacter.Troop = troop.GetNewAggregateTroopRosterElement(memberRoster).GetValueOrDefault();
                    }
                    else if (command.Type == PartyScreenLogic.TroopType.Prisoner)
                    {
                        var index = FindIndexOrSimilarIndex(prisonerRoster, __instance.CurrentCharacter.Character);
                        ____currentCharacter.Troop = prisonerRoster.GetElementCopyAtIndex(index).GetNewAggregateTroopRosterElement(prisonerRoster).GetValueOrDefault();
                    }

                    ____currentCharacter.UpdateTradeData();
                    ____currentCharacter.ThrowOnPropertyChanged();
                    TroopRoster otherRoster = null;
                    TroopRoster commandRoster = null;
                    var otherIndex = 0;
                    var commandRosterIndex = 0;
                    switch (command.Type)
                    {
                        case PartyScreenLogic.TroopType.Member:
                            otherRoster = __instance.PartyScreenLogic.MemberRosters[(int)(1 - command.RosterSide)];
                            otherIndex = FindIndexOrSimilarIndex(otherRoster, ____currentCharacter.Character, command.WoundedNumber <= 0);
                            commandRoster = memberRoster;
                            commandRosterIndex = FindIndexOrSimilarIndex(commandRoster, ____currentCharacter.Character, command.WoundedNumber > 0);
                            break;
                        case PartyScreenLogic.TroopType.Prisoner:
                            otherRoster = __instance.PartyScreenLogic.PrisonerRosters[(int)(1 - command.RosterSide)];
                            otherIndex = FindIndexOrSimilarIndex(otherRoster, ____currentCharacter.Character);
                            commandRoster = prisonerRoster;
                            commandRosterIndex = FindIndexOrSimilarIndex(commandRoster, ____currentCharacter.Character);
                            break;
                    }

                    var partyCharacterVm = commandSide.FirstOrDefault(q => q.Character.Name.Equals(__instance.CurrentCharacter.Character.Name));
                    if (commandRoster!.FindIndexOfTroop(__instance.CurrentCharacter.Character) != -1 && partyCharacterVm != null)
                    {
                        partyCharacterVm.Troop = commandRoster.GetElementCopyAtIndex(commandRosterIndex).GetNewAggregateTroopRosterElement(commandRoster).GetValueOrDefault();
                        partyCharacterVm.UpdateTradeData();
                        partyCharacterVm.ThrowOnPropertyChanged();
                    }

                    if (otherSide.AnyQ(p => p.Character.Name.Equals(__instance.CurrentCharacter.Character.Name)))
                    {
                        var troop = otherSide.First(q => q.Character.Name.Equals(__instance.CurrentCharacter.Character.Name));
                        troop.Troop = otherRoster.GetElementCopyAtIndex(otherIndex).GetNewAggregateTroopRosterElement(otherRoster).GetValueOrDefault();
                        troop.UpdateTradeData();
                        troop.ThrowOnPropertyChanged();
                        if (!commandSide.Contains(__instance.CurrentCharacter))
                            Traverse.Create(__instance).Method("SetSelectedCharacter", troop).GetValue();
                    }
                    else
                    {
                        var troop = new PartyCharacterVM(__instance.PartyScreenLogic, ProcessCharacterLock, SetSelectedCharacter, OnTransferTroop,
                            null, OnFocusCharacter, __instance, otherRoster, otherIndex, command.Type, index1, __instance.PartyScreenLogic.IsTroopTransferable(command.Type, otherRoster.GetCharacterAtIndex(otherIndex), (int)index1), ____fiveStackShortcutkeyText, ____entireStackShortcutkeyText);
                        if (command.Index != -1)
                            otherSide.Insert(command.Index, troop);
                        else
                            otherSide.Add(troop);
                        if (!commandSide.AnyQ(p => p.Character.Name.Equals(__instance.CurrentCharacter.Character.Name)))
                            Traverse.Create(__instance).Method("SetSelectedCharacter", troop).GetValue();
                        troop.IsLocked = troop.Side == PartyScreenLogic.PartyRosterSide.Right &&
                                         Traverse.Create(__instance).Method("IsTroopLocked", (troop.Troop, troop.IsPrisoner)).GetValue<bool>();
                    }

                    __instance.CurrentCharacter.UpdateTradeData();
                    __instance.CurrentCharacter.OnTransferred();
                    __instance.CurrentCharacter.ThrowOnPropertyChanged();
                    Traverse.Create(__instance).Method("RefreshTopInformation").GetValue();
                    Traverse.Create(__instance).Method("RefreshPartyInformation").GetValue();
                    Game.Current.EventManager.TriggerEvent(new PlayerMoveTroopEvent(command.Character, command.RosterSide, (PartyScreenLogic.PartyRosterSide)((uint)(command.RosterSide + 1) % 2U), command.TotalNumber, command.Type == PartyScreenLogic.TroopType.Prisoner));
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(PartyCharacterVM), "ExecuteTransferSingle")]
        public class PartyCharacterVMExecuteTransferSingle
        {
            public static void Postfix(ref PartyCharacterVM __instance)
            {
                if (!Globals.Settings.PartyScreenChanges)
                    return;
                // the PartyCharacterVM ctor postfix places any wounded troops first so now update on transfer
                try
                {
                    ResetPartyCharacterVm(__instance, PartyViewModel);
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }
            }
        }

        [HarmonyPatch(typeof(SaveableCampaignTypeDefiner), "DefineContainerDefinitions")]
        public class SaveableCampaignTypeDefinerDefineContainerDefinitions
        {
            public static void Postfix(SaveableCampaignTypeDefiner __instance)
            {
                AccessTools.Method(typeof(CampaignBehaviorBase.SaveableCampaignBehaviorTypeDefiner),
                    "ConstructContainerDefinition").Invoke(__instance, new object[] { typeof(Dictionary<string, Equipment>) });
                AccessTools.Method(typeof(CampaignBehaviorBase.SaveableCampaignBehaviorTypeDefiner),
                    "ConstructContainerDefinition").Invoke(__instance, new object[] { typeof(Dictionary<string, CharacterSkills>) });
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

        internal static Exception Finalizer() => null;

        // kludgey hideout 1v1 crash patch
        internal static void HideoutBossDuelPrefix(SPScoreboardSideVM __instance, BasicCharacterObject troop)
        {
            // if this troop doesn't appear anywhere, generate all the missing ones so it can complete
            if (!__instance.Parties.AnyQ(s => s.Members.AnyQ(m => m.Character == troop)))
            {
                foreach (var partyVm in __instance.Parties)
                {
                    var roster = ((PartyBase)partyVm.BattleCombatant).MemberRoster;
                    foreach (var member in roster.GetTroopRoster())
                    {
                        if (!member.Character.IsHero && member.Character.OriginalCharacter is not null)
                            __instance.AddTroop(partyVm.BattleCombatant, member.Character, new SPScoreboardStatsVM(new TextObject()));
                    }
                }
            }
        }

        internal static Exception UpdateFinalizer(Exception __exception)
        {
            if (__exception is not null)
            {
                Debug.DebugManager.PrintWarning("GloriousTroops is doing a restore operation due to an update");
                Restore();
            }

            return null;
        }

        [HarmonyPatch(typeof(CharacterObject), "GetSkillValue")]
        public class CharacterObjectGetSkillValue
        {
            public static Exception Finalizer(Exception __exception)
            {
                if (__exception is not null)
                {
                    Debug.DebugManager.PrintWarning("GloriousTroops is doing a restore operation due to an update");
                    Restore();
                }

                return null;
            }
        }
        //
        // [HarmonyPatch(typeof(MBObjectManager), "UnregisterObject")]
        // public class MBObjectManagerUnregisterObject
        // {
        //     public static void Prefix(MBObjectBase obj)
        //     {
        //         if (obj is CharacterObject characterObject)
        //         {
        //             if (characterObject.Name.Contains("Caravan"))
        //             {
        //             }
        //         }
        //     }
        // }

        // [HarmonyPatch(typeof(TroopRoster), "AddToCounts")]
        // public class TroopRosterAddToCounts
        // {
        //     public static void Prefix(CharacterObject character)
        //     {
        //         if (character.Name is null)
        //         {
        //             Log.Debug?.Log("\n");
        //             new StackTrace().GetFrames()?.Take(5).Select(f => f.GetMethod()?.FullDescription()).Do(x => Log.Debug?.Log(x));
        //             return;
        //         }
        //
        //         if (character.Name.Contains("Caravan"))
        //         {
        //             Log.Debug?.Log("\n");
        //             new StackTrace().GetFrames()?.Take(5).Select(f => f.GetMethod()?.FullDescription()).Do(x => Log.Debug?.Log(x));
        //         }
        //     }
        // }

        public class SaveContextCollectObjects
        {
            public static void Postfix(object __instance)
            {
                var childObjects = Traverse.Create(__instance).Field<List<object>>("_childObjects").Value;
                var childObjectIds = Traverse.Create(__instance).Field<Dictionary<object, int>>("_idsOfChildObjects").Value;
                var childContainers = Traverse.Create(__instance).Field<List<object>>("_childContainers").Value;
                var childContainersIds = Traverse.Create(__instance).Field<Dictionary<object, int>>("_idsOfChildContainers").Value;
                for (var i = 60_000; i < childObjects.Count; i++) // aim high to avoid wiping too many
                {
                    if (childObjects[i] is CharacterSkills cs)
                    {
                        childObjects.RemoveAt(i--);
                        childObjectIds.Remove(childObjectIds.ElementAt(i));
                        cs = null;
                    }
                }

                for (var i = 40_000; i < childContainers.Count; i++)
                {
                    if (childContainers[i] is Dictionary<SkillObject, int> container)
                    {
                        childContainers.RemoveAt(i--);
                        childContainersIds.Remove(childContainersIds.ElementAt(i));
                        container = null;
                    }
                }
            }
        }
    }
}
