using System.Collections.Generic;
using HarmonyLib;
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
            if (element.Character.IsHero || element.Character.OriginalCharacter is null)
                return element;
            var sumAlive = Elements(roster).WhereQ(e => e.Character.Name.Equals(element.Character.Name)).SumQ(t => t.Number);
            var sumWounded = Elements(roster).WhereQ(e => e.Character.Name.Equals(element.Character.Name)).SumQ(t => t.WoundedNumber);
            var result = new TroopRosterElement(element.Character)
            {
                Number = sumAlive,
                WoundedNumber = sumWounded
            };
            return result;
        }
    }
}
