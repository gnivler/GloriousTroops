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

        internal static bool Contains(this Equipment equipment, EquipmentElement element)
        {
            for (var index = 0; index < Equipment.EquipmentSlotLength; index++)
            {
                if (equipment[index].Item?.StringId == element.Item?.StringId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
