using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.Scoreboard;
using TaleWorlds.ObjectSystem;
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
        [HarmonyPatch(typeof(MapEventParty), "OnTroopScoreHit")]
        public class MapEventPartyOnTroopScoreHit
        {
            public static void Postfix(MapEventParty __instance, UniqueTroopDescriptor attackerTroopDesc, bool isFatal, bool isTeamKill, FlattenedTroopRoster ____roster)
            {
                if (isFatal && !isTeamKill)
                {
                    var party = __instance.Party;
                    var troop = ____roster[attackerTroopDesc];
                    if (TroopKills.TryGetValue(party, out _))
                        Globals.TroopKills[party].Add(troop.Troop, 1);
                    else
                    {
                        var roster = new FlattenedTroopRoster();
                        roster.Add(troop.Troop, 1);
                        Globals.TroopKills.Add(party, roster);
                    }

                    // uses more memory but matching only player troops is expensive
                    if (KillCounters.TryGetValue(troop.Troop.StringId, out _))
                        KillCounters[troop.Troop.StringId]++;
                    else
                        KillCounters.Add(troop.Troop.StringId, 1);
                }
            }
        }

        [HarmonyPatch(typeof(BattleCampaignBehavior), "CollectLoots")]
        public static class BattleCampaignBehaviorCollectLoots
        {
            public static void Prefix(MapEvent mapEvent, PartyBase party)
            {
                if (!mapEvent.HasWinner || !party.IsMobile)
                    return;
                var loserParties = mapEvent.PartiesOnSide(party.OpponentSide);
                var itemRoster = new ItemRoster();
                foreach (var loserParty in loserParties)
                {
                    if (LootRecord.TryGetValue(loserParty.Party, out var equipment))
                        foreach (var e in equipment)
                            itemRoster.AddToCounts(e, 1);
                }

                if (!itemRoster.Any())
                    return;
                var shuffledLoot = itemRoster.WhereQ(e => e.EquipmentElement.Item is { } itemObject && itemObject.Value >= Globals.Settings.MinLootValue).ToListQ();
                shuffledLoot.Shuffle();
                var lootValue = shuffledLoot.SumQ(e => e.EquipmentElement.Value() * e.Amount);
                var winnerParties = mapEvent.PartiesOnSide(party.Side).ToListQ();
                var shares = new Dictionary<MapEventParty, List<ItemRosterElement>>();
                var totalContributionValue = Traverse.Create(winnerParties[0].Party.MapEventSide).Method("CalculateTotalContribution").GetValue<int>();
                winnerParties.Shuffle();
                foreach (var mapEventParty in winnerParties)
                {
                    var contribution = (float)mapEventParty.ContributionToBattle / totalContributionValue;
                    for (var index = 0; index < shuffledLoot.Count; index++)
                    {
                        var item = shuffledLoot[index];
                        var lootPercentage = 0f;
                        if (shares.TryGetValue(mapEventParty, out var loot))
                        {
                            lootPercentage = loot.SumQ(e => e.EquipmentElement.Value() * e.Amount) / lootValue;
                            if (lootPercentage <= contribution)
                            {
                                shares[mapEventParty].Add(new(item.EquipmentElement, 1));
                                lootPercentage = shares[mapEventParty].SumQ(e => e.EquipmentElement.Value() * e.Amount) / lootValue;
                                if (--item.Amount == 0)
                                    shuffledLoot.RemoveAt(index);
                            }
                        }
                        else
                        {
                            shares.Add(mapEventParty, new List<ItemRosterElement> { new(item.EquipmentElement, 1) });
                            lootPercentage = item.EquipmentElement.Value() / lootValue;
                            if (--item.Amount == 0)
                                shuffledLoot.RemoveAt(index);
                        }

                        if (lootPercentage > contribution)
                            break;
                    }
                }

                winnerParties.Shuffle();
                foreach (var mapEventParty in winnerParties)
                {
                    for (var index = 0; index < shuffledLoot.Count; index++)
                    {
                        var item = shuffledLoot[index];
                        shares[mapEventParty].Add(new(item.EquipmentElement, 1));
                        if (--item.Amount == 0)
                            shuffledLoot.RemoveAt(index);
                        if (shuffledLoot.Count == 0)
                            break;
                    }
                }

                foreach (var mapEventParty in winnerParties)
                {
                    if (shares.TryGetValue(mapEventParty, out _))
                    {
                        itemRoster = new ItemRoster();
                        foreach (var e in shares[mapEventParty])
                            itemRoster.Add(new ItemRosterElement(e.EquipmentElement, 1));
                        EquipmentUpgrading.UpgradeEquipment(mapEventParty.Party, itemRoster);
                    }
                }

                var parties = mapEvent.InvolvedParties;
                foreach (var partyBase in parties)
                    LootRecord.Remove(partyBase);
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
                if (element.Character.IsHero || !element.Character.Name.ToString().StartsWith("Glorious"))
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
                    if (troop.Character.IsHero || !troop.Character.Name.ToString().StartsWith("Glorious"))
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
                    if (character.IsHero || !character.Name.ToString().StartsWith("Glorious"))
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
        [HarmonyPatch(typeof(PartyScreenLogic), "ValidateCommand")]
        public class PartyScreenLogicValidateCommand
        {
            public static bool Prefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, ref bool __result, Game ____game)
            {
                var roster = __instance.MemberRosters[(int)command.RosterSide];
                var prisonRoster = __instance.PrisonerRosters[(int)command.RosterSide];
                if (command.Code == PartyScreenLogic.PartyCommandCode.TransferTroop || command.Code == PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot)
                {
                    var character = command.Character;
                    var tuple = new ValueTuple<int, int>();
                    if (character == CharacterObject.PlayerCharacter)
                        return false;
                    if (command.Type == PartyScreenLogic.TroopType.Member)
                    {
                        var indexOfTroop = FindIndexOrSimilarIndex(roster, character, command.WoundedNumber > 0);
                        tuple = roster.GetAggregateNumber(character);
                        // I did not write this!
                        __result = ((indexOfTroop == -1
                                        ? 0
                                        : tuple.Item1 + tuple.Item2 >= command.TotalNumber
                                            ? 1
                                            : 0)
                                    & (command.RosterSide != PartyScreenLogic.PartyRosterSide.Left
                                        ? 1
                                        : command.Index != 0
                                            ? 1
                                            : 0)) != 0;
                        return false;
                    }

                    var indexOfTroop1 = FindIndexOrSimilarIndex(prisonRoster, character);
                    tuple = prisonRoster.GetAggregateNumber(character);
                    __result = indexOfTroop1 != -1 && tuple.Item1 + tuple.Item2 >= command.TotalNumber;
                    return false;
                }

                if (command.Code == PartyScreenLogic.PartyCommandCode.ShiftTroop)
                {
                    var character = command.Character;
                    // wtf is with these ternaries
                    if ((character == __instance.LeftPartyLeader || character == __instance.RightPartyLeader
                            ? 0
                            : command.RosterSide != PartyScreenLogic.PartyRosterSide.Left || __instance.LeftPartyLeader != null
                            && command.Index == 0
                                ? command.RosterSide != PartyScreenLogic.PartyRosterSide.Right
                                    ? 0
                                    : __instance.RightPartyLeader == null
                                        ? 1
                                        : command.Index != 0
                                            ? 1
                                            : 0
                                : 1) == 0)

                    {
                        __result = false;
                        return false;
                    }

                    if (command.Type == PartyScreenLogic.TroopType.Member)
                    {
                        var indexOfTroop = FindIndexOrSimilarIndex(roster, character);
                        return indexOfTroop != -1 && indexOfTroop != command.Index;
                    }

                    var indexOfTroop2 = FindIndexOrSimilarIndex(prisonRoster, character);
                    __result = indexOfTroop2 != -1 && indexOfTroop2 != command.Index;
                    return false;
                }

                if (command.Code == PartyScreenLogic.PartyCommandCode.TransferPartyLeaderTroop)
                {
                    __result = false;
                    return false;
                }

                if (command.Code == PartyScreenLogic.PartyCommandCode.UpgradeTroop)
                {
                    var character = command.Character;
                    var indexOfTroop = FindIndexOrSimilarIndex(roster, character);
                    var tuple = roster.GetAggregateNumber(character);
                    if (indexOfTroop == -1 || tuple.Item1 + tuple.Item2 < command.TotalNumber || character.UpgradeTargets.Length == 0)
                    {
                        __result = false;
                        return false;
                    }

                    if (command.UpgradeTarget < character.UpgradeTargets.Length)
                    {
                        var upgradeTarget = character.UpgradeTargets[command.UpgradeTarget];
                        var upgradeXpCost = character.GetUpgradeXpCost(PartyBase.MainParty, command.UpgradeTarget);
                        var upgradeGoldCost = character.GetUpgradeGoldCost(PartyBase.MainParty, command.UpgradeTarget);
                        if (GetSimilarElementXp(roster, indexOfTroop) >= upgradeXpCost * command.TotalNumber)
                        {
                            var gold = (command.RosterSide == PartyScreenLogic.PartyRosterSide.Left ? __instance.LeftPartyLeader : __instance.RightPartyLeader)?.HeroObject.Gold;
                            var goldChangeAmount = __instance.CurrentData.PartyGoldChangeAmount;
                            var nullable = gold + goldChangeAmount;
                            var num = upgradeGoldCost * command.TotalNumber;
                            if (nullable.GetValueOrDefault() >= num & nullable.HasValue)
                            {
                                if (upgradeTarget.UpgradeRequiresItemFromCategory == null)
                                {
                                    __result = true;
                                    return false;
                                }

                                foreach (var itemRosterElement in __instance.RightOwnerParty.ItemRoster)
                                {
                                    if (itemRosterElement.EquipmentElement.Item.ItemCategory == upgradeTarget.UpgradeRequiresItemFromCategory)
                                    {
                                        __result = true;
                                        return false;
                                    }
                                }

                                MBTextManager.SetTextVariable("REQUIRED_ITEM", upgradeTarget.UpgradeRequiresItemFromCategory.GetName());
                                MBInformationManager.AddQuickInformation(GameTexts.FindText("str_item_needed_for_upgrade"));

                                __result = false;
                                return false;
                            }

                            MBTextManager.SetTextVariable("VALUE", upgradeGoldCost);
                            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_gold_needed_for_upgrade"));
                            __result = false;
                            return false;
                        }

                        MBInformationManager.AddQuickInformation(new TextObject("{=m1bIfPf1}Character does not have enough experience for upgrade."));
                        __result = false;
                        return false;
                    }

                    MBInformationManager.AddQuickInformation(new TextObject("{=kaQ7DsW3}Character does not have upgrade target."));
                    __result = false;
                    return false;
                }

                if (command.Code == PartyScreenLogic.PartyCommandCode.RecruitTroop)
                {
                    __result = __instance.IsPrisonerRecruitable(command.Type, command.Character, command.RosterSide);
                    return false;
                }

                if (command.Code == PartyScreenLogic.PartyCommandCode.ExecuteTroop)
                {
                    __result = __instance.IsExecutable(command.Type, command.Character, command.RosterSide);
                    return false;
                }

                ;
                throw new MBUnknownTypeException("Unknown command type in ValidateCommand.");
            }
        }

        // modified assembly copy 1.8.1
        [HarmonyPatch(typeof(PartyScreenLogic), "UpgradeTroop")]
        public class PartyScreenLogicUpgradeTroop
        {
            public static bool Prefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command)
            {
                if (!__instance.ValidateCommand(command))
                    return false;
                var character = command.Character;
                var upgradeTarget = character.UpgradeTargets[command.UpgradeTarget];
                var roster = __instance.MemberRosters[(int)command.RosterSide];
                // switch the command character for a character which has enough xp to level
                // we wouldn't have landed in this method if at least ONE character didn't have enough xp
                var maxAkaLevellingXp = roster.GetTroopRoster().WhereQ(e => e.Character != null && e.Character.Name.Equals(character.Name)).MaxQ(e => e.Xp);
                character = roster.GetTroopRoster().FirstOrDefaultQ(e => e.Character != null && e.Character.Name.Equals(character.Name) && e.Xp == maxAkaLevellingXp).Character;
                var indexOfTroop = roster.FindIndexOfTroop(character);
                var upgradeCost = character.GetUpgradeXpCost(PartyBase.MainParty, command.UpgradeTarget);
                var num = upgradeCost * command.TotalNumber;
                roster.SetElementXp(indexOfTroop, roster.GetElementXp(indexOfTroop) - num);
                var usedHorses = (List<(EquipmentElement, int)>)null;
                Traverse.Create(__instance).Method("SetPartyGoldChangeAmount", __instance.CurrentData.PartyGoldChangeAmount - character.GetUpgradeGoldCost(PartyBase.MainParty, command.UpgradeTarget) * command.TotalNumber).GetValue();
                if (upgradeTarget.UpgradeRequiresItemFromCategory != null)
                    usedHorses = (List<(EquipmentElement, int)>)Traverse.Create(__instance).Method("RemoveItemFromItemRoster", upgradeTarget.UpgradeRequiresItemFromCategory, command.TotalNumber).GetValue();
                var woundedCount = 0;
                foreach (var troopRosterElement in roster.GetTroopRoster())
                {
                    var tuple = roster.GetAggregateNumber(character);
                    if (troopRosterElement.Character == character && command.TotalNumber > tuple.Item1 - tuple.Item2)
                        woundedCount = Math.Max(0, command.TotalNumber - tuple.Item1 - tuple.Item2);
                }

                if (character.Name.ToString().StartsWith("Glorious"))
                {
                    for (var i = 0; i < command.TotalNumber; i++)
                    {
                        var co = roster.GetTroopRoster().WhereQ(e =>
                                     e.Character.StringId != character.StringId
                                     && e.Character.Name.Equals(character.Name)
                                     && e.Xp >= upgradeCost).FirstOrDefault().Character
                                 ?? character;
                        var party = FindParties(co).First();
                        var original = new TroopRosterElement(co) { Number = 1, WoundedNumber = woundedCount };
                        var upgrade = new TroopRosterElement(upgradeTarget) { Number = 1, WoundedNumber = woundedCount };
                        try
                        {
                            DoStripUpgrade(party, original, upgrade);
                        }
                        catch (Exception ex)
                        {
                            LogException(ex);
                        }
                    }
                }
                else
                {
                    roster.AddToCounts(character, -command.TotalNumber, woundedCount: -woundedCount);
                    roster.AddToCounts(upgradeTarget, command.TotalNumber, woundedCount: woundedCount);
                }

                Traverse.Create(__instance).Method("AddUpgradeToHistory", character, upgradeTarget, command.TotalNumber).GetValue();
                Traverse.Create(__instance).Method("AddUsedHorsesToHistory", usedHorses).GetValue();
                var updateDelegate = __instance.UpdateDelegate;
                updateDelegate?.Invoke(command);
                return false;
            }
        }

        // adapted from 1.0.0
        [HarmonyPatch(typeof(PartyVM), "UpgradeTroop")]
        public class PartyVMUpgradeTroop
        {
            public static bool Prefix(PartyVM __instance, PartyScreenLogic.PartyCommand command,
                string ____fiveStackShortcutkeyText, string ____entireStackShortcutkeyText,
                PartyCharacterVM ____currentCharacter)
            {
                try
                {
                    var commandRoster = __instance.PartyScreenLogic.MemberRosters[(int)command.RosterSide];
                    var indexOfTroop = FindIndexOrSimilarIndex(commandRoster, command.Character.UpgradeTargets[command.UpgradeTarget]);
                    TroopRosterElement element = default;
                    // training a troop that winds up Glorious from equipment, fails on the command.UpgradeTarget's name
                    if (indexOfTroop == -1)
                    {
                        var searchName = $"Glorious {command.Character.UpgradeTargets[command.UpgradeTarget].Name}";
                        element = commandRoster.GetTroopRoster().FirstOrDefaultQ(e => e.Character.Name.ToString() == searchName);
                        indexOfTroop = commandRoster.GetTroopRoster().IndexOf(element);
                    }

                    var newCharacter = new PartyCharacterVM(__instance.PartyScreenLogic, ProcessCharacterLock, SetSelectedCharacter, OnTransferTroop,
                        null, OnFocusCharacter, __instance, commandRoster, indexOfTroop, command.Type, command.RosterSide,
                        __instance.PartyScreenLogic.IsTroopTransferable(command.Type, commandRoster.GetCharacterAtIndex(indexOfTroop), (int)command.RosterSide),
                        ____fiveStackShortcutkeyText, ____entireStackShortcutkeyText);
                    newCharacter.IsLocked = Traverse.Create(__instance).Method("IsTroopLocked", newCharacter.Troop, false).GetValue<bool>();
                    MBBindingList<PartyCharacterVM> list = new();
                    var args = new object[] { list, command.RosterSide, command.Type };
                    AccessTools.Method(__instance.GetType(), "GetPartyCharacterVMList").Invoke(__instance, args);
                    list = (MBBindingList<PartyCharacterVM>)args[0];
                    if (list.Contains(newCharacter))
                    {
                        var partyCharacterVm = list.First(character => character.Equals(newCharacter));
                        partyCharacterVm.Troop = newCharacter.Troop;
                        partyCharacterVm.ThrowOnPropertyChanged();
                    }
                    else
                    {
                        list.Add(newCharacter);
                        newCharacter.ThrowOnPropertyChanged();
                    }

                    var index = -1;
                    var currentSideRoster = __instance.PartyScreenLogic.MemberRosters[(int)__instance.CurrentCharacter.Side];
                    var currentSidePrisonerRoster = __instance.PartyScreenLogic.MemberRosters[(int)__instance.CurrentCharacter.Side];
                    if (command.Type == PartyScreenLogic.TroopType.Member)
                    {
                        index = FindIndexOrSimilarIndex(currentSideRoster, __instance.CurrentCharacter.Character); //currentSideRoster.FindIndexOfTroop(__instance.CurrentCharacter.Character);

                        if (index > 0)
                            ____currentCharacter.Troop = GetSimilarElementCopy(currentSideRoster, index);
                    }
                    else if (command.Type == PartyScreenLogic.TroopType.Prisoner)
                    {
                        index = FindIndexOrSimilarIndex(currentSidePrisonerRoster, __instance.CurrentCharacter.Character); // currentSidePrisonerRoster.FindIndexOfTroop(__instance.CurrentCharacter.Character);
                        if (index > 0)
                            ____currentCharacter.Troop = GetSimilarElementCopy(currentSidePrisonerRoster, index);
                    }

                    if (index < 0)
                    {
                        __instance.UpgradePopUp.OnRanOutTroop(__instance.CurrentCharacter);
                        list.Remove(__instance.CurrentCharacter);
                        __instance.CurrentCharacter = newCharacter;
                        MBInformationManager.HideInformations();
                    }
                    else
                    {
                        __instance.CurrentCharacter.InitializeUpgrades();
                        __instance.CurrentCharacter.ThrowOnPropertyChanged();
                    }

                    __instance.CurrentCharacter?.UpdateTradeData();
                    Game.Current.EventManager.TriggerEvent(new PlayerRequestUpgradeTroopEvent(command.Character, element.Character, command.TotalNumber));
                    Traverse.Create(__instance).Method("RefreshTopInformation").GetValue();
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }

                return false;
            }

            private static TroopRosterElement GetSimilarElementCopy(TroopRoster roster, int index)
            {
                return roster.GetElementCopyAtIndex(index).GetNewAggregateTroopRosterElement(roster).GetValueOrDefault();
            }
        }

        [HarmonyPatch(typeof(PartyCharacterVM), "InitializeUpgrades")]
        public class PartyCharacterVMInitializeUpgrades
        {
            public static bool Prefix(PartyCharacterVM __instance, PartyScreenLogic ____partyScreenLogic, PartyVM ____partyVm, string ____entireStackShortcutKeyText, string ____fiveStackShortcutKeyText)
            {
                if (__instance.Side == PartyScreenLogic.PartyRosterSide.Right && !__instance.Character.IsHero && __instance.Character.UpgradeTargets.Length != 0 && !__instance.IsPrisoner && !____partyScreenLogic.IsTroopUpgradesDisabled)
                {
                    for (var i = 0; i < __instance.Character.UpgradeTargets.Length; i++)
                    {
                        var characterObject = __instance.Character.UpgradeTargets[i];
                        var flag = false;
                        var flag2 = false;
                        var num = 0;
                        var level = characterObject.Level;
                        var upgradeGoldCost = __instance.Character.GetUpgradeGoldCost(PartyBase.MainParty, i);
                        var flag3 = Campaign.Current.Models.PartyTroopUpgradeModel.DoesPartyHaveRequiredPerksForUpgrade(PartyBase.MainParty, __instance.Character, characterObject, out var requiredPerk);
                        var b = flag3 ? __instance.Troop.Number : 0;
                        var flag4 = true;
                        var numOfCategoryItemPartyHas = __instance.GetNumOfCategoryItemPartyHas(____partyScreenLogic.RightOwnerParty.ItemRoster, characterObject.UpgradeRequiresItemFromCategory);
                        if (characterObject.UpgradeRequiresItemFromCategory != null)
                            flag4 = numOfCategoryItemPartyHas > 0;
                        var flag5 = Hero.MainHero.Gold + ____partyScreenLogic.CurrentData.PartyGoldChangeAmount >= upgradeGoldCost;
                        // edit 1
                        flag = level >= __instance.Character.Level && __instance.Troops.GetTroopRoster().AnyQ(e => e.Xp >= __instance.Character.GetUpgradeXpCost(PartyBase.MainParty, i)) && !____partyVm.PartyScreenLogic.IsTroopUpgradesDisabled;
                        flag2 = !(flag4 && flag5);
                        var a = __instance.Troop.Number;
                        if (upgradeGoldCost > 0)
                            a = (int)MathF.Clamp(MathF.Floor((Hero.MainHero.Gold + ____partyScreenLogic.CurrentData.PartyGoldChangeAmount) / (float)upgradeGoldCost), 0f, __instance.Troop.Number);
                        var b2 = characterObject.UpgradeRequiresItemFromCategory != null ? numOfCategoryItemPartyHas : __instance.Troop.Number;
                        // edit 2
                        // not so Glorious troops
                        int num2;
                        if (!__instance.Character.Name.ToString().StartsWith("Glorious"))
                            num2 = flag ? (int)MathF.Clamp(MathF.Floor(__instance.Troop.Xp / (float)__instance.Character.GetUpgradeXpCost(PartyBase.MainParty, i)), 0f, __instance.Troop.Number) : 0;
                        else
                            num2 = flag
                                ? (int)MathF.Clamp(__instance.Troops.GetTroopRoster().CountQ(e =>
                                    e.Character.Name.Equals(__instance.Character.Name)
                                    && e.Xp >= (float)__instance.Character.GetUpgradeXpCost(PartyBase.MainParty, i)), 0f, __instance.Troop.Number)
                                : 0;
                        num = MathF.Min(MathF.Min(a, b2), MathF.Min(num2, b));
                        if (__instance.Character.Culture.IsBandit)
                        {
                            flag2 = flag2 || !Campaign.Current.Models.PartyTroopUpgradeModel.CanPartyUpgradeTroopToTarget(PartyBase.MainParty, __instance.Character, characterObject);
                            num = flag ? num : 0;
                        }

                        // edit 3
                        flag = num > 0;
                        var upgradeHint = CampaignUIHelper.GetUpgradeHint(i, numOfCategoryItemPartyHas, num, upgradeGoldCost, flag3, requiredPerk, __instance.Character, __instance.Troop, ____partyScreenLogic.CurrentData.PartyGoldChangeAmount, ____entireStackShortcutKeyText, ____fiveStackShortcutKeyText);
                        __instance.Upgrades[i].Refresh(num, upgradeHint, flag, flag2, flag4, flag3);
                        if (i == 0)
                        {
                            __instance.UpgradeCostText = upgradeGoldCost.ToString();
                            __instance.HasEnoughGold = flag5;
                            __instance.NumOfReadyToUpgradeTroops = num2;
                            __instance.MaxXP = __instance.Character.GetUpgradeXpCost(PartyBase.MainParty, i);
                            // edit 4
                            var mostXpOfAnyTroop = __instance.Troops.GetTroopRoster().WhereQ(e => e.Character.Name.Equals(__instance.Character.Name)).MaxQ(e => e.Xp);
                            var troopWithMostXp = __instance.Troops.GetTroopRoster().First(e => e.Character.Name.Equals(__instance.Character.Name) && e.Xp == mostXpOfAnyTroop);
                            __instance.CurrentXP = troopWithMostXp.Xp >= __instance.MaxXP ? __instance.MaxXP : troopWithMostXp.Xp % __instance.MaxXP;
                        }
                    }

                    __instance.AnyUpgradeHasRequirement = __instance.Upgrades.Any(x => x.Requirements.HasItemRequirement || x.Requirements.HasPerkRequirement);
                }

                var num3 = 0;
                foreach (var upgrade in __instance.Upgrades)
                {
                    if (upgrade.AvailableUpgrades > num3)
                    {
                        num3 = upgrade.AvailableUpgrades;
                    }
                }

                __instance.NumOfUpgradeableTroops = num3;
                __instance.IsTroopUpgradable = __instance.NumOfUpgradeableTroops > 0 && !____partyVm.PartyScreenLogic.IsTroopUpgradesDisabled;
                GameTexts.SetVariable("LEFT", __instance.NumOfReadyToUpgradeTroops);
                GameTexts.SetVariable("RIGHT", __instance.Troop.Number);
                __instance.StrNumOfUpgradableTroop = GameTexts.FindText("str_LEFT_over_RIGHT").ToString();
                __instance.OnPropertyChanged("AmountOfUpgrades");
                return false;
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
                    if (!____currentCharacter.Character.IsHero && !____currentCharacter.Character.Name.ToString().StartsWith("Glorious"))
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
            // if __instance troop doesn't appear anywhere, generate all the missing ones so it can complete
            if (!__instance.Parties.AnyQ(s => s.Members.AnyQ(m => m.Character == troop)))
            {
                foreach (var partyVm in __instance.Parties)
                {
                    var roster = ((PartyBase)partyVm.BattleCombatant).MemberRoster;
                    foreach (var member in roster.GetTroopRoster())
                    {
                        if (member.Character.Name.ToString().StartsWith("Glorious"))
                            __instance.AddTroop(partyVm.BattleCombatant, member.Character, new SPScoreboardStatsVM(new TextObject()));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BasicCharacterObject), "GetSkillValue")]
        public class BasicCharacterObjectGetSkillValue
        {
            public static bool Prefix(BasicCharacterObject __instance, SkillObject skill, ref int __result)
            {
                if (skill is null)
                {
                    __result = 0;
                    return false;
                }

                if (SkillsMap.TryGetValue(__instance.StringId, out var skills))
                {
                    EquipmentUpgrading.Attributes(skills).TryGetValue(skill, out __result);
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(BasicCharacterObject), "GetSkillValue")]
        public class CharacterObjectGetSkillValue
        {
            public static Exception Finalizer(BasicCharacterObject __instance, SkillObject skill, Exception __exception)
            {
                if (__exception is not null)
                {
                    Debug.DebugManager.PrintWarning($"GloriousTroops caught the game checking for {skill.Name} on {__instance.Name} {__instance.StringId}, which will crash.");
                    return null;
                }

                return __exception;
            }
        }

        // caravans pull straight COs, so we have to stop them pulling GTs
        [HarmonyPatch(typeof(CaravanPartyComponent), "InitializeCaravanOnCreation")]
        public class CaravanPartyComponentInitializeCaravanOnCreation
        {
            public static void Postfix(MobileParty mobileParty, Hero caravanLeader)
            {
                if (caravanLeader is null)
                {
                    var caravanCharacters = CharacterObject.All.WhereQ(c => c.Occupation == Occupation.CaravanGuard && c.IsInfantry && c.Level == 26);
                    ;
                    var troop = mobileParty.MemberRoster.GetTroopRoster().FirstOrDefaultQ(e => e.Character.Name.ToString().StartsWith("Glorious"));
                    if (troop.Character is not null)
                    {
                        var leader = caravanCharacters.FirstOrDefaultQ(c => c.Culture == mobileParty.Party.Owner.Culture && !c.Name.ToString().StartsWith("Glorious"));
                        if (leader is null)
                            Debugger.Break();
                        mobileParty.MemberRoster.RemoveTroop(troop.Character);
                        mobileParty.MemberRoster.AddToCounts(leader, 1, true);
                    }
                }
            }
        }
    }
}

//
// [HarmonyPatch(typeof(MBObjectManager), "UnregisterObject")]
// public class MBObjectManagerUnregisterObject
// {
//     public static void Prefix(MBObjectBase obj)
//     {
//         // if (obj is CharacterObject characterObject)
//         // {
//         //     Log.Debug?.Log($"*** Unregistering {characterObject.StringId} {characterObject.Name}");
//         //     var stack = new StackTrace().GetFrames().Take(5).Select(f => f.GetMethod()?.FullDescription());
//         //     stack.Do(x => Log.Debug?.Log(x));
//         //     if (characterObject.Name.Contains("Caravan"))
//         //     {
//         //     }
//         // }
//     }
// }
//
// public class MBObjectManagerRegisterObject
// {
//     public static Exception Finalizer(CharacterObject obj)
//     {
//         Log.Debug?.Log("PING");
//         Log.Debug?.Log($"*** Registering {obj.StringId} {obj.Name}");
//         var stack = new StackTrace().GetFrames().Take(5).Select(f => f.GetMethod()?.FullDescription());
//         stack.Do(x => Log.Debug?.Log(x));
//         return null;
//     }
// }
//
// [HarmonyPatch(typeof(TroopRoster), "AddToCounts")]
// public class TroopRosterAddToCounts
// {
//     public static void Prefix(TroopRoster __instance, CharacterObject character)
//     {
//         // var stack = new StackTrace().GetFrames().Take(5).Select(f => f.GetMethod()?.FullDescription());
//         // if (stack.AnyQ(s => s.Contains("TooltipParty")))
//         //     return;
//         // if (character.Name is null)
//         // {
//         //     Log.Debug?.Log("\n");
//         //     stack.Do(x => Log.Debug?.Log(x));
//         //     return;
//         // }
//         //
//         // if (character.Name.Contains("Caravan"))
//         // {
//         // Log.Debug?.Log("\n");
//         // stack.Do(x => Log.Debug?.Log(x));
//         // }
//     }
// }
// public class SaveContextCollectObjects
// {
//     public static void Postfix(object __instance)
//     {
//         var childObjects = Traverse.Create(__instance).Field<List<object>>("_childObjects").Value;
//         var childObjectIds = Traverse.Create(__instance).Field<Dictionary<object, int>>("_idsOfChildObjects").Value;
//         var childContainers = Traverse.Create(__instance).Field<List<object>>("_childContainers").Value;
//         var childContainersIds = Traverse.Create(__instance).Field<Dictionary<object, int>>("_idsOfChildContainers").Value;
//         for (var i = 60_000; i < childObjects.Count; i++) // aim high to avoid wiping too many
//         {
//             if (childObjects[i] is CharacterSkills cs)
//             {
//                 childObjects.RemoveAt(i--);
//                 childObjectIds.Remove(childObjectIds.ElementAt(i));
//                 cs = null;
//             }
//         }
//
//         for (var i = 40_000; i < childContainers.Count; i++)
//         {
//             if (childContainers[i] is Dictionary<SkillObject, int> container)
//             {
//                 childContainers.RemoveAt(i--);
//                 childContainersIds.Remove(childContainersIds.ElementAt(i));
//                 container = null;
//             }
//         }
//     }
