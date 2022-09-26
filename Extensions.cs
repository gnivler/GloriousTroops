using System;
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

        internal static int Value(this EquipmentElement element)
        {
            return Convert.ToInt32(element.Item?.Tierf ?? element.ItemValue);
        }
    }
}
