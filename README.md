# Glorious Troops

A Mount & Blade II: Bannerlord mod that enhances troop equipment and management mechanics.

## Overview

Glorious Troops is a comprehensive mod that improves how AI troops manage their equipment in battles. Instead of using fixed equipment sets, your troops will now dynamically upgrade their gear based on battlefield loot, creating more adaptive and capable forces as they gain experience and victories.

## Key Features

### Dynamic Equipment Upgrading
- Troops automatically upgrade their equipment after battles based on available loot
- Intelligent gear selection prioritizes higher-value items while maintaining compatibility with troop skills
- Equipment upgrades are distributed based on kill records, giving priority to troops that performed well in battle

### Custom Troop Creation
- When upgrading gear, new "Glorious" troop variants are created to preserve original troop templates
- These custom troops maintain upgraded equipment and skill levels throughout campaigns
- Heroes keep their unique equipment and are not affected by the upgrade system

### Loot Management System
- Configurable drop rates for equipment (default 66% chance per item)
- Minimum loot value threshold to prevent low-quality gear clutter
- Smart filtering to ensure troops only equip items they can effectively use

### Skill Progression
- Automatic skill increases when troops equip new weapons or armor
- Skills are capped appropriately to maintain game balance
- Skill progression tied directly to equipment usage

### UI Enhancements
- Custom skill panel accessible via hotkey (default Z) to view upgraded troop stats
- Compact party screen widgets showing multiple troop instances
- Detailed tracking of troop kills and equipment history

### Configuration Options
- Toggle equipment upgrades for bandits only
- Control looting of mounts and saddles
- Maintain cultural equipment restrictions
- Preserve weapon type preferences (blunt/slashing/piercing)
- Adjustable drop percentages and minimum loot values
- Party screen UI customization
- Debug logging capabilities

## Installation

1. Download and install the mod through the Bannerlord launcher or manually place in your Modules folder

## Usage

Once enabled, the mod works automatically in the background. Your troops will begin upgrading their equipment after battles based on the configured settings.

To view detailed information about your upgraded troops:
1. Press the configured hotkey (default Z) while on the campaign map
2. Use the skill panel to browse different troop types and their individual statistics

## Technical Details

- Created custom CharacterObject instances for upgraded troops to preserve original templates
- Implements extensive Harmony patches for deep integration with Bannerlord systems
- Maintains compatibility with save files through careful serialization handling

## Dependencies

- Bannerlord.ButterLib
- Bannerlord.MCM (Mod Configuration Menu)
- Bannerlord.UIExtenderEx
- Lib.Harmony

## Configuration

Access the mod settings through the Mod Configuration Menu (MCM) to customize behavior according to your preferences.

## Known Issues

- Some edge cases may occur with troop tracking during complex campaign events
- Save file compatibility between versions should be maintained but always backup saves

## Credits

Developed for the Mount & Blade II: Bannerlord community.