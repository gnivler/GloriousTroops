using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;
using static GloriousTroops.Globals;

// ReSharper disable RedundantAssignment 
// ReSharper disable InconsistentNaming  
// ReSharper disable UnusedMember.Local   
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace GloriousTroops

{
    public static class TroopManagement
    {
        // stops vanilla from removing all COs without a Hero (like, every custom troop)
        [HarmonyPatch(typeof(SandBoxManager), "InitializeCharactersAfterLoad")]
        public static class SandBoxManagerInitializeCharactersAfterLoad
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // just stop the Add so they aren't unregistered
                // it's blind...  but it works for now
                var listAddMethod = AccessTools.Method(typeof(List<CharacterObject>), "Add");
                var codes = instructions.ToList();
                for (var i = 0; i < codes.Count; i++)
                    if (codes[i].opcode == OpCodes.Callvirt && codes[i].OperandIs(listAddMethod))
                        for (var j = -2; j < 1; j++)
                            codes[i + j].opcode = OpCodes.Nop;
                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior), "ApplyEscapeChanceToExceededPrisoners")]
        public static class PrisonerReleaseCampaignBehaviorApplyEscapeChanceToExceededPrisoners
        {
            // if the method rolled that the prisoner escaped, it won't be in the roster anymore, so remove it
            // ReSharper disable once IdentifierTypo
            public static void Postfix(CharacterObject character, MobileParty capturerParty)
            {
                if (!capturerParty.PrisonRoster.Contains(character) && Troops.Contains(character))
                    Helper.RemoveTracking(character, capturerParty.PrisonRoster);
            }
        }

        [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "DailyTickSettlement")]
        public class PartiesSellPrisonerCampaignBehaviorDailyTickSettlement
        {
            private static readonly MethodInfo from = AccessTools.Method(typeof(TroopRoster), nameof(TroopRoster.RemoveTroop));
            private static readonly MethodInfo to = AccessTools.Method(typeof(PartiesSellPrisonerCampaignBehaviorDailyTickSettlement), nameof(RemoveTroop));

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return instructions.MethodReplacer(from, to);
            }

            private static void RemoveTroop(TroopRoster __instance, CharacterObject troop, int numberToRemove = 1, UniqueTroopDescriptor troopSeed = default, int xp = 0)
            {
                if (Troops.ContainsQ(troop))
                    Helper.RemoveTracking(troop, __instance);

                __instance.RemoveTroop(troop, numberToRemove, troopSeed, xp);
            }
        }

        [HarmonyPatch(typeof(DesertionCampaignBehavior), "PartiesCheckDesertionDueToPartySizeExceedsPaymentRatio")]
        public class DesertionCampaignBehaviorPartiesCheckDesertionDueToPartySizeExceedsPaymentRatio
        {
            public static void Prefix(MobileParty mobileParty, ref TroopRoster __state)
            {
                __state = mobileParty.MemberRoster.CloneRosterData();
            }

            public static void Postfix(MobileParty mobileParty, TroopRoster __state)
            {
                foreach (var element in __state.GetTroopRoster().Except(mobileParty.MemberRoster.GetTroopRoster()))
                    if (Troops.ContainsQ(element.Character))
                        Helper.RemoveTracking(element.Character, __state);
            }
        }

        [HarmonyPatch(typeof(TroopRoster), "KillOneManRandomly")]
        public static class LeaveTroopsToSettlementActionApplyInternal
        {
            public static void Prefix(TroopRoster __instance, ref TroopRoster __state)
            {
                __state = __instance.CloneRosterData();
            }

            public static void Postfix(TroopRoster __instance, TroopRoster __state)
            {
                foreach (var element in __state.GetTroopRoster().Except(__instance.GetTroopRoster()))
                    if (Troops.ContainsQ(element.Character))
                        Helper.RemoveTracking(element.Character, __state);
            }
        }

        [HarmonyPatch(typeof(MobilePartyHelper), "DesertTroopsFromParty")]
        public static class MobilePartyHelperDesertTroopsFromParty
        {
            public static void Postfix(TroopRoster desertedTroopList)
            {
                foreach (var troop in desertedTroopList.GetTroopRoster())
                    if (Troops.ContainsQ(troop.Character))
                        Helper.RemoveTracking(troop.Character, desertedTroopList);
            }
        }

        // check if a Glorious troop is being upgraded to a new type and keep any superior gear
        [HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), "UpgradeTroop")]
        public static class PartyUpgraderCampaignBehaviorUpgradeTroop
        {
            public static void Prefix(PartyBase party, int rosterIndex, ref TroopRoster __state)
            {
                if (party.MemberRoster.GetElementCopyAtIndex(rosterIndex).Character.Name.ToString().StartsWith("Glorious"))
                    __state = party.MemberRoster.CloneRosterData();
            }

            public static void Postfix(PartyBase party, TroopRoster __state)
            {
                if (__state is not null)
                {
                    var original = __state.GetTroopRoster().Except(party.MemberRoster.GetTroopRoster()).FirstOrDefault();
                    var upgradeTarget = party.MemberRoster.GetTroopRoster().Except(__state.GetTroopRoster()).FirstOrDefault();
                    if (upgradeTarget.Character is null)
                        for (var index = 0; index < party.MemberRoster.GetTroopRoster().Count; index++)
                        {
                            var element = party.MemberRoster.GetElementCopyAtIndex(index);
                            if (element.Number > __state.GetElementCopyAtIndex(index).Number)
                                upgradeTarget = element;
                        }

                    var usableEquipment = new List<ItemRosterElement>();
                    for (var index = 0; index < Equipment.EquipmentSlotLength; index++)
                    {
                        if (original.Character.Equipment[index].IsEmpty)
                            continue;
                        var item = new ItemRosterElement(original.Character.Equipment[index], 1);
                        usableEquipment.Add(item);
                        EquipmentUpgrading.DoPossibleUpgrade(party, item, ref upgradeTarget, ref usableEquipment);
                    }
                }
            }
        }

        // idea from True Battle Loot
        [HarmonyPatch(typeof(MapEventParty), "OnTroopKilled")]
        public static class MapEventPartyOnTroopKilled
        {
            public static void Postfix(MapEventParty __instance, UniqueTroopDescriptor troopSeed, FlattenedTroopRoster ____roster)
            {
                var troop = ____roster[troopSeed].Troop;
                if (troop == CharacterObject.PlayerCharacter)
                    return;
                for (var index = 0; index < Equipment.EquipmentSlotLength; index++)
                {
                    var item = troop.Equipment[index];
                    if (item.IsEmpty)
                        continue;
                    // always take the ammo
                    if (Rng.Next(0, 101) > Globals.Settings.DropPercent && item.Item.ItemType is not
                            (ItemObject.ItemTypeEnum.Arrows or ItemObject.ItemTypeEnum.Bolts or ItemObject.ItemTypeEnum.Bullets))
                        continue;
                    if (LootRecord.TryGetValue(__instance.Party, out _))
                        LootRecord[__instance.Party].Add(new EquipmentElement(item));
                    else
                        LootRecord.Add(__instance.Party, new List<EquipmentElement> { item });
                }

                if (!troop.IsHero && __instance.Party.IsActive && Troops.ContainsQ(troop))
                {
                    //Log.Debug?.Log($"<<< killed {troop.Name} {troop.StringId}");
                    Helper.RemoveTracking(troop, __instance.Party.MemberRoster);
                }
                else if (!__instance.Party.IsActive)
                    Log.Debug?.Log($"<<< killed {troop.Name} {troop.StringId} but not active party");
            }
        }
    }
}
