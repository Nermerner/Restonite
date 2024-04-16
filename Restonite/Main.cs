using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using FrooxEngine.UIX;
using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResoniteModLoader;
using Elements.Core;
using FrooxEngine.Undo;
using Elements.Assets;

namespace Restonite
{
    public enum StatueType : int
    {
        AlphaFade,
        AlphaCutout,
        PlaneSlicer,
        RadialSlicer,
    }

    public class StoneMod : ResoniteMod
    {
        private const string WIZARD_TITLE = "Statue System Wizard (Mod)";

        public override string Name => "Restonite";
        public override string Author => "Nermerner, Uruloke";
        public override string Version => "1.3.1";
        public override string Link => "https://github.com/Nermerner/Restonite";

        public override void OnEngineInit()
        {
            //// do whatever LibHarmony patching you need

            Engine.Current.RunPostInit(AddMenuOption);

            Harmony harmony = new Harmony("com.nermerner.StatueUtilities");
            harmony.PatchAll();
        }

        private void AddMenuOption()
        {
            DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => StatueSystemWizard.GetOrCreateWizard(x));
        }

        private class StatueSystemWizard
        {
            private readonly Slot _wizardSlot;
            private readonly WizardUi _ui;
            private readonly Avatar _avatar;

            public static StatueSystemWizard GetOrCreateWizard(Slot x)
            {
                return new StatueSystemWizard(x);
            }

            private StatueSystemWizard(Slot x)
            {
                // Init editor
                _wizardSlot = x;
                _wizardSlot.Tag = "Developer";
                _wizardSlot.PersistentSelf = false;
                _wizardSlot.LocalScale *= 0.0008f;

                _avatar = new Avatar();

                _ui = new WizardUi(_wizardSlot, WIZARD_TITLE, _avatar, InstallSystemOnAvatar);

                Log.Setup(_ui, Debug, Msg, Warn, Error);
            }

            public bool InstallSystemOnAvatar(Slot scratchSpace, Slot statueSystem, SyncRef<Slot> installSlot, SyncRef<Slot> contextMenuSlot)
            {
                Log.Info($"=== Starting install for avatar {_avatar.AvatarRoot.ToShortString()}");

                // Start
                if(!_avatar.VerifyInstallRequirements())
                    return false;

                _avatar.SetScratchSpace(scratchSpace);

                _avatar.CreateOrUpdateSlots(installSlot);
                _avatar.SetupRootDynVar();

                // Add statue system objects
                _avatar.RemoveLegacySystem();
                _avatar.InstallRemasterSystem(statueSystem, contextMenuSlot);

                if (!_avatar.DuplicateMeshes())
                    return false;

                // Materials:
                // 1. For each material that needs to be created, create a driver and default material
                // 2. For each old material, give it an appropriate blend mode

                // Create a map of normal materials -> statue materials
                _avatar.CollectMaterials();
                _avatar.GenerateNormalMaterials();
                _avatar.GenerateStatueMaterials();

                // Set up drivers
                _avatar.CopyBlendshapes();
                _avatar.CreateOrUpdateEnableDrivers();
                _avatar.CreateOrUpdateDisableOnFreeze();
                _avatar.CreateOrUpdateVoiceDrivers();

                _avatar.CreateOrUpdateDefaults();

                Log.Success("Setup completed successfully!");
                Log.Success("Re-equip your avatar before testing the system.");

                _avatar.OpenUserConfigInspector();

                return true;
            }
        }

        /*
        [HarmonyPatch(typeof(DynamicImpulseTriggerWithValue<IAssetProvider<Material>>), nameof(DynamicImpulseTriggerWithValue<IAssetProvider<Material>>.Run))]
        public class Patch
        {
            [HarmonyPostfix]
            public static void Postfix(DynamicImpulseTriggerWithValue<IAssetProvider<Material>> __instance)
            {
                // Target Hierarchy is either User Root or collider slot if no user
                if (__instance.Tag.Evaluate() == "StoneMod.SetupStatueSystem")
                {
                    // if (dynImpulseTrigger.TargetHierarchy.LastModifyingUser.Mat)
                    Slot senderSlot = __instance.Slot.GetObjectRoot();
                    Slot senderDynVars = senderSlot.FindChild((slot) => slot.Name.Contains("Dynvars"), 2);
                    Slot scratchSpace = senderSlot.FindChild((slot) => slot.Name.Contains("Scratch"));
                    Slot targetSlot = senderDynVars.FindChild((slot) => slot.Name.Contains("Target Root")).GetComponent<DynamicReferenceVariable<Slot>>().Reference.Target;

                    InstallSystemOnAvatar(
                        targetSlot,
                        scratchSpace,
                        senderDynVars.FindChild((slot) => slot.Name.Contains("Statue System Cloud URL"), 3).GetComponent<CloudValueVariable<string>>(),
                        targetSlot.GetComponentsInChildren<SkinnedMeshRenderer>(),
                        senderDynVars.FindChild((slot) => slot.Name.Contains("Statue Material"), 3).GetComponent<DynamicReferenceVariable<IAssetProvider<Material>>>().Reference.Target,
                        (StatueType)senderDynVars.FindChild((slot) => slot.Name.Contains("StatueType"), 3).GetComponent<DynamicValueVariable<int>>().Value.Value
                        );
                }
            }
        }
        */
    }
}
