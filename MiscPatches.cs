using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
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

        // appears to work
        //[HarmonyPatch(typeof(PartyVM), "InitializePartyList")]
        //public static class PartyVMInitializePartyList
        //{
        //    private static bool Prefix(PartyVM __instance,
        //        MBBindingList<PartyCharacterVM> partyList,
        //        TroopRoster currentTroopRoster,
        //        PartyScreenLogic.TroopType type,
        //        int side,
        //        string ____fiveStackShortcutkeyText,
        //        string ____entireStackShortcutkeyText)
        //    {
        //        partyList.Clear();
        //        var map = new Dictionary<string, PartyCharacterVM>();
        //        for (var index = 0; index < currentTroopRoster.Count; ++index)
        //        {
        //            var elementCopyAtIndex = currentTroopRoster.GetElementCopyAtIndex(index);
        //            if (!map.TryGetValue(elementCopyAtIndex.Character.Name.ToString(), out var partyCharacterVm))
        //            {
        //                partyCharacterVm = new PartyCharacterVM(__instance.PartyScreenLogic, ProcessCharacterLock, SetSelectedCharacter, OnTransferTroop, null, OnFocusCharacter, __instance, currentTroopRoster, index, type, (PartyScreenLogic.PartyRosterSide)side, __instance.PartyScreenLogic.IsTroopTransferable(type, elementCopyAtIndex.Character, side), ____fiveStackShortcutkeyText, ____entireStackShortcutkeyText);
        //                map.Add(elementCopyAtIndex.Character.Name.ToString(), partyCharacterVm);
        //                partyList.Add(partyCharacterVm);
        //            }
        //            else
        //            {
        //                elementCopyAtIndex.Number += partyCharacterVm.Number;
        //                partyCharacterVm.Troop = elementCopyAtIndex;
        //            }
        //
        //            partyCharacterVm.ThrowOnPropertyChanged();
        //            partyCharacterVm.IsLocked = partyCharacterVm.Side == PartyScreenLogic.PartyRosterSide.Right && (bool)Traverse.Create(__instance).Method("IsTroopLocked", partyCharacterVm.Troop, partyCharacterVm.IsPrisoner).GetValue();
        //        }
        //
        //        return false;
        //
        //        void OnFocusCharacter(PartyCharacterVM partyCharacterVm)
        //        {
        //            AccessTools.Method(typeof(PartyVM), "OnFocusCharacter").Invoke(__instance, new object[] { partyCharacterVm });
        //        }
        //
        //        void SetSelectedCharacter(PartyCharacterVM partyCharacterVm)
        //        {
        //            AccessTools.Method(typeof(PartyVM), "SetSelectedCharacter").Invoke(__instance, new object[] { partyCharacterVm });
        //        }
        //
        //        void ProcessCharacterLock(PartyCharacterVM partyCharacterVm, bool b)
        //        {
        //            AccessTools.Method(typeof(PartyVM), "ProcessCharacterLock").Invoke(__instance, new object[] { partyCharacterVm, b });
        //        }
        //
        //        void OnTransferTroop(PartyCharacterVM partyCharacterVm, int i, int arg3, PartyScreenLogic.PartyRosterSide arg4)
        //        {
        //            AccessTools.Method(typeof(PartyVM), "OnTransferTroop").Invoke(__instance, new object[] { partyCharacterVm, i, arg3, arg4 });
        //        }
        //    }
        //}
        //
        //// makes the PartyTradeVM (troop widgets) show the proper number of troops by patch its sole instantiation, in this ctor
        //public class PartyCharacterVMConstructor
        //{
        //    private static readonly ConstructorInfo from = AccessTools.FirstConstructor(typeof(PartyTradeVM), c => c.GetParameters().Length > 0);
        //    private static readonly MethodInfo helper = AccessTools.Method(typeof(PartyCharacterVMConstructor), nameof(TrickCtor));
        //
        //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        var codes = instructions.ToListQ();
        //        int index = -1;
        //        for (var i = 0; i < codes.Count; i++)
        //        {
        //            // new PartyTradeVM
        //            if (codes[i].OperandIs(from))
        //                index = i;
        //        }
        //
        //        codes.Insert(index, new CodeInstruction(OpCodes.Ldarg_S, 8)); // TroopRoster
        //        codes[index + 1].opcode = OpCodes.Call;
        //        codes[index + 1].operand = helper;
        //        return codes.AsEnumerable();
        //    }
        //
        //    private static PartyTradeVM TrickCtor(PartyScreenLogic partyScreenLogic, TroopRosterElement troopRoster,
        //        PartyScreenLogic.PartyRosterSide side, bool isTransfarable, bool isPrisoner,
        //        Action<int, bool> onApplyTransaction, TroopRoster roster)
        //    {
        //        var element = troopRoster.AllSimilar(roster).GetValueOrDefault();
        //        var sum = element.Number + element.WoundedNumber; //   roster.GetTroopRoster().WhereQ(e => e.Character.Name.Equals(troopRoster.Character.Name)).SumQ(e => e.Number);
        //        troopRoster.Number = sum;
        //        return new PartyTradeVM(partyScreenLogic, troopRoster, side, isTransfarable, isPrisoner, onApplyTransaction);
        //    }
        //}
        //
        //// step in to adjust counts in various places
        //[HarmonyPatch(typeof(PartyScreenLogic), "TransferTroop")]
        //public class PartyScreenLogicTransferTroop
        //{
        //    public static bool Prefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command)
        //    {
        //        var flag = false;
        //        if (__instance.ValidateCommand(command))
        //        {
        //            var troop = command.Character;
        //            if (command.Type == PartyScreenLogic.TroopType.Member)
        //            {
        //                var indexOfTroop = __instance.MemberRosters[(int)command.RosterSide].FindIndexOfTroop(troop);
        //                var roster = __instance.MemberRosters[(int)command.RosterSide];
        //                var elementCopyAtIndex = roster.GetElementCopyAtIndex(indexOfTroop).AllSimilar(roster);
        //                //var elementCopyAtIndex = __instance.MemberRosters[(int)command.RosterSide].GetElementCopyAtIndex(indexOfTroop);
        //                var num1 = troop.UpgradeTargets.Length != 0 ? troop.UpgradeTargets.Max(x => Campaign.Current.Models.PartyTroopUpgradeModel.GetXpCostForUpgrade(PartyBase.MainParty, troop, x)) : 0;
        //                int xpAmount;
        //                if (command.RosterSide == PartyScreenLogic.PartyRosterSide.Right)
        //                {
        //                    var num2 = (elementCopyAtIndex.GetValueOrDefault().Number - command.TotalNumber) * num1;
        //                    xpAmount = elementCopyAtIndex.GetValueOrDefault().Xp < num2 || num2 < 0 ? 0 : elementCopyAtIndex.GetValueOrDefault().Xp - num2;
        //                }
        //                else
        //                {
        //                    var num3 = command.TotalNumber * num1;
        //                    xpAmount = elementCopyAtIndex.GetValueOrDefault().Xp <= num3 || num3 < 0 ? elementCopyAtIndex.GetValueOrDefault().Xp : num3;
        //                    __instance.MemberRosters[(int)command.RosterSide].AddXpToTroop(-xpAmount, troop);
        //                }
        //
        //                var otherRoster = __instance.MemberRosters[(int)(1 - command.RosterSide)];
        //                // iterate through the roster and operate n times
        //                for (var n = 0; n < command.TotalNumber; n++)
        //                {
        //                    // find troop again
        //                    var troop1 = troop;
        //                    var foo = roster.GetTroopRoster().FirstOrDefaultQ(e => e.Character.Name.Equals(troop1.Name)).Character;
        //                    //if (troop is null)
        //                    //    break;
        //                    indexOfTroop = roster.FindIndexOfTroop(troop);
        //                    roster.AddToCounts(troop, -1, woundedCount: 0, removeDepleted: false, index: indexOfTroop);
        //                    otherRoster.AddToCounts(troop, 1, woundedCount: 0, removeDepleted: false, index: command.Index);
        //                    otherRoster.AddXpToTroop(xpAmount, troop);
        //                }
        //
        //                for (var n = 0; n < command.WoundedNumber; n++)
        //                {
        //                    // find troop again
        //                    troop = roster.GetTroopRoster().FirstOrDefaultQ(e => e.Character.Name.Equals(troop.Name)).Character;
        //                    if (troop is null)
        //                        break;
        //                    indexOfTroop = roster.FindIndexOfTroop(troop);
        //                    roster.AddToCounts(troop, 0, woundedCount: -1, removeDepleted: false, index: indexOfTroop);
        //                    otherRoster.AddToCounts(troop, 0, woundedCount: 1, removeDepleted: false, index: command.Index);
        //                    otherRoster.AddXpToTroop(xpAmount, troop);
        //                }
        //            }
        //            else
        //            {
        //                var indexOfTroop = __instance.PrisonerRosters[(int)command.RosterSide].FindIndexOfTroop(troop);
        //                var elementCopyAtIndex = __instance.PrisonerRosters[(int)command.RosterSide].GetElementCopyAtIndex(indexOfTroop);
        //                var toRecruitPrisoner = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetConformityNeededToRecruitPrisoner(elementCopyAtIndex.Character);
        //                int xpAmount;
        //                if (command.RosterSide == PartyScreenLogic.PartyRosterSide.Right)
        //                {
        //                    Traverse.Create(__instance).Method("UpdatePrisonerTransferHistory", troop, -command.TotalNumber).GetValue();
        //                    var num = (elementCopyAtIndex.Number - command.TotalNumber) * toRecruitPrisoner;
        //                    xpAmount = elementCopyAtIndex.Xp < num || num < 0 ? 0 : elementCopyAtIndex.Xp - num;
        //                }
        //                else
        //                {
        //                    Traverse.Create(__instance).Method("UpdatePrisonerTransferHistory", troop, command.TotalNumber).GetValue();
        //                    var num = command.TotalNumber * toRecruitPrisoner;
        //                    xpAmount = elementCopyAtIndex.Xp <= num || num < 0 ? elementCopyAtIndex.Xp : num;
        //                    __instance.PrisonerRosters[(int)command.RosterSide].AddXpToTroop(-xpAmount, troop);
        //                }
        //
        //                __instance.PrisonerRosters[(int)command.RosterSide].AddToCounts(troop, -command.TotalNumber, woundedCount: (-command.WoundedNumber), removeDepleted: false, index: command.Index);
        //                __instance.PrisonerRosters[(int)(1 - command.RosterSide)].AddToCounts(troop, command.TotalNumber, woundedCount: command.WoundedNumber, removeDepleted: false, index: command.Index);
        //                __instance.PrisonerRosters[(int)(1 - command.RosterSide)].AddXpToTroop(xpAmount, troop);
        //                if (__instance.CurrentData.RightRecruitableData.ContainsKey(troop))
        //                    __instance.CurrentData.RightRecruitableData[troop] = MathF.Max(MathF.Min(__instance.CurrentData.RightRecruitableData[troop], __instance.PrisonerRosters[1].GetElementNumber(troop)), Campaign.Current.Models.PrisonerRecruitmentCalculationModel.CalculateRecruitableNumber(PartyBase.MainParty, troop));
        //            }
        //
        //            flag = true;
        //        }
        //
        //        if (!flag)
        //            return false;
        //        if (__instance.PrisonerTransferState == PartyScreenLogic.TransferState.TransferableWithTrade && command.Type == PartyScreenLogic.TroopType.Prisoner)
        //        {
        //            if (command.RosterSide == PartyScreenLogic.PartyRosterSide.Right)
        //                Traverse.Create(__instance).Method("SetPartyGoldChangeAmount", __instance.CurrentData.PartyGoldChangeAmount + Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(command.Character, Hero.MainHero) * command.TotalNumber).GetValue();
        //            else
        //                Traverse.Create(__instance).Method("SetPartyGoldChangeAmount", __instance.CurrentData.PartyGoldChangeAmount - Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(command.Character, Hero.MainHero) * command.TotalNumber).GetValue();
        //        }
        //
        //        if (PartyScreenManager.Instance.IsDonating)
        //        {
        //            var currentSettlement = Hero.MainHero.CurrentSettlement;
        //            var heroInfluence = 0.0f;
        //            var troopInfluence = 0.0f;
        //            var prisonerInfluence = 0.0f;
        //            foreach (var troopTradeDifference in Traverse.Create(__instance).Field<PartyScreenData>("_initialData").Value.GetTroopTradeDifferencesFromTo(__instance.CurrentData))
        //            {
        //                var num = troopTradeDifference.FromCount - troopTradeDifference.ToCount;
        //                if (num > 0)
        //                {
        //                    if (!troopTradeDifference.IsPrisoner)
        //                        troopInfluence += num * Campaign.Current.Models.PrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation(PartyBase.MainParty, troopTradeDifference.Troop, currentSettlement);
        //                    else if (troopTradeDifference.Troop.IsHero)
        //                        heroInfluence += Campaign.Current.Models.PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation(PartyBase.MainParty, troopTradeDifference.Troop, currentSettlement);
        //                    else
        //                        prisonerInfluence += num * Campaign.Current.Models.PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation(PartyBase.MainParty, troopTradeDifference.Troop, currentSettlement);
        //                }
        //            }
        //
        //            Traverse.Create(__instance).Method("SetInfluenceChangeAmount", ((int)heroInfluence, (int)troopInfluence, (int)prisonerInfluence)).GetValue();
        //        }
        //
        //        var updateDelegate = __instance.UpdateDelegate;
        //        updateDelegate?.Invoke(command);
        //        var update = Traverse.Create(__instance).Field<PartyScreenLogic.PresentationUpdate>("Update").Value;
        //        update?.Invoke(command);
        //        return false;
        //    }
        //}
        //
        //// when the index isn't found, search by name
        //[HarmonyPatch(typeof(PartyVM), "TransferTroop")]
        //public class PartyVMTransferTroop
        //{
        //    private static FieldInfo MemberRosters = AccessTools.Field(typeof(PartyScreenLogic), nameof(PartyScreenLogic.MemberRosters));
        //
        //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        var codes = instructions.ToList();
        //        var target = -1;
        //        for (var i = 0; i < codes.Count; i++)
        //        {
        //            if (codes[i].opcode == OpCodes.Ldarg_0
        //                && codes[i + 1].opcode == OpCodes.Call
        //                && codes[i + 2].opcode == OpCodes.Ldfld && (FieldInfo)codes[i + 2].operand == MemberRosters
        //                && codes[i + 3].opcode == OpCodes.Ldarg_1
        //                && codes[i + 4].opcode == OpCodes.Callvirt
        //                && codes[i + 5].opcode == OpCodes.Ldelem_Ref
        //                && codes[i + 6].opcode == OpCodes.Stloc_S)
        //                target = i;
        //        }
        //
        //        // swap index2 for a checked one
        //        var stack = new CodeInstruction[]
        //        {
        //            new(OpCodes.Ldloc_S, 5),
        //            new(OpCodes.Ldloc_0), // index1
        //            new(OpCodes.Ldarg_0),
        //            new(OpCodes.Call, AccessTools.Method(typeof(PartyVMTransferTroop), nameof(FixIndex))),
        //            new(OpCodes.Stloc_S, 5)
        //        };
        //        codes.InsertRange(target, stack);
        //        return codes.AsEnumerable();
        //    }
        //
        //    private static int FixIndex(int index2, PartyScreenLogic.PartyRosterSide index1, PartyVM partyVM)
        //    {
        //        if (index2 >= 0)
        //            return index2;
        //        var troop = partyVM.PartyScreenLogic.MemberRosters[(int)index1].GetTroopRoster().FirstOrDefaultQ(e => e.Character.Name.Equals(partyVM.CurrentCharacter.Character.Name));
        //        var result = -1;
        //        if (troop.Character is not null)
        //            result = partyVM.PartyScreenLogic.MemberRosters[(int)index1].FindIndexOfTroop(troop.Character);
        //        return result;
        //    }
        //}
        //
        //
        //[HarmonyPatch(typeof(PartyTradeVM), "FindTroopFromSide")]
        //public class PartyTradeVMFindTroopsFromSidePatch
        //{
        //    private static void Postfix(CharacterObject character, PartyScreenLogic.PartyRosterSide side, PartyScreenLogic ____partyScreenLogic, ref TroopRosterElement? __result)
        //    {
        //        var roster = ____partyScreenLogic.MemberRosters[(int)side];
        //        __result = (roster?.GetTroopRoster().FirstOrDefaultQ(e => e.Character == character))?.AllSimilar(roster);
        //    }
        //}
        //
        //[HarmonyPatch(typeof(PartyTradeVM), "UpdateTroopData")]
        //public class PartyTradeVMUpdateTroopData
        //{
        //    public static void Prefix(PartyScreenLogic ____partyScreenLogic, bool ____isPrisoner, ref TroopRosterElement troopRoster, PartyScreenLogic.PartyRosterSide side)
        //    {
        //        var roster = ____isPrisoner ? ____partyScreenLogic.PrisonerRosters[(int)side] : ____partyScreenLogic.MemberRosters[(int)side];
        //        var element = troopRoster.AllSimilar(roster).GetValueOrDefault();
        //        troopRoster.Number = element.Number;
        //        troopRoster.WoundedNumber = element.WoundedNumber;
        //    }
        //}
        //
        ////
        ////[HarmonyPatch(typeof(PartyVM), "GetNumberOfHealthyTroopNumberForSide")]
        ////public class PartyVMGetNumberOfHealthyTroopNumberForSide
        ////{
        ////    public static bool Prefix(PartyVM __instance, CharacterObject character,
        ////        PartyScreenLogic.PartyRosterSide fromSide,
        ////        bool isPrisoner,
        ////        ref int __result)
        ////    {
        ////        var characterVm = (PartyCharacterVM)Traverse.Create(__instance).Method("FindCharacterVM", character, fromSide, isPrisoner).GetValue();
        ////        var troop = characterVm.Troop.AllSimilar(__instance.PartyScreenLogic.MemberRosters[(int)fromSide]);
        ////        var number = troop?.Number;
        ////        troop = characterVm.Troop;
        ////        var woundedNumber = troop?.WoundedNumber;
        ////        __result = number!.Value - woundedNumber.Value;
        ////        return false;
        ////    }
        ////}
        //
        //// nerve centre for updating the UI counts
        //[HarmonyPatch(typeof(PartyVM), "UpdateTroopManagerPopUpCounts")]
        //public class PartyVMUpdateTroopManagerPopUpCounts
        //{
        //    public static bool Prefix(PartyVM __instance)
        //    {
        //        if (__instance.UpgradePopUp.IsOpen || __instance.RecruitPopUp.IsOpen)
        //            return false;
        //        __instance.RecruitableTroopCount = 0;
        //        __instance.UpgradableTroopCount = 0;
        //
        //        __instance.MainPartyPrisoners.ApplyActionOnAllItems(x =>
        //        {
        //            // ReSharper disable once PossibleInvalidOperationException
        //            // RecruitableTroopCount +=  summed amount for type
        //            var count = x.Troop.AllSimilar(__instance.PartyScreenLogic.PrisonerRosters[(int)x.Side]).Value.Number;
        //            __instance.RecruitableTroopCount += count;
        //            Log.Debug?.Log($"Prisoners: {x.Troop.Character.Name} {count} = {__instance.RecruitableTroopCount}");
        //        });
        //        //__instance.MainPartyPrisoners.ApplyActionOnAllItems(x => __instance.RecruitableTroopCount += x.NumOfRecruitablePrisoners);
        //        __instance.MainPartyTroops.ApplyActionOnAllItems(x =>
        //        {
        //            // ReSharper disable once PossibleInvalidOperationException
        //            var count = x.Troop.AllSimilar(__instance.PartyScreenLogic.MemberRosters[(int)x.Side]).Value.Number;
        //            __instance.UpgradableTroopCount += count;
        //            Log.Debug?.Log($"Members: {x.Troop.Character.Name} {count} = {__instance.UpgradableTroopCount}");
        //        });
        //        __instance.IsRecruitPopUpDisabled = !__instance.ArePrisonersRelevantOnCurrentMode || __instance.RecruitableTroopCount == 0 || __instance.PartyScreenLogic.IsTroopUpgradesDisabled;
        //        __instance.IsUpgradePopUpDisabled = !__instance.AreMembersRelevantOnCurrentMode || __instance.UpgradableTroopCount == 0 || __instance.PartyScreenLogic.IsTroopUpgradesDisabled;
        //        __instance.RecruitPopUp.UpdateOpenButtonHint(__instance.IsRecruitPopUpDisabled, !__instance.ArePrisonersRelevantOnCurrentMode, __instance.PartyScreenLogic.IsTroopUpgradesDisabled);
        //        __instance.UpgradePopUp.UpdateOpenButtonHint(__instance.IsUpgradePopUpDisabled, !__instance.AreMembersRelevantOnCurrentMode, __instance.PartyScreenLogic.IsTroopUpgradesDisabled);
        //        return false;
        //    }
        //}

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

        internal static Exception Finalizer() => null;
    }
}
