using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.LinQuick;

namespace UniqueTroopsGoneWild
{
    internal static class Extensions
    {
        internal static int CountMounted(this TroopRoster troopRoster)
        {
            return troopRoster.GetTroopRoster().WhereQ(t => !t.Character.FirstBattleEquipment[10].IsEmpty).SumQ(t => t.Number);
        }
    }
}
