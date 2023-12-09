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
        public override string Version => "1.2.0";
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

            private readonly CloudValueVariable<string> _uriVariable;

            public static StatueSystemWizard GetOrCreateWizard(Slot x)
            {
                return new StatueSystemWizard(x);
            }

            private StatueSystemWizard(Slot x)
            {
                // Initialize cloud spawn
                var statueSystemLoadSlot = x.AddSlot("Statue System Loader");
                var statueSystemCloudURIVariable = statueSystemLoadSlot.AttachComponent<CloudValueVariable<string>>();
                statueSystemCloudURIVariable.Path.Value = "U-Azavit.Statue.Stable.AssetURI";
                statueSystemCloudURIVariable.VariableOwnerId.Value = "U-Azavit";
                statueSystemCloudURIVariable.ChangeHandling.Value = CloudVariableChangeMode.Ignore;
                statueSystemCloudURIVariable.IsLinkedToCloud.Value = true;
                _uriVariable = statueSystemCloudURIVariable;

                // Init editor
                _wizardSlot = x;
                _wizardSlot.Tag = "Developer";
                _wizardSlot.PersistentSelf = false;
                _wizardSlot.LocalScale *= 0.0008f;

                _avatar = new Avatar();

                _ui = new WizardUi(_wizardSlot, WIZARD_TITLE, _avatar, InstallSystemOnAvatar);

                Log.Setup(_ui, Debug, Msg, Warn, Error);
            }

            public bool InstallSystemOnAvatar(Slot scratchSpace, SyncRef<Slot> statueSystemFallback)
            {
                Log.Info($"=== Starting install for avatar {_avatar.AvatarRoot.ToShortString()}");

                // Start
                if(!_avatar.VerifyInstallRequirements())
                    return false;

                _avatar.SetScratchSpace(scratchSpace);

                _avatar.CreateOrUpdateSlots();
                _avatar.SetupRootDynVar();

                // Add statue system objects
                var systemSlot = GetStatueSystem(scratchSpace, _uriVariable, statueSystemFallback);
                if(systemSlot == null)
                    return false;

                _avatar.RemoveLegacySystem();
                _avatar.InstallRemasterSystem(systemSlot);

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

                return true;
            }

            public Slot SpawnSlot(Slot x, string file, World world, float3 position, float3 scale)
            {
                DataTreeDictionary loadNode = DataTreeConverter.Load(file);

                Slot slot = x.AddSlot("SpawnSlotObject");
                slot.CreateSpawnUndoPoint();
                slot.LoadObject(loadNode);
                slot.GlobalPosition = position;
                slot.GlobalScale = scale;

                return slot.Children.First();
            }

            public Slot GetStatueSystem(Slot scratchSpace, CloudValueVariable<string> uriVariable, SyncRef<Slot> fallback)
            {
                if (fallback.Value != RefID.Null)
                {
                    Log.Info("Using statue system override from RefID " + fallback.Value);

                    return fallback.Target.Duplicate(scratchSpace);
                }
                else
                {
                    Log.Info("Getting statue system from cloud");
                    // Yoinked from FrooxEngine.FileMetadata.OnImportFile
                    var fileName = uriVariable.Value.Value;
                    var fileUri = new Uri(fileName);

                    var record = scratchSpace.Engine.RecordManager.FetchRecord(fileUri).GetAwaiter().GetResult();

                    Log.Debug($"Got record for {record.Entity.Name}");
                    Log.Debug($"Fetching from {record.Entity.AssetURI}");

                    string fileData = scratchSpace.Engine.AssetManager.GatherAssetFile(new Uri(record.Entity.AssetURI), 100.0f).GetAwaiter().GetResult();

                    Msg(fileUri);
                    Msg(fileData);

                    if (fileData != null)
                    {
                        scratchSpace.LocalUser.GetPointInFrontOfUser(out var point, out var rotation, float3.Backward);

                        Log.Info("Got file successfully");

                        return SpawnSlot(scratchSpace, fileData, scratchSpace.World, point, new float3(1.0f, 1.0f, 1.0f));
                    }
                    else
                    {
                        Log.Error("ERROR: File was null after RequestGather");

                        return scratchSpace.AddSlot("File was null after RequestGather");
                    }
                }
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
