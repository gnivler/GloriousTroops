using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace UniqueTroopsGoneWild
{
    internal static class Extensions
    {
        internal static int CountMounted(this TroopRoster troopRoster)
        {
            return troopRoster.GetTroopRoster().WhereQ(t => !t.Character.FirstBattleEquipment[10].IsEmpty).SumQ(t => t.Number);
        }

        internal static float Value(this EquipmentElement element)
        {
            return element.Item?.Tierf ?? element.ItemValue;
        }

        internal static TroopRosterElement? AllSimilar(this TroopRosterElement element, TroopRoster roster)
        {
            if (element.Character is null)
            {
                return null;
            }
            var sumAlive = roster.GetTroopRoster().WhereQ(e => e.Character.Name.Equals(element.Character.Name)).SumQ(t => t.Number);
            var sumWounded = roster.GetTroopRoster().WhereQ(e => e.Character.Name.Equals(element.Character.Name)).SumQ(t => t.WoundedNumber);
            var result = new TroopRosterElement(element.Character)
            {
                Number = sumAlive,
                WoundedNumber = sumWounded
            };
            return result;
        }
    }
}
