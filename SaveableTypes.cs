using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GloriousTroops;

public class SaveableTypes : SaveableCampaignTypeDefiner
{
    protected override void DefineContainerDefinitions()
    {
        base.DefineContainerDefinitions();
        ConstructContainerDefinition(typeof(Dictionary<string, Equipment>));
        ConstructContainerDefinition(typeof(Dictionary<string, CharacterSkills>));
        ConstructContainerDefinition(typeof(Dictionary<PartyBase, FlattenedTroopRoster>));
        ConstructContainerDefinition(typeof(Dictionary<string, int>));
    }
}
