using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

// ReSharper disable ClassNeverInstantiated.Global  
// ReSharper disable InconsistentNaming

namespace GloriousTroops
{
    public class SubModule : MBSubModuleBase
    {
        internal static Harmony harmony;
        internal static readonly bool MEOWMEOW = Environment.MachineName == "MEOWMEOW";

        private static readonly AccessTools.FieldRef<PartyVM, PartyCharacterVM> currentCharacter =
            AccessTools.FieldRefAccess<PartyVM, PartyCharacterVM>("_currentCharacter");

        internal static readonly FieldInfo dataSource = AccessTools.Field(typeof(GauntletPartyScreen), "_dataSource");
        private static bool IsPatched;

        protected override void OnSubModuleLoad()
        {
            harmony = new Harmony("ca.gnivler.bannerlord.GloriousTroops");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            base.OnSubModuleLoad();
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);

            if (!IsPatched)
            {
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

                original = AccessTools.Method("DefaultPartyWageModel:GetTotalWage");
                var updateFinalizer = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateFinalizer));
                harmony.Patch(original, finalizer: new HarmonyMethod(updateFinalizer));

                original = AccessTools.Method("SPScoreboardSideVM:RemoveTroop");
                var prefix = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.HideoutBossDuelPrefix));
                harmony.Patch(original, prefix: new HarmonyMethod(prefix));

                // if (MEOWMEOW || Globals.Settings.SaveRecovery)
                // {
                //     original = AccessTools.Method("SaveContext:CollectObjects", new Type[] { });
                //     var postfix = AccessTools.Method(typeof(MiscPatches.SaveContextCollectObjects), nameof(MiscPatches.SaveContextCollectObjects.Postfix));
                //     harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                // }

                if (Globals.Settings.PartyScreenChanges)
                {
                    var ctor = AccessTools.FirstConstructor(typeof(PartyCharacterVM), c => c.GetParameters().Length > 0);
                    harmony.Patch(ctor, transpiler: new HarmonyMethod(typeof(MiscPatches.PartyCharacterVMConstructor), nameof(MiscPatches.PartyCharacterVMConstructor.Transpiler)));
                    original = AccessTools.Method("PartyScreenLogic:ValidateCommand");
                    var patch = AccessTools.Method(typeof(MiscPatches.PartyScreenLogicValidateCommand), nameof(MiscPatches.PartyScreenLogicValidateCommand.Transpiler));
                    harmony.Patch(original, transpiler: new HarmonyMethod(patch));
                }

                IsPatched = true;
            }

            EquipmentUpgrading.InitSkills();
            EquipmentUpgrading.SetName = AccessTools.Method(typeof(CharacterObject), "SetName");
            if (MEOWMEOW)
            {
                CampaignCheats.SetCampaignSpeed(new List<string> { "100" });
                CampaignCheats.SetMainPartyAttackable(new List<string> { "0" });
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Globals.Settings = Settings.Instance;
            if (Globals.Settings!.Debug)
                Globals.Log.Restart();
            Globals.Log.Debug?.Log($"{Globals.Settings?.DisplayName} starting up...");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter gameStarter)
                gameStarter.AddBehavior(new GloriousTroopsBehavior());
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            var superKey = Campaign.Current != null
                           && (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
                           && (Input.IsKeyDown(InputKey.LeftAlt) || Input.IsKeyDown(InputKey.RightAlt))
                           && (Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift));

            if (ScreenManager.TopScreen is GauntletPartyScreen screen
                && Input.IsKeyPressed(InputKey.Space))
            {
                try
                {
                    var partyVM = (PartyVM)dataSource.GetValue(screen);
                    var troop = currentCharacter(partyVM);
                    if (troop is null)
                        return;

                    //this.CurrentCharacter.Side TODO use this member instead of First
                    //var inRoster = partyVM.PartyScreenLogic.MemberRosters.First(r => r.Contains(troop.Character));
                    //uniqueTroops = inRoster.ToFlattenedRoster().WhereQ(e => e.Troop.Name.Equals(troop.Character.Name)).ToListQ();
                    //troopIndex = uniqueTroops.FindIndexQ(e => e.Troop.StringId == troop.Character.StringId);
                    //var nextIndex = troopIndex + 1 > uniqueTroops.Count ? 0 : troopIndex + 1;
                    //if (!troop.IsHero && troop.Character.OriginalCharacter is not null)
                    //{
                    //    var element = currentCharacter(partyVM).Troop;
                    //    element.Character = uniqueTroops[nextIndex].Troop;
                    //    currentCharacter(partyVM).Troop = element;
                    //    Traverse.Create(partyVM).Method("RefreshCurrentCharacterInformation").GetValue();
                    //    Globals.Log.Debug?.Log(troop.Name);
                    //}
                }
                catch
                {
                    //ignore
                }
            }

            if (superKey && Input.IsKeyPressed(InputKey.T))
                Helper.Restore();

            if (MEOWMEOW && Input.IsKeyPressed(InputKey.Tilde))
                Helper.CheckTracking();
        }
    }
}
