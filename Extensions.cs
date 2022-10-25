using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace GloriousTroops
{
    internal static class Extensions
    {
        private static readonly AccessTools.FieldRef<TroopRoster, List<TroopRosterElement>> Elements = AccessTools.FieldRefAccess<TroopRoster, List<TroopRosterElement>>("_troopRosterElements");

        internal static int CountMounted(this TroopRoster troopRoster)
        {
            return troopRoster.GetTroopRoster().WhereQ(t => !t.Character.FirstBattleEquipment[10].IsEmpty).SumQ(t => t.Number);
        }

        internal static float Value(this EquipmentElement element)
        {
            return element.Item?.Tierf ?? element.ItemValue;
        }

        internal static TroopRosterElement? GetNewAggregateTroopRosterElement(this TroopRosterElement element, TroopRoster roster)
        {
            if (element.Character is null)
                return null;
            if (element.Character.IsHero || element.Character.OriginalCharacter is null || !element.Character.Name.ToString().StartsWith("Glorious"))
                return element;
            var sumAlive = Elements(roster).WhereQ(e => e.Character.Name.Equals(element.Character.Name)).SumQ(t => t.Number);
            var sumWounded = Elements(roster).WhereQ(e => e.Character.Name.Equals(element.Character.Name)).SumQ(t => t.WoundedNumber);
            var sumXp = Elements(roster).WhereQ(e => e.Character.Name.Equals(element.Character.Name)).SumQ(t => t.Xp);
            var result = new TroopRosterElement(element.Character)
            {
                Number = sumAlive,
                WoundedNumber = sumWounded,
                Xp = sumXp
            };
            return result;
        }

        internal static (int, int) GetAggregateNumber(this TroopRoster roster, CharacterObject character)
        {
            if (!character.Name.ToString().StartsWith("Glorious"))
            {
                var originalElement = roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(character));
                return new ValueTuple<int, int>(originalElement.Number, originalElement.WoundedNumber);
            }

            var element = roster.GetElementCopyAtIndex(Helper.FindIndexOrSimilarIndex(roster, character)).GetNewAggregateTroopRosterElement(roster);
            return new ValueTuple<int, int>(element!.GetValueOrDefault().Number, element!.GetValueOrDefault().WoundedNumber);
        }

        internal static CharacterObject FindSimilarCharacterWithXpNotAtIndex(this CharacterObject character, TroopRoster roster, int upgradeCost, int index)
        {
            return roster.GetTroopRoster().FirstOrDefaultQ(e => e.Character.Name.Equals(character.Name) && e.Xp >= upgradeCost).Character;
        }
    }
}
