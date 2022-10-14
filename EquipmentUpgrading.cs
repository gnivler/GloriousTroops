using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
using static GloriousTroops.Globals;
using static GloriousTroops.Helper;

// ReSharper disable StringLiteralTypo

namespace GloriousTroops
{
    public static class EquipmentUpgrading
    {
        private static readonly string[] BadLoot = { "" };
        private static readonly string FightLogName = ModuleHelper.GetModuleFullPath("GloriousTroops") + @"\FightLog.txt";
        private static readonly DeferringLogger FightLog = new(FightLogName);
        private const int NumWeaponSlots = 4;

        private static readonly AccessTools.FieldRef<BasicCharacterObject, bool> IsSoldier =
            AccessTools.FieldRefAccess<BasicCharacterObject, bool>("<IsSoldier>k__BackingField");

        public static readonly AccessTools.FieldRef<Equipment, EquipmentElement[]> ItemSlots =
            AccessTools.FieldRefAccess<Equipment, EquipmentElement[]>("_itemSlots");

        private static readonly AccessTools.StructFieldRef<BodyProperties, StaticBodyProperties> StaticBodyProps =
            AccessTools.StructFieldRefAccess<BodyProperties, StaticBodyProperties>("_staticBodyProperties");

        internal static readonly AccessTools.FieldRef<BasicCharacterObject, MBCharacterSkills> CharacterSkills =
            AccessTools.FieldRefAccess<BasicCharacterObject, MBCharacterSkills>("CharacterSkills");

        private static readonly AccessTools.FieldRef<PropertyOwner<SkillObject>, Dictionary<SkillObject, int>> Attributes =
            AccessTools.FieldRefAccess<PropertyOwner<SkillObject>, Dictionary<SkillObject, int>>("_attributes");

        internal static MethodInfo SetName;
        private static SkillObject oneHanded;
        private static SkillObject twoHanded;
        private static SkillObject polearm;
        private static SkillObject throwing;
        private static SkillObject crossbow;
        private static SkillObject bow;
        private static SkillObject athletics;
        private static SkillObject riding;

        internal static void InitSkills()
        {
            oneHanded = MBObjectManager.Instance.GetObject<SkillObject>("OneHanded");
            twoHanded = MBObjectManager.Instance.GetObject<SkillObject>("TwoHanded");
            polearm = MBObjectManager.Instance.GetObject<SkillObject>("Polearm");
            throwing = MBObjectManager.Instance.GetObject<SkillObject>("Throwing");
            crossbow = MBObjectManager.Instance.GetObject<SkillObject>("Crossbow");
            bow = MBObjectManager.Instance.GetObject<SkillObject>("Bow");
            athletics = MBObjectManager.Instance.GetObject<SkillObject>("Athletics");
            riding = MBObjectManager.Instance.GetObject<SkillObject>("Riding");
        }

        private static void LogBoth(object input)
        {
            Log.Debug?.Log(input);
            FightLog.Debug?.Log(input);
        }

        public static void UpgradeEquipment(PartyBase party, ItemRoster loot)
        {
            var lootedItems = loot.OrderByDescending(i => i.EquipmentElement.Value()).ToList();
            var usableEquipment = lootedItems.Where(i =>
                    i.EquipmentElement.Item.ItemType
                        is ItemObject.ItemTypeEnum.Horse
                        or ItemObject.ItemTypeEnum.OneHandedWeapon
                        or ItemObject.ItemTypeEnum.TwoHandedWeapon
                        or ItemObject.ItemTypeEnum.Polearm
                        or ItemObject.ItemTypeEnum.Arrows
                        or ItemObject.ItemTypeEnum.Bolts
                        or ItemObject.ItemTypeEnum.Shield
                        or ItemObject.ItemTypeEnum.Bow
                        or ItemObject.ItemTypeEnum.Crossbow
                        or ItemObject.ItemTypeEnum.Thrown
                        or ItemObject.ItemTypeEnum.HeadArmor
                        or ItemObject.ItemTypeEnum.BodyArmor
                        or ItemObject.ItemTypeEnum.LegArmor
                        or ItemObject.ItemTypeEnum.HandArmor
                        or ItemObject.ItemTypeEnum.Pistol
                        or ItemObject.ItemTypeEnum.Musket
                        or ItemObject.ItemTypeEnum.Bullets
                        or ItemObject.ItemTypeEnum.ChestArmor
                        or ItemObject.ItemTypeEnum.Cape
                        or ItemObject.ItemTypeEnum.HorseHarness
                    && i.EquipmentElement.ItemValue >= (Globals.Settings?.MinLootValue ?? 1000))
                .OrderByDescending(i => i.EquipmentElement.Value()).ToListQ();
            usableEquipment.RemoveAll(e => BadLoot.Contains(e.EquipmentElement.Item.StringId));
            if (!usableEquipment.Any())
                return;

            var troops = UpdateTroops(party);
            if (!usableEquipment.Any())
                return;
            LogBoth($"Loot pile {usableEquipment.Sum(i => i.Amount)} items:");
            usableEquipment.Do(i => LogBoth($"  {i.Amount}x {i.EquipmentElement.Item.Name} {i.EquipmentElement.Value()}"));
            for (var i = 0; i < troops.Count; i++)
            {
                var troop = troops[i];
                if (troop.Character == CharacterObject.PlayerCharacter)
                    continue;
                if (!usableEquipment.Any())
                    break;
                LogBoth($"{troop.Character.Name} {troop.Character.StringId} is up for upgrades.");
                LogBoth("Current equipment:");
                for (var eqIndex = 0; eqIndex < Equipment.EquipmentSlotLength; eqIndex++)
                    LogBoth($"{eqIndex}: {troop.Character.Equipment[eqIndex].Item?.Name} valued at {troop.Character.Equipment[eqIndex].Value()}");

                for (var index = 0; index < usableEquipment.Count; index++)
                {
                    var possibleUpgrade = usableEquipment[index];
                    // bail-out clauses
                    // bandits will loot anything
                    if ((Globals.Settings?.MaintainCulture ?? false)
                        && troop.Character.Occupation is not Occupation.Bandit
                        && possibleUpgrade.EquipmentElement.Item.Culture != troop.Character.Culture
                        && possibleUpgrade.EquipmentElement.Item.Culture != CampaignData.NeutralFaction.Culture)
                        continue;
                    if (!(Globals.Settings?.Mounts ?? true) && (possibleUpgrade.EquipmentElement.Item.HasHorseComponent || possibleUpgrade.EquipmentElement.Item.HasSaddleComponent))
                        continue;
                    // no mounted crossbows
                    if (possibleUpgrade.EquipmentElement.Item.HasHorseComponent && troop.Character.Equipment.HasWeaponOfClass(WeaponClass.Crossbow))
                        continue;
                    // prevent them from getting a bunch of the same item
                    if (ItemSlots(troop.Character.Equipment).Contains(possibleUpgrade.EquipmentElement))
                        continue;
                    // if it's a horse slot but we already have enough, skip to next upgrade EquipmentElement
                    if (possibleUpgrade.EquipmentElement.Item.HasHorseComponent && party.MemberRoster.CountMounted() > party.MemberRoster.TotalManCount / 2)
                        continue;
                    // don't take items we can't use
                    if (troop.Character.GetSkillValue(possibleUpgrade.EquipmentElement.Item.RelevantSkill) < possibleUpgrade.EquipmentElement.Item.Difficulty)
                        continue;
                    // if it's ammo but we have no ranged weapons, skip to next upgrade EquipmentElement
                    if (possibleUpgrade.EquipmentElement.Item.ItemType is ItemObject.ItemTypeEnum.Arrows or ItemObject.ItemTypeEnum.Bolts
                        && (!troop.Character.Equipment.HasWeaponOfClass(WeaponClass.Bow)
                            || !troop.Character.Equipment.HasWeaponOfClass(WeaponClass.Crossbow)))
                        continue;
                    if (possibleUpgrade.EquipmentElement.Item.ItemType is ItemObject.ItemTypeEnum.Bullets
                        && (!troop.Character.Equipment.HasWeaponOfClass(WeaponClass.Pistol)
                            || !troop.Character.Equipment.HasWeaponOfClass(WeaponClass.Musket)))
                        continue;
                    LogBoth($"{troop.Character.Name} of {troop.Character.Culture.Name} considering... {possibleUpgrade.EquipmentElement.Item?.Name}, valued at {possibleUpgrade.EquipmentElement.Value()} of culture {possibleUpgrade.EquipmentElement.Item.Culture?.Name}");
                    DoPossibleUpgrade(party, possibleUpgrade, troop, ref usableEquipment);
                    // if all the troops were upgraded, bail out to the next troop
                    if (!troop.Character.IsHero && party.MemberRoster.FindIndexOfTroop(troop.Character.OriginalCharacter ?? troop.Character) == -1)
                        break;
                }
            }

            LogBoth("=== Done Looting ===");
            FightLog.Restart();
        }

        // collection is modified so re-sort
        // ReSharper disable once RedundantAssignment
        private static List<TroopRosterElement> UpdateTroops(PartyBase party)
        {
            var rosterElements = party.MemberRoster.GetTroopRoster()
                .OrderByDescending(e => e.Character.IsHero)
                .ThenByDescending(e => e.Character.Level)
                .ThenByDescending(SumValue).ToListQ();
            if (Globals.Settings?.OnlyBandits ?? false)
                rosterElements.RemoveAll(e => e.Character.Occupation is not Occupation.Bandit);
            return rosterElements;

            int SumValue(TroopRosterElement element)
            {
                var value = 0;
                for (var index = 0; index < Equipment.EquipmentSlotLength; index++)
                    value += (int)element.Character.Equipment[index].Value();
                return value;
            }
        }

        private static void MapUpgrade(PartyBase party, CharacterObject troop)
        {
            // Heroes keep their equipment without special tracking
            try
            {
                if (troop.IsHero)
                    return;
                if (!EquipmentMap.TryGetValue(troop.StringId, out _))
                {
                    Troops.Add(troop);
                    EquipmentMap.Add(troop.StringId, troop.Equipment);
                    SkillsMap.Add(troop.StringId, GloriousTroopsBehavior.Skills(CharacterSkills(troop)));
                    party.MemberRoster.Add(new TroopRosterElement(troop) { Number = 1 });
                    if (party.MemberRoster.Contains(troop.OriginalCharacter))
                        party.MemberRoster.RemoveTroop(troop.OriginalCharacter);
                }
                else
                {
                    EquipmentMap[troop.StringId] = troop.Equipment;
                    SkillsMap[troop.StringId] = GloriousTroopsBehavior.Skills(CharacterSkills(troop));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(ex.ToString()));
                LogException(ex);
            }
        }

        private static void DoPossibleUpgrade(
            PartyBase party,
            ItemRosterElement possibleUpgrade,
            TroopRosterElement troopRosterElement,
            ref List<ItemRosterElement> usableEquipment)
        {
            var targetSlot = GetSlotOfLeastValuableOfType(possibleUpgrade.EquipmentElement.Item.ItemType, troopRosterElement);
            if (targetSlot < 0)
                return;
            var replacedItem = troopRosterElement.Character.Equipment[targetSlot];
            if (Globals.Settings.MaintainType && replacedItem.Item?.WeaponComponent is { } replacedWeapon)
            {
                if (replacedWeapon.PrimaryWeapon.SwingDamageType != possibleUpgrade.EquipmentElement.Item.WeaponComponent?.PrimaryWeapon.SwingDamageType
                    || replacedWeapon.PrimaryWeapon.ThrustDamageType != possibleUpgrade.EquipmentElement.Item.WeaponComponent?.PrimaryWeapon.ThrustDamageType)
                {
                    LogBoth($"{troopRosterElement.Character.Name} passing on {possibleUpgrade.EquipmentElement.Item.Name} being different type from {replacedItem.Item.Name}");
                    return;
                }
            }

            if (replacedItem.Value() >= possibleUpgrade.EquipmentElement.Value())
                return;
            // allow a better shield
            if (possibleUpgrade.EquipmentElement.Item?.ItemType is ItemObject.ItemTypeEnum.Shield
                && replacedItem.Item?.ItemType is not ItemObject.ItemTypeEnum.Shield
                && ItemSlots(troopRosterElement.Character.Equipment).AnyQ(e => e.Item?.ItemType is ItemObject.ItemTypeEnum.Shield))
                return;
            var iterations = troopRosterElement.Number;
            for (var i = 0; i < iterations; i++)
            {
                var troop = !troopRosterElement.Character.IsHero && troopRosterElement.Character.OriginalCharacter is null
                    ? CreateCustomCharacter(troopRosterElement)
                    : troopRosterElement.Character;
                LogBoth($"### Upgrading {troop.HeroObject?.Name ?? troop.Name} ({troop.StringId}): {replacedItem.Item?.Name.ToString() ?? "empty slot"} with {possibleUpgrade.EquipmentElement.Item.Name}");
                // assign the upgrade
                troop.Equipment[targetSlot] = possibleUpgrade.EquipmentElement;
                MapUpgrade(party, troop);


                // skill up the troop
                SetSkillLevel(possibleUpgrade.EquipmentElement.Item, troop);

                // put anything replaced back into the pile
                if (!replacedItem.IsEmpty && replacedItem.Value() >= (Globals.Settings?.MinLootValue ?? 1000))
                {
                    LogBoth($"### Returning {replacedItem.Item?.Name} to the bag");
                    var index = usableEquipment.SelectQ(e => e.EquipmentElement.Item).ToListQ().FindIndexQ(replacedItem.Item);
                    if (index > -1)
                    {
                        var item = usableEquipment[index];
                        item.Amount++;
                        usableEquipment[index] = item;
                    }
                    else
                    {
                        usableEquipment.Add(new ItemRosterElement(replacedItem.Item, 1));
                        usableEquipment = usableEquipment.OrderByDescending(e => e.EquipmentElement.Value()).ToListQ();
                    }
                }

                // decrement and remove ItemRosterElements
                if (--possibleUpgrade.Amount == 0)
                {
                    usableEquipment.Remove(possibleUpgrade);
                    break;
                }
            }
        }

        private static CharacterObject CreateCustomCharacter(TroopRosterElement troop)
        {
            var tempCharacter = CharacterObject.CreateFrom(troop.Character);
            tempCharacter.InitializeHeroCharacterOnAfterLoad();
            tempCharacter.Level = troop.Character.Level; 
            // have to create a new MBCharacterSkills or every similar BCO picks up changes
            CharacterSkills(tempCharacter) = new MBCharacterSkills();
            Attributes(CharacterSkills(tempCharacter).Skills) = new Dictionary<SkillObject, int>(Attributes(CharacterSkills(troop.Character).Skills));
            var bodyProps = tempCharacter.GetBodyProperties(tempCharacter.Equipment);
            StaticBodyProps(ref bodyProps) = StaticBodyProperties.GetRandomStaticBodyProperties();
            SetName.Invoke(tempCharacter, new object[] { new TextObject(@"{=GTTroop}Glorious {TROOP}").SetTextVariable("TROOP", tempCharacter.Name) });
            IsSoldier(tempCharacter) = true;
            HiddenInEncyclopedia(tempCharacter) = true;
            var mbEquipmentRoster = new MBEquipmentRoster();
            Equipments(mbEquipmentRoster) = new List<Equipment> { new(troop.Character.Equipment) };
            EquipmentRoster(tempCharacter) = mbEquipmentRoster;
            return tempCharacter;
        }

        private static int GetSlotOfLeastValuableOfType(ItemObject.ItemTypeEnum itemType, TroopRosterElement troopRosterElement)
        {
            var leastValuable = float.MaxValue;
            var leastSlot = -1;
            var itemTypes = itemType is ItemObject.ItemTypeEnum.TwoHandedWeapon or ItemObject.ItemTypeEnum.Polearm
                ? new[] { ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm }
                : new[] { itemType };
            for (var slot = 0; slot < Equipment.EquipmentSlotLength; slot++)
            {
                var value = troopRosterElement.Character.Equipment[slot].Value();
                if (troopRosterElement.Character.Equipment[slot].Item is not null
                    && itemTypes.Contains(troopRosterElement.Character.Equipment[slot].Item.ItemType)
                    && value < leastValuable)
                {
                    leastValuable = value;
                    leastSlot = slot;
                }
            }

            // find empty weapon slot
            if (leastSlot == -1 && itemType is not
                    (ItemObject.ItemTypeEnum.HeadArmor
                    or ItemObject.ItemTypeEnum.BodyArmor
                    or ItemObject.ItemTypeEnum.LegArmor
                    or ItemObject.ItemTypeEnum.HandArmor
                    or ItemObject.ItemTypeEnum.Cape
                    or ItemObject.ItemTypeEnum.Horse
                    or ItemObject.ItemTypeEnum.HorseHarness))
                for (var slot = 0; slot < NumWeaponSlots; slot++)
                    if (troopRosterElement.Character.Equipment[slot].IsEmpty)
                        return slot;

            return leastSlot;
        }

        private static void SetSkillLevel(ItemObject item, CharacterObject troop)
        {
            switch (item?.ItemType)
            {
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    CharacterSkills(troop).Skills.SetPropertyValue(riding, ClampSkillLevel(riding));
                    break;
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                    CharacterSkills(troop).Skills.SetPropertyValue(oneHanded, ClampSkillLevel(oneHanded));
                    break;
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                    CharacterSkills(troop).Skills.SetPropertyValue(twoHanded, ClampSkillLevel(twoHanded));
                    break;
                case ItemObject.ItemTypeEnum.Polearm:
                    CharacterSkills(troop).Skills.SetPropertyValue(polearm, ClampSkillLevel(polearm));
                    break;
                case ItemObject.ItemTypeEnum.Arrows:
                    CharacterSkills(troop).Skills.SetPropertyValue(bow, ClampSkillLevel(bow));
                    break;
                case ItemObject.ItemTypeEnum.Bolts:
                    CharacterSkills(troop).Skills.SetPropertyValue(crossbow, ClampSkillLevel(crossbow));
                    break;
                case ItemObject.ItemTypeEnum.Bow:
                    CharacterSkills(troop).Skills.SetPropertyValue(bow, ClampSkillLevel(bow));
                    break;
                case ItemObject.ItemTypeEnum.Crossbow:
                    CharacterSkills(troop).Skills.SetPropertyValue(crossbow, ClampSkillLevel(crossbow));
                    break;
                case ItemObject.ItemTypeEnum.Thrown:
                    CharacterSkills(troop).Skills.SetPropertyValue(throwing, ClampSkillLevel(throwing));
                    break;
                case ItemObject.ItemTypeEnum.Shield:
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.ChestArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    CharacterSkills(troop).Skills.SetPropertyValue(athletics, ClampSkillLevel(athletics));
                    break;
            }

            int ClampSkillLevel(SkillObject skill)
            {
                return Math.Min(troop.GetSkillValue(athletics) + Globals.Settings.SkillBuffAmount, 300);
            }
        }
    }
}
