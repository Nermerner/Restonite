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
        None = 0,
        AlphaFade,
        AlphaCutout,
        PlaneSlicer,
        RadialSlicer,
        Count,
    }

    public static class ModExtensions
    {
        public static void Debugstr(this Slot x, string str)
        {
            // x.AddSlot(str);
        }
    }

    public class StoneMod : ResoniteMod
    {
        public override string Name => "Restonite";
        public override string Author => "Nermerner";
        public override string Version => "1.0.0";

        const string WIZARD_TITLE = "Statue System Wizard (Mod)";
        public override void OnEngineInit()
        {
            //// do whatever LibHarmony patching you need

            //Debug("a debug log");
            //Msg("a regular log");
            //Warn("a warn log");
            //Error("an error log");
            Engine.Current.RunPostInit(AddMenuOption);

            Harmony harmony = new Harmony("com.nermerner.StatueUtilities");
            harmony.PatchAll();
        }

        void AddMenuOption()
        {
            DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => StatueSystemWizard.GetOrCreateWizard(x));
        }


        class StatueSystemWizard
        {
            Slot WizardSlot;

            readonly ReferenceField<Slot> avatarRoot;
            readonly ReferenceField<Slot> slotsToAdd;
            readonly ValueField<int> statueType;
            readonly Button testButton;
            readonly Button spawnCloudButton;
            readonly RefEditor avatarField;

            readonly ReferenceField<IAssetProvider<Material>> baseStatueMaterial;

            readonly ReferenceMultiplexer<SkinnedMeshRenderer> foundSkinnedMeshRenderers;

            readonly CloudValueVariable<string> uriVariable;

            public static StatueSystemWizard GetOrCreateWizard(Slot x)
            {
                return new StatueSystemWizard(x);
            }

            Slot SpawnSlot(Slot x, string file, World world, float3 position, float3 scale)
            {
                DataTreeDictionary loadNode = DataTreeConverter.Load(file);

                Slot slot = world.LocalUserSpace.AddSlot("SpawnSlotObject");
                slot.CreateSpawnUndoPoint();
                slot.LoadObject(loadNode);
                slot.GlobalPosition = position;
                slot.GlobalScale = scale;

                return slot;
            }

            Slot GetSlotThing(Slot x)
            {
                // Yoinked from FrooxEngine.FileMetadata.OnImportFile
                var fileName = uriVariable.Value.Value;
                var fileUri = new Uri(fileName);

                var file = x.Engine.AssetManager.GatherAsset(fileUri, 4.0f).GetAwaiter().GetResult().GetFile().GetAwaiter().GetResult();

                if (file != null)
                {
                    x.LocalUser.GetPointInFrontOfUser(out var point, out var rotation, float3.Backward);

                    return SpawnSlot(x, file, x.World, point, new float3(1.0f, 1.0f, 1.0f));
                }
                else
                {
                    return x.AddSlot("File was null after RequestGather");
                }
            }

            StatueSystemWizard(Slot x)
            {
                var statueSystemLoadSlot = x.AddSlot("Statue System Loader");
                var statueSystemCloudURIVariable = statueSystemLoadSlot.AttachComponent<CloudValueVariable<string>>();
                statueSystemCloudURIVariable.Path.Value = "U-Azavit.Statue.Stable.AssetURI";
                statueSystemCloudURIVariable.VariableOwnerId.Value = "U-Azavit";
                statueSystemCloudURIVariable.ChangeHandling.Value = CloudVariableChangeMode.Ignore;
                statueSystemCloudURIVariable.IsLinkedToCloud.Value = true;
                uriVariable = statueSystemCloudURIVariable;

                WizardSlot = x;
                WizardSlot.Tag = "Developer";
                WizardSlot.PersistentSelf = false;

                LegacyCanvasPanel canvasPanel = WizardSlot.AttachComponent<LegacyCanvasPanel>();
                canvasPanel.Panel.AddCloseButton();
                canvasPanel.Panel.AddParentButton();
                canvasPanel.Panel.Title = WIZARD_TITLE;
                canvasPanel.Canvas.Size.Value = new float2(800f, 756f);

                Slot Data = WizardSlot.AddSlot("Data");

                avatarRoot = Data.AddSlot("avatarRoot").AttachComponent<ReferenceField<Slot>>();

                slotsToAdd = Data.AddSlot("slotsToAdd").AttachComponent<ReferenceField<Slot>>();

                baseStatueMaterial = Data.AddSlot("baseMaterial").AttachComponent<ReferenceField<IAssetProvider<Material>>>();

                statueType = Data.AddSlot("statueType").AttachComponent<ValueField<int>>();

                foundSkinnedMeshRenderers = Data.AddSlot("foundSMRs").AttachComponent<ReferenceMultiplexer<SkinnedMeshRenderer>>();
                /*
                - Add avatar space
                - Add statue system objects
                - Enumerate SkinnedMeshRenderers
                - Do the algorithm thing to create statue object(s)
                - Add dynamic bool driver for Statue.BodyNormal and Statue.BodyStatue
                - Make Statue.DisableOnFreeze disable:
                     -> VRIK
                     -> Dynamic Bone Slot's Enabled State
                     -> Viseme Drivers
                     -> Expression Drivers
                     -> Animetion Systems (Wigglers, Panners, etc.)
                     -> Hand Posers
                     -> Custom Grabable Systems installed on avatar
                     -> Custom Nameplaces/Badges
                        * If you don't have a custom Nameplate/Badge you will need one to have it hidden.
                - Drives in general
                - Materials: generate default materials (Material 0 is used for vision overlay)
                - Configure material system specifically
                */

                UIBuilder UI = new UIBuilder(canvasPanel.Canvas);

                UI.Canvas.MarkDeveloper();
                UI.Canvas.AcceptPhysicalTouch.Value = false;

                UI.SplitHorizontally(0.5f, out RectTransform left, out RectTransform right);

                left.OffsetMax.Value = new float2(-2f);
                right.OffsetMin.Value = new float2(2f);

                UI.NestInto(left);

                UI.Style.MinHeight = 24f;
                UI.Style.PreferredHeight = 24f;
                UI.Style.PreferredWidth = 400f;
                UI.Style.MinWidth = 400f;

                VerticalLayout verticalLayout = UI.VerticalLayout(4f, childAlignment: Alignment.TopLeft);
                verticalLayout.ForceExpandHeight.Value = false;

                UI.Text("Processing Root:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Root");
                var avatarField = UI.Current.AttachComponent<RefEditor>();
                avatarField.Setup(avatarRoot.Reference);
                avatarRoot.Reference.OnValueChange += (field) =>
                {
                    foundSkinnedMeshRenderers.References.Clear();
                    foundSkinnedMeshRenderers.References.AddRange(avatarRoot.Reference.Target.GetComponentsInChildren<SkinnedMeshRenderer>());
                };

                //UI.Text("Slots to add (Statue System):").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                //UI.Next("SlotsToAdd");
                //UI.Current.AttachComponent<RefEditor>().Setup(slotsToAdd.Reference);

                UI.Text("Default statue material:");
                UI.Next("Base Texture");
                UI.Current.AttachComponent<RefEditor>().Setup(baseStatueMaterial.Reference);

                UI.Text("Statue transition type:");
                UI.Next("Statue Type");
                UI.HorizontalElementWithLabel("Alpha Fade", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.AlphaFade));
                UI.HorizontalElementWithLabel("Alpha Cutout", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.AlphaCutout));
                UI.HorizontalElementWithLabel("Plane Slicer", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.PlaneSlicer));
                UI.HorizontalElementWithLabel("Radial Slicer", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.RadialSlicer));

                UI.Spacer(24f);

                testButton = UI.Button("Statuefy!");
                testButton.LocalPressed += OnTestButtonPressed;

                //spawnCloudButton = UI.Button("Try spawning statue system from cloud");
                //spawnCloudButton.LocalPressed += (a, b) => { GetSlotThing(x); };

                UI.NestInto(right);
                UI.ScrollArea();
                UI.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                SyncMemberEditorBuilder.Build(foundSkinnedMeshRenderers.References, "Skinned Mesh Renderers found", null, UI);

                WizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
            }

            void OnTestButtonPressed(IButton button, ButtonEventData eventData)
            {
                var scratchSpace = WizardSlot.AddSlot("Scratch space");
                InstallSystemOnAvatar(avatarRoot.Reference.Target, scratchSpace, uriVariable, foundSkinnedMeshRenderers.References.ToList(), baseStatueMaterial.Reference.Target, (StatueType)statueType.Value.Value);
            }
        }


        public static void InstallSystemOnAvatar(
            Slot avatarRoot,
            Slot scratchSpace,
            CloudValueVariable<string> uriVariable,
            List<SkinnedMeshRenderer> normalSkinnedMeshRenderers,
            IAssetProvider<Material> baseStatueMaterial,
            StatueType statueType)
        {
            scratchSpace.Debugstr("Start, avatar = " + avatarRoot.Name);

            // Start
            var avatarRootSlot = avatarRoot;

            // Attach dynvar space
            var avatarSpace = avatarRootSlot.GetComponent<DynamicVariableSpace>((space) => space.SpaceName == "Avatar");
            if (avatarSpace == null)
            {
                avatarSpace = avatarRootSlot.AttachComponent<DynamicVariableSpace>();
                avatarSpace.SpaceName.Value = "Avatar";
            }
            scratchSpace.Debugstr("Space");

            // Add statue system objects
            var systemSlot = GetSlotThing(scratchSpace, uriVariable);
            scratchSpace.Debugstr("Slotspawn");

            systemSlot.FindChild((slot) => slot.Name == "Statue").Children.ToList().ForEach((childSlot) =>
            {
                childSlot.Duplicate(avatarRootSlot);
            });
            scratchSpace.Debugstr("Slot attached, SMR len = " + normalSkinnedMeshRenderers.Count());

            // Find unique slots to duplicate
            var normalUniqueSlots = new Dictionary<RefID, Slot>();
            normalSkinnedMeshRenderers.ForEach((smr) =>
            {
                if (!normalUniqueSlots.ContainsKey(smr.Slot.ReferenceID))
                {
                    normalUniqueSlots.Add(smr.Slot.ReferenceID, smr.Slot);
                }
            });
            scratchSpace.Debugstr("SMRS");

            // Duplicate each statue slot
            var statueSlots = new List<Slot>();
            normalUniqueSlots.ToList().ForEach((slot) =>
            {
                statueSlots.Add(slot.Value.Duplicate());
                statueSlots.Last().Name = slot.Value.Name + "_Statue";
            });

            // Get SMRs for each statue slot
            var statueSkinnedMeshRenderers = new List<SkinnedMeshRenderer>();
            statueSlots.ForEach((slot) =>
            {
                var smrs = slot.GetComponents<SkinnedMeshRenderer>();
                statueSkinnedMeshRenderers.AddRange(smrs);
            });

            var driverSlot = avatarRootSlot.AddSlot(name: "Drivers");
            // Oh lordy
            // var materialDriversSlot = driverSlot.AddSlot("Materials");

            // Materials:
            // 1. For each material that needs to be created, create a driver and default material
            // 2. For each old material, give it an appropriate blend mode

            // Create a map of normal materials -> statue materials
            var materialHolder = avatarRootSlot.AddSlot("Generated Materials");

            var statueMaterialHolder = materialHolder.AddSlot("Statue Materials");



            // Creating blinder material
            {
                var blinderMaterialHolder = statueMaterialHolder.AddSlot("Statue 0");
                var blinderDefaultMaterial = blinderMaterialHolder.CopyComponent((AssetProvider<Material>)baseStatueMaterial);

                var blinderDynVar = blinderMaterialHolder.AttachComponent<DynamicReferenceVariable<IAssetProvider<Material>>>();
                blinderDynVar.VariableName.Value = "Avatar/Statue.Material0";
                blinderDynVar.Reference.Value = blinderDefaultMaterial.ReferenceID;
                // Assigns Statue.Material.Assigned to field
                var assignedMaterialDriver = blinderMaterialHolder.AttachComponent<DynamicReferenceVariableDriver<IAssetProvider<Material>>>();
                assignedMaterialDriver.VariableName.Value = "Avatar/Statue.Material.Assigned";

                // Stores assigned for Equality check
                var assignedMaterialField = blinderMaterialHolder.AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                assignedMaterialDriver.Target.ForceLink(assignedMaterialField.Reference);

                // Assigns Statue.Material.Assigned to boolean
                var bassignedMaterialDriver = blinderMaterialHolder.AttachComponent<DynamicReferenceVariableDriver<IAssetProvider<Material>>>();
                bassignedMaterialDriver.VariableName.Value = "Avatar/Statue.Material.Assigned";

                // Decides whether we use default or assigned
                var booleanReferenceDriver = blinderMaterialHolder.AttachComponent<BooleanReferenceDriver<IAssetProvider<Material>>>();
                booleanReferenceDriver.TrueTarget.Value = blinderDefaultMaterial.ReferenceID;
                bassignedMaterialDriver.Target.ForceLink(booleanReferenceDriver.FalseTarget);

                // Checks if assigned material is null and writes that value to boolean ref driver
                var equalityDriver = blinderMaterialHolder.AttachComponent<ReferenceEqualityDriver<IAssetProvider<Material>>>();
                equalityDriver.TargetReference.Target = assignedMaterialField.Reference;
                equalityDriver.Target.ForceLink(booleanReferenceDriver.State);

                booleanReferenceDriver.TargetReference.ForceLink(blinderDynVar.Reference);
            }


            scratchSpace.Debugstr("a");

            // Create Material objects for each statue material
            var oldMaterialToStatueMaterialMap = new Dictionary<RefID, ReferenceMultiDriver<IAssetProvider<Material>>>();
            statueSkinnedMeshRenderers.ForEach((smr) =>
            {
                for (int i = 0; i < smr.Materials.Count; ++i)
                {
                    var material = smr.Materials[i];

                    if (!oldMaterialToStatueMaterialMap.ContainsKey(material.ReferenceID))
                    {
                        // If assigned == null, use default

                        // Create a new statue material object (i.e. drives material slot on statue SMR, has default material with normal map)
                        var newMaterialHolder = statueMaterialHolder.AddSlot($"Statue {oldMaterialToStatueMaterialMap.Count + 1}");
                        var newDefaultMaterial = MaterialHelpers.CreateStatueMaterial(material, baseStatueMaterial, newMaterialHolder);

                        // Assigns Statue.Material.Assigned to equality
                        var assignedMaterialDriver = newMaterialHolder.AttachComponent<DynamicReferenceVariableDriver<IAssetProvider<Material>>>();
                        assignedMaterialDriver.VariableName.Value = "Avatar/Statue.Material.Assigned";
                        var assignedMaterialField = newMaterialHolder.AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                        assignedMaterialDriver.Target.ForceLink(assignedMaterialField.Reference);

                        // Assigns Statue.Material.Assigned to boolean
                        var bassignedMaterialDriver = newMaterialHolder.AttachComponent<DynamicReferenceVariableDriver<IAssetProvider<Material>>>();
                        bassignedMaterialDriver.VariableName.Value = "Avatar/Statue.Material.Assigned";

                        // Decides whether we use default or assigned
                        var booleanReferenceDriver = newMaterialHolder.AttachComponent<BooleanReferenceDriver<IAssetProvider<Material>>>();
                        booleanReferenceDriver.TrueTarget.Value = newDefaultMaterial.ReferenceID;
                        bassignedMaterialDriver.Target.ForceLink(booleanReferenceDriver.FalseTarget);

                        // Checks if assigned material is null and writes that value to boolean ref driver
                        var equalityDriver = newMaterialHolder.AttachComponent<ReferenceEqualityDriver<IAssetProvider<Material>>>();
                        equalityDriver.TargetReference.Target = assignedMaterialField.Reference;
                        equalityDriver.Target.ForceLink(booleanReferenceDriver.State);

                        // boolean ref driver drives this, which drives everything else
                        var multiDriver = newMaterialHolder.AttachComponent<ReferenceMultiDriver<IAssetProvider<Material>>>();
                        booleanReferenceDriver.TargetReference.ForceLink(multiDriver.Reference);

                        // Makes material accessible elsewhere
                        var dynMaterialVariable = newMaterialHolder.AttachComponent<DynamicReferenceVariable<IAssetProvider<Material>>>();
                        dynMaterialVariable.VariableName.Value = $"Avatar/Statue.Material{oldMaterialToStatueMaterialMap.Count + 1}";

                        // Drive that dynvar
                        multiDriver.Drives.Add();
                        multiDriver.Drives[0].ForceLink(dynMaterialVariable.Reference);

                        oldMaterialToStatueMaterialMap.Add(material.ReferenceID, multiDriver);
                    }

                    var drives = oldMaterialToStatueMaterialMap[material.ReferenceID].Drives;
                    drives.Add().ForceLink(smr.Materials.GetElement(i));
                    // Thanks Dann :)
                }
            });


            var normalMaterialHolder = materialHolder.AddSlot("Normal Materials");
            var oldMaterialToNewNormalMaterialMap = new Dictionary<RefID, IAssetProvider<Material>>();
            // Create alpha material and swap normal material for it
            normalSkinnedMeshRenderers.ForEach((smr) =>
            {
                for (int i = 0; i < smr.Materials.Count; ++i)
                {
                    var oldMaterial = smr.Materials[i];

                    if (!oldMaterialToNewNormalMaterialMap.ContainsKey(oldMaterial.ReferenceID))
                    {
                        var newSlot = normalMaterialHolder.AddSlot($"Normal {oldMaterialToNewNormalMaterialMap.Count}");
                        var newMaterial = MaterialHelpers.CreateAlphaMaterial(oldMaterial, statueType, newSlot);
                        oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID] = newMaterial;
                    }

                    smr.Materials[i] = oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID];
                }
            });
            scratchSpace.Debugstr("b");

            // TODO: Hygiene: Create parent slots for avatars

            // Set up enabling drivers

            var normalDriverSlot = driverSlot.AddSlot("Avatar/Statue.BodyNormal");
            var normalVarReader = normalDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var normalDriver = normalDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            normalVarReader.VariableName.Value = "Avatar/Statue.BodyNormal";
            normalVarReader.DefaultValue.Value = true;
            normalVarReader.Target.Value = normalDriver.Value.ReferenceID;

            //normalUniqueSlots.ToList().ForEach((slot) =>
            //{
            //    normalDriver.Drives.Add();
            //    normalDriver.Drives[normalDriver.Drives.Count - 1].ForceLink(slot.Value.ActiveSelf_Field);
            //});

            scratchSpace.Debugstr("bb");
            foreach (var smr in normalSkinnedMeshRenderers)
            {
                normalDriver.Drives.Add().ForceLink(smr.EnabledField);
            }
            scratchSpace.Debugstr("bc");


            var statueDriverSlot = driverSlot.AddSlot("Avatar/Statue.BodyStatue");
            var statueVarReader = statueDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var statueDriver = statueDriverSlot.AttachComponent<ValueMultiDriver<bool>>();
            scratchSpace.Debugstr("bd");

            statueVarReader.VariableName.Value = "Avatar/Statue.BodyStatue";
            statueVarReader.DefaultValue.Value = false;
            statueVarReader.Target.Value = statueDriver.Value.ReferenceID;
            scratchSpace.Debugstr("be");

            //statueSlots.ToList().ForEach((slot) =>
            //{
            //    statueDriver.Drives.Add();
            //    statueDriver.Drives[statueDriver.Drives.Count - 1].ForceLink(slot.ActiveSelf_Field);
            //});

            scratchSpace.Debugstr("bf");
            foreach (var smr in statueSkinnedMeshRenderers)
            {
                statueDriver.Drives.Add().ForceLink(smr.EnabledField);
            }

            var disableOnFreezeDriverSlot = driverSlot.AddSlot("Avatar/Statue.DisableOnFreeze");
            var dofVarReader = disableOnFreezeDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var dofDriver = disableOnFreezeDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            dofVarReader.VariableName.Value = "Avatar/Statue.DisableOnFreeze";
            dofVarReader.DefaultValue.Value = true;
            dofVarReader.Target.Value = dofDriver.Value.ReferenceID;

            scratchSpace.Debugstr("bg");

            AddFieldToMultidriver(dofDriver, avatarRootSlot.GetComponent<VRIK>().EnabledField);

            var boneChainSlots = new Dictionary<RefID, Slot>();

            avatarRootSlot.GetComponentsInChildren<DynamicBoneChain>().ForEach((dbc) =>
            {
                if (!boneChainSlots.ContainsKey(dbc.Slot.ReferenceID))
                {
                    boneChainSlots.Add(dbc.Slot.ReferenceID, dbc.Slot);
                }
            });
            scratchSpace.Debugstr("c");

            boneChainSlots.ToList().ForEach((dbcSlot) => AddFieldToMultidriver(dofDriver, dbcSlot.Value.ActiveSelf_Field));

            // TODO: copy blendshapes to statue from normal
            AddFieldToMultidriver(dofDriver, avatarRootSlot.GetComponentInChildren<VisemeAnalyzer>().EnabledField);

            avatarRootSlot.GetComponentsInChildren<AvatarExpressionDriver>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });

            avatarRootSlot.GetComponentsInChildren<DirectVisemeDriver>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });

            // TODO: Disable animation systems (Wigglers, Panners, etc.)
            avatarRootSlot.GetComponentsInChildren<Wiggler>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Panner1D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Panner2D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Panner3D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Panner4D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Wobbler1D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Wobbler2D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Wobbler3D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            avatarRootSlot.GetComponentsInChildren<Wobbler4D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });

            avatarRootSlot.GetComponentsInChildren<HandPoser>().ForEach((hp) =>
            {
                AddFieldToMultidriver(dofDriver, hp.EnabledField);
            });

            avatarRootSlot.GetComponentsInChildren<EyeManager>().ForEach((em) =>
            {
                AddFieldToMultidriver(dofDriver, em.Slot.ActiveSelf_Field);
            });

            avatarRootSlot.GetComponentsInChildren<AvatarToolAnchor>().ForEach((em) =>
            {
                AddFieldToMultidriver(dofDriver, em.Slot.ActiveSelf_Field);
            });

            // Detect any name badges
            var nameBadges = avatarRootSlot.GetComponentsInChildren<AvatarNameTagAssigner>().Select((anta) => anta.Slot);
            foreach (var nameBadge in nameBadges)
            {
                var newParent = nameBadge.Parent.AddSlot("Name Badge Parent (Statufication)");
                nameBadge.SetParent(newParent, true);
                AddFieldToMultidriver(dofDriver, newParent.ActiveSelf_Field);
            }

            var whisperVolSlot = driverSlot.AddSlot("Avatar/Statue.WhisperVolume");
            var whisperDriver = whisperVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            whisperDriver.DefaultValue.Value = 0.75f;
            whisperDriver.VariableName.Value = "Avatar/Statue.WhisperVolume";
            whisperDriver.Target.Value = avatarRootSlot.GetComponentInChildren<AvatarAudioOutputManager>().WhisperConfig.Volume.ReferenceID;

            var voiceVolSlot = driverSlot.AddSlot("Avatar/Statue.VoiceVolume");
            var voiceDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            var shoutDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            voiceDriver.DefaultValue.Value = 1.0f;
            voiceDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
            voiceDriver.Target.Value = avatarRootSlot.GetComponentInChildren<AvatarAudioOutputManager>().NormalConfig.Volume.ReferenceID;
            shoutDriver.DefaultValue.Value = 1.0f;
            shoutDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
            shoutDriver.Target.Value = avatarRootSlot.GetComponentInChildren<AvatarAudioOutputManager>().ShoutConfig.Volume.ReferenceID;
            //TODO: maybe other voice configs

            scratchSpace.Debugstr("d");

            scratchSpace.Destroy();
        }

        public static Slot SpawnSlot(Slot x, string file, World world, float3 position, float3 scale)
        {
            DataTreeDictionary loadNode = DataTreeConverter.Load(file);

            Slot slot = x.AddSlot("SpawnSlotObject");
            slot.CreateSpawnUndoPoint();
            slot.LoadObject(loadNode);
            slot.GlobalPosition = position;
            slot.GlobalScale = scale;

            return slot;
        }

        public static Slot GetSlotThing(Slot x, CloudValueVariable<string> uriVariable)
        {
            // Yoinked from FrooxEngine.FileMetadata.OnImportFile
            var fileName = uriVariable.Value.Value;
            var fileUri = new Uri(fileName);

            var file = x.Engine.AssetManager.GatherAsset(fileUri, 4.0f).GetAwaiter().GetResult().GetFile().GetAwaiter().GetResult();

            if (file != null)
            {
                x.LocalUser.GetPointInFrontOfUser(out var point, out var rotation, float3.Backward);

                return SpawnSlot(x, file, x.World, point, new float3(1.0f, 1.0f, 1.0f));
            }
            else
            {
                return x.AddSlot("File was null after RequestGather");
            }
        }

        private static void AddFieldToMultidriver<T>(ValueMultiDriver<T> driver, Sync<T> field)
        {
            driver.Drives.Add();
            driver.Drives[driver.Drives.Count - 1].ForceLink(field);
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
