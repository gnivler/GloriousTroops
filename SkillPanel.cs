using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
using static GloriousTroops.Helper;

// ReSharper disable MemberCanBePrivate.Global  
// ReSharper disable InconsistentNaming
#pragma warning disable CS0169

namespace GloriousTroops;

public class SkillPanel : ViewModel
{
    internal GauntletLayer layer = new(580);
    private string BannerCodeText;
    private string CharStringId;
    private string EquipmentCode;
    private string BodyProperties;
    private bool IsFemale;
    private string MountCreationKey;
    private int StanceIndex;
    private uint ArmorColor1;
    private uint ArmorColor2;
    private int Race;
    private bool HasMount;
    private HeroViewModel character;
    private string currentTroop;
    private readonly Dictionary<string, string> troopsIdsAndNames = new();
    private readonly Dictionary<string, List<CharacterObject>> characters = new();
    private int individual;
    private SelectorVM<SelectorItemVM> troops;
    private MBBindingList<EncyclopediaSkillVM> skills = new();
    private SkillObject skill;
    private string skillId;
    private int skillValue;
    private BasicTooltipViewModel hint;
    private string troopCount;

    public SkillPanel()
    {
        if (ScreenManager.TopScreen is not MapScreen)
            return;
        var query = MobileParty.MainParty.MemberRoster.GetTroopRoster()
            .WhereQ(e => e.Character.Name.ToString().StartsWith("Glorious"))
            .SelectQ(e => e.Character);
        foreach (var troop in query)
        {
            var co = MBObjectManager.Instance.GetObject<CharacterObject>(troop.StringId);
            if (!troopsIdsAndNames.ContainsValue(co.Name.ToString()))
                troopsIdsAndNames.Add(co.StringId, $"{co.Name}");
            if (characters.TryGetValue(co.Name.ToString(), out _))
                characters[co.Name.ToString()].Add(co);
            else
                characters.Add(co.Name.ToString(), new List<CharacterObject> { co });
        }

        Skills = new();
        troops = new(troopsIdsAndNames.Values, 0, TroopChanged);
        TroopChanged(troops);
        try
        {
            layer.LoadMovie("SkillPanel", this);
        }
        catch (Exception ex)
        {
            LogException(ex);
        }
    }

    public override void OnFinalize()
    {
        base.OnFinalize();
        layer = null;
    }

    private void Next()
    {
        try
        {
            SetupCharacter(characters[CurrentTroop][Individual++]);
        }
        catch
        {
            //ignore
        }
    }

    private void Previous()
    {
        try
        {
            SetupCharacter(characters[CurrentTroop][Individual--]);
        }
        catch
        {
            //ignore
        }
    }

    public void ExecuteBeginHint() => Hint.ExecuteBeginHint();
    public void ExecuteEndHint() => Hint.ExecuteEndHint();

    private void SetupCharacter(CharacterObject co)
    {
        Character = new HeroViewModel();
        Character.FillFrom(co);
        BannerCodeText = Character.BannerCodeText;
        CharStringId = Character.CharStringId;
        var characterObject = MBObjectManager.Instance.GetObject<CharacterObject>(CharStringId);
        EquipmentCode = Equipments(EquipmentRoster(characterObject))[0].CalculateEquipmentCode();
        BodyProperties = Character.BodyProperties;
        IsFemale = Character.IsFemale;
        StanceIndex = Character.StanceIndex;
        ArmorColor1 = Character.ArmorColor1;
        ArmorColor2 = Character.ArmorColor2;
        Race = Character.Race;
        Character.RefreshValues();
        RefreshSkills();
        TroopCount = MakeTroopCount();
        OnPropertyChanged(nameof(KillCount));
    }

    private void TroopChanged(SelectorVM<SelectorItemVM> selector)
    {
        if (!string.IsNullOrEmpty(selector.SelectedItem?.StringItem))
        {
            var co = MBObjectManager.Instance.GetObject<CharacterObject>(troopsIdsAndNames.ElementAt(selector.SelectedIndex).Key);
            CurrentTroop = troopsIdsAndNames.ElementAt(selector.SelectedIndex).Value;
            SetupCharacter(co);
            RefreshSkills();
            if (Troops is not null)
                Individual = 0;
            TroopCount = MakeTroopCount();
        }
    }

    public void RefreshSkills()
    {
        try
        {
            if (Troops is null)
                return;
            Skills.Clear();
            var co = MBObjectManager.Instance.GetObject<CharacterObject>(characters[CurrentTroop][Individual].StringId);
            foreach (var item in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (co.GetSkillValue(item) is var value and > 0)
                    Skills.Add(new EncyclopediaSkillVM(item, value));
            }
        }
        catch
        {
            //ignore
        }
    }

    private string MakeTroopCount()
    {
        var roster = MobileParty.MainParty.MemberRoster.GetTroopRoster();
        var matchingTroops = roster.WhereQ(e => e.Character.Name.ToString() == CurrentTroop);
        return $"{Individual + 1}/{matchingTroops.SumQ(e => e.Number)}";
    }

    public int Individual
    {
        get => individual;
        set
        {
            if (individual != value)
            {
                if (value >= 0 && value <= characters[Troops.SelectedItem.StringItem].Count - 1)
                    individual = value;
                else if (value > characters[Troops.SelectedItem.StringItem].Count - 1)
                    individual = 0;
                else
                    individual = characters[Troops.SelectedItem.StringItem].Count - 1;
            }
        }
    }

    [DataSourceProperty]
    public SelectorVM<SelectorItemVM> Troops
    {
        get => troops;
        set
        {
            if (troops != value)
            {
                troops = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public HeroViewModel Character
    {
        get => character;
        set
        {
            character = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public string CurrentTroop
    {
        get => currentTroop;
        set
        {
            if (currentTroop != value)
            {
                currentTroop = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<EncyclopediaSkillVM> Skills
    {
        get => skills;
        set
        {
            if (value != skills)
            {
                skills = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public BasicTooltipViewModel Hint
    {
        get => hint;
        set
        {
            if (value != hint)
            {
                hint = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public int SkillValue
    {
        get => skillValue;
        set
        {
            if (value != skillValue)
            {
                skillValue = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public string SkillId
    {
        get => skillId;
        set
        {
            if (value != skillId)
            {
                skillId = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public string TroopCount
    {
        get => troopCount;
        set
        {
            if (troopCount != value)
            {
                troopCount = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public string KillCount
    {
        get
        {
            var co = MBObjectManager.Instance.GetObject<CharacterObject>(Character.CharStringId);
            if (Globals.KillCounters.TryGetValue(co.StringId, out var kills))
                return kills.ToString();
            return "0";
        }
    }
}
