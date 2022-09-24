using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using static UniqueTroopsGoneWild.Globals;
using static UniqueTroopsGoneWild.Helper;

namespace UniqueTroopsGoneWild
{
    internal static class EquipmentUpgrading
    {
        private static readonly string[] BadLoot = { "" };
        private static readonly List<int> Slots = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

        private static readonly AccessTools.FieldRef<BasicCharacterObject, bool> IsSoldier =
            AccessTools.FieldRefAccess<BasicCharacterObject, bool>("<IsSoldier>k__BackingField");

        private static MethodInfo setName;

        internal static void UpgradeEquipment(PartyBase party, ItemRoster loot)
        {
            var lootedItems = loot.OrderByDescending(i => i.EquipmentElement.ItemValue).ToListQ();
            var usableEquipment = lootedItems.WhereQ(i =>
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
                    && i.EquipmentElement.ItemValue >= Globals.Settings.MinLootValue)
                .OrderByDescending(i => i.EquipmentElement.ItemValue).ToListQ();
            usableEquipment.RemoveAll(e => BadLoot.Contains(e.EquipmentElement.Item.StringId));
            if (!usableEquipment.Any())
                return;

            var troops = UpdateTroops(party);
            if (!usableEquipment.Any())
                return;
            for (var i = 0; i < troops.Count; i++)
            {
                var troop = troops[i];
                if (troop.Character == CharacterObject.PlayerCharacter)
                    continue;
                if (!usableEquipment.Any())
                    break;
                Log.Debug?.Log($"{troop.Character.Name} {troop.Character.StringId} is up for upgrades.");
                for (var index = 0; index < usableEquipment.Count; index++)
                {
                    var possibleUpgrade = usableEquipment[index];
                    // bail-out clauses
                    // bandits will loot anything
                    if (Globals.Settings.MaintainCulture
                        && troop.Character.Occupation is not Occupation.Bandit
                        && possibleUpgrade.EquipmentElement.Item.Culture != troop.Character.Culture
                        && possibleUpgrade.EquipmentElement.Item.Culture != CampaignData.NeutralFaction.Culture)
                        continue;
                    if (!Globals.Settings.Mounts && (possibleUpgrade.EquipmentElement.Item.HasHorseComponent || possibleUpgrade.EquipmentElement.Item.HasSaddleComponent))
                        continue;
                    if (possibleUpgrade.EquipmentElement.Item.HasHorseComponent && troop.Character.Equipment.HasWeaponOfClass(WeaponClass.Crossbow))
                        continue;
                    // prevent them from getting a bunch of the same item
                    if (troop.Character.Equipment.Contains(possibleUpgrade.EquipmentElement))
                        continue;
                    // if it's a horse slot but we already have enough, skip to next upgrade EquipmentElement
                    if (possibleUpgrade.EquipmentElement.Item.HasHorseComponent && party.MemberRoster.CountMounted() > party.MemberRoster.TotalManCount / 2)
                        continue;
                    // don't take items we can't use
                    if (troop.Character.GetSkillValue(possibleUpgrade.EquipmentElement.Item.RelevantSkill) < possibleUpgrade.EquipmentElement.Item.Difficulty)
                        continue;
                    var upgradeValue = possibleUpgrade.EquipmentElement.ItemValue;
                    if (upgradeValue <= LeastValuableItem(troop.Character))
                        break;
                    // only take ammo manually
                    if (possibleUpgrade.EquipmentElement.Item.ItemType
                        is ItemObject.ItemTypeEnum.Arrows
                        or ItemObject.ItemTypeEnum.Bolts
                        or ItemObject.ItemTypeEnum.Bullets)
                        continue;
                    Log.Debug?.Log("Current equipment:");
                    for (var eqIndex = 0; eqIndex < Equipment.EquipmentSlotLength; eqIndex++)
                        Log.Debug?.Log($"{eqIndex}: {troop.Character.Equipment[eqIndex].Item?.Name} {(troop.Character.Equipment[eqIndex].Item?.Value is not null ? "$" : "")}{troop.Character.Equipment[eqIndex].Item?.Value}");
                    Log.Debug?.Log($"{troop.Character.Name} of {troop.Character.Culture.Name} considering... {possibleUpgrade.EquipmentElement.Item?.Name}, worth {possibleUpgrade.EquipmentElement.ItemValue} of culture {possibleUpgrade.EquipmentElement.Item.Culture?.Name}");
                    var rangedSlot = -1;
                    // assume that sane builds are coming in (no double bows, missing ammo)
                    if (possibleUpgrade.EquipmentElement.Item.HasWeaponComponent)
                    {
                        if (possibleUpgrade.EquipmentElement.Item?.ItemType
                                is ItemObject.ItemTypeEnum.Bow
                                or ItemObject.ItemTypeEnum.Crossbow
                                or ItemObject.ItemTypeEnum.Pistol
                                or ItemObject.ItemTypeEnum.Musket
                            && possibleUpgrade.EquipmentElement.Item.PrimaryWeapon.WeaponClass
                                is not (WeaponClass.Javelin or WeaponClass.Stone))
                        {
                            // don't want to swap out a ranged weapon for a melee weapon
                            if (troop.Character.GetFormationClass() is not (FormationClass.Ranged or FormationClass.Skirmisher or FormationClass.HorseArcher))
                                continue;
                            for (var slot = 0; slot < 4; slot++)
                                if (troop.Character.Equipment[slot].Item?.PrimaryWeapon is not null
                                    && troop.Character.Equipment[slot].Item.PrimaryWeapon.IsRangedWeapon)
                                {
                                    rangedSlot = slot;
                                    break;
                                }

                            // weapon is an upgrade so take it and take the ammo
                            if (DoPossibleUpgrade(party, possibleUpgrade, ref troop, ref usableEquipment, rangedSlot))
                            {
                                var ammo = GetAmmo(possibleUpgrade, usableEquipment);
                                if (ammo.IsEmpty)
                                    continue;
                                var ammoSlot = -1;
                                for (var slot = 0; slot < 4; slot++)
                                    if (troop.Character.Equipment[slot].Item?.PrimaryWeapon is not null
                                        && troop.Character.Equipment[slot].Item.PrimaryWeapon.IsAmmo)
                                        ammoSlot = slot;

                                possibleUpgrade = new ItemRosterElement(ammo.EquipmentElement.Item, 1);
                                DoPossibleUpgrade(party, possibleUpgrade, ref troop, ref usableEquipment, ammoSlot);
                                continue;
                            }
                        }
                    }

                    Slots.Shuffle();
                    var slots = new List<int>(Slots);
                    for (; slots.Count > 0; slots.RemoveAt(0))
                    {
                        var slot = slots[0];
                        if (Equipment.IsItemFitsToSlot((EquipmentIndex)slot, possibleUpgrade.EquipmentElement.Item))
                        {
                            DoPossibleUpgrade(party, possibleUpgrade, ref troop, ref usableEquipment);
                            break;
                        }
                    }
                }
            }
        }

        // collection is modified so re-sort
        // ReSharper disable once RedundantAssignment
        private static List<TroopRosterElement> UpdateTroops(PartyBase party)
        {
            var rosterElements = party.MemberRoster.GetTroopRoster()
                .OrderBy(e => e.Character.IsHero)
                .ThenByDescending(e => e.Character.Level)
                .ThenByDescending(SumValue).ToListQ();
            if (Globals.Settings.OnlyBandits)
                rosterElements.RemoveAll(e => e.Character.Occupation is not Occupation.Bandit);
            return rosterElements;

            int SumValue(TroopRosterElement element)
            {
                var value = 0;
                for (var index = 0; index < Equipment.EquipmentSlotLength; index++)
                    value += element.Character.Equipment[index].ItemValue;
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
                    party.MemberRoster.Add(new TroopRosterElement(troop) { Number = 1 });
                    // the final troop won't exist any longer due to being replaced earlier
                    var index = party.MemberRoster.FindIndexOfTroop(troop.OriginalCharacter);
                    if (party.MemberRoster.GetElementNumber(index) > 0)
                        party.MemberRoster.RemoveTroop(troop.OriginalCharacter);
                }
                else
                {
                    EquipmentMap[troop.StringId] = troop.Equipment;
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(ex.ToString()));
                Log.Debug?.Log(ex);
            }
        }

        private static int LeastValuableItem(CharacterObject tempCharacter)
        {
            var leastValuable = int.MaxValue;
            for (var slot = 0; slot < Equipment.EquipmentSlotLength; slot++)
            {
                if (tempCharacter.Equipment[slot].ItemValue < leastValuable)
                    leastValuable = tempCharacter.Equipment[slot].ItemValue;
            }

            return leastValuable;
        }

        private static ItemRosterElement GetAmmo(ItemRosterElement possibleUpgrade, List<ItemRosterElement> usableEquipment)
        {
            var ammo = possibleUpgrade.EquipmentElement.Item.ItemType switch
            {
                ItemObject.ItemTypeEnum.Bow => usableEquipment.FirstOrDefaultQ(e => e.EquipmentElement.Item.ItemType is ItemObject.ItemTypeEnum.Arrows),
                ItemObject.ItemTypeEnum.Crossbow => usableEquipment.FirstOrDefaultQ(e => e.EquipmentElement.Item.ItemType is ItemObject.ItemTypeEnum.Bolts),
                ItemObject.ItemTypeEnum.Musket or ItemObject.ItemTypeEnum.Pistol =>
                    usableEquipment.FirstOrDefaultQ(e => e.EquipmentElement.Item.ItemType is ItemObject.ItemTypeEnum.Bullets),
                _ => default
            };
            return ammo;
        }

        private static bool DoPossibleUpgrade(
            PartyBase party,
            ItemRosterElement possibleUpgrade,
            ref TroopRosterElement troopRosterElement,
            ref List<ItemRosterElement> usableEquipment,
            int slotOverride = -1)
        {
            // current item where it's the right kind
            var targetSlot = slotOverride < 0 ? GetLowestValueSlotThatFits(troopRosterElement.Character.Equipment, possibleUpgrade) : slotOverride;
            var replacedItem = troopRosterElement.Character.Equipment[targetSlot];
            // every slot is better or the equipment isn't an upgrade
            if (targetSlot < 0 || replacedItem.ItemValue >= possibleUpgrade.EquipmentElement.ItemValue)
                return false;
            for (var i = 0; i < troopRosterElement.Number; i++)
            {
                var troop = !troopRosterElement.Character.IsHero && troopRosterElement.Character.OriginalCharacter is null
                    ? CreateCustomCharacter(troopRosterElement)
                    : troopRosterElement.Character;
                if (troop.Name is null)
                    Debugger.Break();
                Log.Debug?.Log($"### Upgrading {troop.HeroObject?.Name ?? troop.Name} ({troop.StringId}): {replacedItem.Item?.Name.ToString() ?? "empty slot"} with {possibleUpgrade.EquipmentElement.Item.Name}");
                // assign the upgrade
                troop.Equipment[targetSlot] = possibleUpgrade.EquipmentElement;
                MapUpgrade(party, troop);
                // put anything replaced back into the pile
                if (!replacedItem.IsEmpty && replacedItem.ItemValue >= Globals.Settings.MinLootValue)
                {
                    //Log.Debug?.Log($"### Returning {replacedItem.Item?.Name} to the bag");
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
                        usableEquipment = usableEquipment.OrderByDescending(e => e.EquipmentElement.ItemValue).ToListQ();
                    }

                    // decrement and remove ItemRosterElements
                    if (--possibleUpgrade.Amount == 0)
                    {
                        usableEquipment.Remove(possibleUpgrade);
                        break;
                    }
                }
            }

            if (party.MemberRoster.GetTroopRoster().AnyQ(tre => tre.Character.Name == null))
                Debugger.Break();
            return true;
        }

        private static CharacterObject CreateCustomCharacter(TroopRosterElement troop)
        {
            var tempCharacter = CharacterObject.CreateFrom(troop.Character);
            tempCharacter.InitializeHeroCharacterOnAfterLoad();
            setName ??= AccessTools.Method(typeof(CharacterObject), "SetName");
            // localization not included
            setName.Invoke(tempCharacter, new object[] { new TextObject(@"{=BMTroops}Upgraded " + tempCharacter.Name) });
            IsSoldier(tempCharacter) = true;
            HiddenInEncyclopedia(tempCharacter) = true;
            var mbEquipmentRoster = new MBEquipmentRoster();
            Equipments(mbEquipmentRoster) = new List<Equipment> { new(troop.Character.Equipment) };
            EquipmentRoster(tempCharacter) = mbEquipmentRoster;
            //Log.Debug?.Log($"Created {tempCharacter.Name} {tempCharacter.StringId}");
            return tempCharacter;
        }

        private static int GetLowestValueSlotThatFits(Equipment equipment, ItemRosterElement possibleUpgrade)
        {
            var lowestValue = int.MaxValue;
            var targetSlot = -1;
            for (var slot = 0; slot < Equipment.EquipmentSlotLength; slot++)
            {
                if (!Equipment.IsItemFitsToSlot((EquipmentIndex)slot, possibleUpgrade.EquipmentElement.Item))
                    continue;
                if (equipment[slot].IsEmpty)
                {
                    targetSlot = slot;
                    break;
                }

                if (equipment[slot].Item.ItemType is
                    ItemObject.ItemTypeEnum.Arrows
                    or ItemObject.ItemTypeEnum.Bolts
                    or ItemObject.ItemTypeEnum.Bullets) continue;

                if (equipment[slot].ItemValue < lowestValue)
                {
                    lowestValue = equipment[slot].ItemValue;
                    targetSlot = slot;
                }
            }

            return targetSlot;
        }
    }
}
