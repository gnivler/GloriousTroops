using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

// ReSharper disable InconsistentNaming

namespace UniqueTroopsGoneWild
{
    public class SubModule : MBSubModuleBase
    {
        internal static Harmony harmony;

        protected override void OnSubModuleLoad()
        {
            harmony = new Harmony("ca.gnivler.bannerlord.UniqueTroopsGoneWild");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            base.OnSubModuleLoad();
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            var propertyBasedTooltipVMExtensions = AccessTools.TypeByName("PropertyBasedTooltipVMExtensions");

            var displayClass = AccessTools.Inner(propertyBasedTooltipVMExtensions, "<>c__DisplayClass16_0");
            var original = displayClass.GetMethod("<UpdateTooltip>b__0", AccessTools.all);
            var replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipPartyMemberReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            original = displayClass.GetMethod("<UpdateTooltip>b__1", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipPartyPrisonerReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            displayClass = AccessTools.Inner(propertyBasedTooltipVMExtensions, "<>c__DisplayClass15_0");
            original = displayClass.GetMethod("<UpdateTooltip>b__0", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipSettlementMemberReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            original = displayClass.GetMethod("<UpdateTooltip>b__1", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipSettlementPrisonerReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            displayClass = AccessTools.Inner(propertyBasedTooltipVMExtensions, "<>c__DisplayClass17_0");
            original = displayClass.GetMethod("<UpdateTooltip>b__0", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipArmyMemberReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            original = displayClass.GetMethod("<UpdateTooltip>b__1", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipArmyPrisonerReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            if (Environment.MachineName == "MEOWMEOW")
            {
                CampaignCheats.SetCampaignSpeed(new List<string> { "100" });
                CampaignCheats.SetMainPartyAttackable(new List<string> { "0" });
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Globals.Settings = Settings.Instance;
            Globals.Log.Debug?.Log($"{Globals.Settings?.DisplayName} starting up...");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter gameStarter)
                gameStarter.AddBehavior(new UniqueTroopsBehavior());
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            var superKey = Campaign.Current != null
                           && (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
                           && (Input.IsKeyDown(InputKey.LeftAlt) || Input.IsKeyDown(InputKey.RightAlt))
                           && (Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift));

            if (superKey && Input.IsKeyPressed(InputKey.T))
                Helper.Nuke();

            if (Environment.MachineName == "MEOWMEOW" && Input.IsKeyPressed(InputKey.Tilde))
                Helper.CheckTracking();
        }
    }
}
