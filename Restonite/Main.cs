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
            Text debugText;
            Checkbox skinnedMeshRenderersOnly;

            readonly ReferenceField<Slot> avatarRoot;
            readonly ReferenceField<Slot> statueSystemFallback;
            readonly ValueField<int> statueType;
            readonly Button confirmButton;

            readonly ReferenceField<IAssetProvider<Material>> baseStatueMaterial;

            readonly ReferenceMultiplexer<MeshRenderer> foundMeshRenderers;

            readonly CloudValueVariable<string> uriVariable;

            public static StatueSystemWizard GetOrCreateWizard(Slot x)
            {
                return new StatueSystemWizard(x);
            }

            public void LogDebug(string logMessage)
            {
                Msg(logMessage);
                this.debugText.Content.Value = $"<color=gray>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{this.debugText?.Content?.Value ?? ""}";
            }

            public void LogInfo(string logMessage)
            {
                Msg(logMessage);
                this.debugText.Content.Value = $"<color=black>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{this.debugText?.Content?.Value ?? ""}";
            }

            public void LogWarn(string logMessage)
            {
                Msg(logMessage);
                this.debugText.Content.Value = $"<color=yellow>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{this.debugText?.Content?.Value ?? ""}";
            }

            public void LogError(string logMessage)
            {
                Msg(logMessage);
                this.debugText.Content.Value = $"<color=red>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{this.debugText?.Content?.Value ?? ""}";
            }

            public void LogSuccess(string logMessage)
            {
                Msg(logMessage);
                this.debugText.Content.Value = $"<color=green>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{this.debugText?.Content?.Value ?? ""}";
            }

            StatueSystemWizard(Slot x)
            {
                // Initialize cloud spawn
                var statueSystemLoadSlot = x.AddSlot("Statue System Loader");
                var statueSystemCloudURIVariable = statueSystemLoadSlot.AttachComponent<CloudValueVariable<string>>();
                statueSystemCloudURIVariable.Path.Value = "U-Azavit.Statue.Stable.AssetURI";
                statueSystemCloudURIVariable.VariableOwnerId.Value = "U-Azavit";
                statueSystemCloudURIVariable.ChangeHandling.Value = CloudVariableChangeMode.Ignore;
                statueSystemCloudURIVariable.IsLinkedToCloud.Value = true;
                uriVariable = statueSystemCloudURIVariable;

                // Init editor
                WizardSlot = x;
                WizardSlot.Tag = "Developer";
                WizardSlot.PersistentSelf = false;

                LegacyCanvasPanel canvasPanel = WizardSlot.AttachComponent<LegacyCanvasPanel>();
                canvasPanel.Panel.AddCloseButton();
                canvasPanel.Panel.AddParentButton();
                canvasPanel.Panel.Title = WIZARD_TITLE;
                canvasPanel.Canvas.Size.Value = new float2(800f, 1000f);

                Slot Data = WizardSlot.AddSlot("Data");

                avatarRoot = Data.AddSlot("avatarRoot").AttachComponent<ReferenceField<Slot>>();
                statueSystemFallback = Data.AddSlot("statueSystemFallback").AttachComponent<ReferenceField<Slot>>();
                baseStatueMaterial = Data.AddSlot("baseMaterial").AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                statueType = Data.AddSlot("statueType").AttachComponent<ValueField<int>>();
                foundMeshRenderers = Data.AddSlot("foundMRs").AttachComponent<ReferenceMultiplexer<MeshRenderer>>();

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

                UI.SplitVertically(0.5f, out RectTransform top, out RectTransform bottom);

                UI.NestInto(top);

                UI.Style.MinHeight = 24f;
                UI.Style.PreferredHeight = 24f;
                UI.Style.PreferredWidth = 400f;
                UI.Style.MinWidth = 400f;

                VerticalLayout verticalLayout = UI.VerticalLayout(4f, childAlignment: Alignment.TopLeft);
                verticalLayout.ForceExpandHeight.Value = true;

                skinnedMeshRenderersOnly = UI.HorizontalElementWithLabel("Skinned Meshes only", 0.9f, () => UI.Checkbox("Skinned Meshes only", true));

                UI.Text("Avatar Root Slot:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Avatar Root Slot");
                var avatarField = UI.Current.AttachComponent<RefEditor>();
                avatarField.Setup(avatarRoot.Reference);
                avatarRoot.Reference.OnValueChange += (field) =>
                {
                    foundMeshRenderers.References.Clear();
                    if (skinnedMeshRenderersOnly.State.Value)
                    {
                        foundMeshRenderers.References.AddRange(avatarRoot.Reference.Target.GetComponentsInChildren<SkinnedMeshRenderer>());
                    }
                    else
                    {
                        foundMeshRenderers.References.AddRange(avatarRoot.Reference.Target.GetComponentsInChildren<MeshRenderer>());
                    }
                };

                UI.Text("Default statue material:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Base Texture");
                UI.Current.AttachComponent<RefEditor>().Setup(baseStatueMaterial.Reference);

                UI.Text("Statue transition type:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.HorizontalElementWithLabel("Alpha Fade", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.AlphaFade));
                UI.HorizontalElementWithLabel("Alpha Cutout", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.AlphaCutout));
                UI.HorizontalElementWithLabel("Plane Slicer", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.PlaneSlicer));
                UI.HorizontalElementWithLabel("Radial Slicer", 0.9f, () => UI.ValueRadio<int>(statueType.Value, (int)StatueType.RadialSlicer));

                UI.Spacer(24f);
                confirmButton = UI.Button("Statuefy!");
                confirmButton.LocalPressed += OnInstallButtonPressed;

                UI.Text("(Optional, Advanced) Override system:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("(Optional, Advanced) Override system:");
                UI.Current.AttachComponent<RefEditor>().Setup(statueSystemFallback.Reference);

                UI.Spacer(24f);


                UI.NestInto(bottom);

                UI.SplitVertically(0.06f, out RectTransform logTop, out RectTransform logBottom);

                UI.NestInto(logTop);
                var logTitle = UI.Text((LocaleString)"Log", true, new Alignment?(), true, null);
                logTitle.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                logTitle.VerticalAlign.Value = TextVerticalAlignment.Top;

                UI.NestInto(logBottom);
                UI.ScrollArea();
                UI.FitContent(SizeFit.Disabled, SizeFit.MinSize);
                UI.VerticalLayout();

                debugText = UI.Text((LocaleString)"", false, new Alignment?(), true, null);
                debugText.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                debugText.VerticalAlign.Value = TextVerticalAlignment.Top;
                debugText.Size.Value = 10f;
                debugText.VerticalAutoSize.Value = false;
                debugText.HorizontalAutoSize.Value = false;
                debugText.Slot.RemoveComponent(debugText.Slot.GetComponent<LayoutElement>());

                UI.NestInto(right);
                UI.ScrollArea();
                UI.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                SyncMemberEditorBuilder.Build(foundMeshRenderers.References, "Skinned Mesh Renderers found", null, UI);

                WizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
            }

            void OnInstallButtonPressed(IButton button, ButtonEventData eventData)
            {
                var scratchSpace = WizardSlot.AddSlot("Scratch space");
                try
                {
                    InstallSystemOnAvatar(avatarRoot.Reference.Target, scratchSpace, uriVariable, foundMeshRenderers.References.ToList(), baseStatueMaterial.Reference.Target, (StatueType)statueType.Value.Value);
                    HighlightHelper.FlashHighlight(avatarRoot.Reference.Target, (_a) => true, new colorX(0.5f, 0.5f, 0.5f, 1.0f));
                }
                catch (Exception ex)
                {
                    var errorString = $"ERROR: Encountered exception during install: {ex.Message} / {ex}";
                    this.LogError(errorString);
                    this.LogError("ERROR: Sorry! We ran into an error installing the statue system.<br>Debugging information has been copied to your clipboard; please send it to the Statue devs!<br>(Arion, Azavit, Nermerner)");
                    Engine.Current.InputInterface.Clipboard.SetText(this.debugText.Content.Value);
                }
            }

            public void InstallSystemOnAvatar(
            Slot avatarRoot,
            Slot scratchSpace,
            CloudValueVariable<string> uriVariable,
            List<MeshRenderer> normalSkinnedMeshRenderers,
            IAssetProvider<Material> baseStatueMaterial,
            StatueType statueType)
            {
                this.LogInfo("Starting InstallSystemOnAvatar for: " + avatarRoot.Name);

                // Start
                var avatarRootSlot = avatarRoot;

                // Attach dynvar space
                var avatarSpace = avatarRootSlot.GetComponent<DynamicVariableSpace>((space) => space.SpaceName == "Avatar");
                if (avatarSpace == null)
                {
                    avatarSpace = avatarRootSlot.AttachComponent<DynamicVariableSpace>();
                    avatarSpace.SpaceName.Value = "Avatar";
                    this.LogInfo("Created Avatar DynVarSpace");
                }
                else
                {
                    this.LogInfo("Avatar DynVarSpace already exists, skipping");
                }

                // Add statue system objects
                var systemSlot = GetStatueSystem(scratchSpace, uriVariable);
                var statueRootSloot = avatarRootSlot.AddSlot("Statue");

                this.LogInfo("Duplicating slots onto Avatar Root");

                systemSlot.GetChildrenWithTag("CopyToStatue").ForEach((childSlot) =>
                {
                    this.LogInfo($"Adding {childSlot.Name} with tag {childSlot.Tag}");
                    childSlot.Duplicate(statueRootSloot);
                });

                this.LogInfo($"Found {this.foundMeshRenderers} MeshRenderers");

                // Find unique slots to duplicate
                var normalUniqueSlots = new Dictionary<RefID, Slot>();
                foreach (var smr in normalSkinnedMeshRenderers)
                {
                    if (!normalUniqueSlots.ContainsKey(smr.Slot.ReferenceID))
                    {
                        normalUniqueSlots.Add(smr.Slot.ReferenceID, smr.Slot);

                        var smrsInChildren = smr.Slot.GetComponentsInChildren<MeshRenderer>();
                        if (smrsInChildren.Any(x => x != smr))
                        {
                            this.LogError($"Slot {smr.Slot.Name} has nested MeshRenderers, aborting");
                            statueRootSloot.Destroy();
                            scratchSpace.Destroy();
                            return;
                        }
                    }
                }

                this.LogInfo($"Found {normalUniqueSlots.Count} unique Slots to duplicate");

                // Duplicate each statue slot
                var statueSlots = new Dictionary<Slot, Slot>();
                normalUniqueSlots.ToList().ForEach((slot) =>
                {
                    statueSlots.Add(slot.Value, slot.Value.Duplicate());
                    statueSlots.Last().Value.Name = slot.Value.Name + "_Statue";
                });

                this.LogInfo($"Created {statueSlots.Count} statue slots");

                // Get SMRs for each statue slot
                var statueSkinnedMeshRenderers = new List<MeshRenderer>();
                foreach (var slot in statueSlots)
                {
                    var smrs = slot.Value.GetComponents<MeshRenderer>();
                    statueSkinnedMeshRenderers.AddRange(smrs);
                }

                this.LogInfo($"Creating material drivers");
                var driverSlot = statueRootSloot.AddSlot(name: "Drivers");
                // Oh lordy
                // var materialDriversSlot = driverSlot.AddSlot("Materials");

                // Materials:
                // 1. For each material that needs to be created, create a driver and default material
                // 2. For each old material, give it an appropriate blend mode

                // Create a map of normal materials -> statue materials
                var materialHolder = statueRootSloot.AddSlot("Generated Materials");

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


                this.LogInfo($"Created blinder material");

                // Create Material objects for each statue material
                var oldMaterialToStatueMaterialMap = new Dictionary<RefID, ReferenceMultiDriver<IAssetProvider<Material>>>();
                statueSkinnedMeshRenderers.ForEach((smr) =>
                {
                    for (int i = 0; i < smr.Materials.Count; ++i)
                    {
                        var material = smr.Materials[i];

                        if (!oldMaterialToStatueMaterialMap.ContainsKey(material.ReferenceID))
                        {
                            this.LogInfo($"Creating material {oldMaterialToStatueMaterialMap.Count + 1} as duplicate of {material.ReferenceID}");
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
                        else
                        {
                            this.LogInfo($"Material {i} was already created as {oldMaterialToStatueMaterialMap[material.ReferenceID].ReferenceID}");
                        }

                        var drives = oldMaterialToStatueMaterialMap[material.ReferenceID].Drives;
                        drives.Add().ForceLink(smr.Materials.GetElement(i));
                        // Thanks Dann :)
                    }
                });

                this.LogInfo($"Converting original materials to transparent versions");

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
                            this.LogInfo($"Creating material for {oldMaterial.ReferenceID}");
                            var newSlot = normalMaterialHolder.AddSlot($"Normal {oldMaterialToNewNormalMaterialMap.Count}");
                            var newMaterial = MaterialHelpers.CreateAlphaMaterial(oldMaterial, statueType, newSlot);
                            oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID] = newMaterial;
                        }
                        else
                        {
                            this.LogInfo($"Material {i} was already created as {oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID].ReferenceID}");
                        }

                        smr.Materials[i] = oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID];
                    }
                });

                // TODO: Hygiene: Create parent slots for avatars

                this.LogInfo("Creating drivers between normal/statue meshes and blend shapes");
                var statueSyncMeshes = driverSlot.AddSlot("Meshes");
                var statueSyncBlendshapes = driverSlot.AddSlot("Blend Shapes");

                foreach (var slot in statueSlots)
                {
                    // Remove any DirectVisemeDrivers from statue slots as these will be driven by ValueCopys
                    var visemeDriver = slot.Value.GetComponent<DirectVisemeDriver>();
                    if (visemeDriver != null)
                    {
                        slot.Value.RemoveComponent(visemeDriver);
                        this.LogInfo(string.Format("Removed DirectVisemeDriver on {0}", slot.Value.Name));
                    }

                    var blendshapeDrivers = statueSyncBlendshapes.AddSlot(slot.Key.Name);

                    // Since statue is duplicated from normal it is assumed there's the same number of SMRs
                    var normalSmrs = slot.Key.GetComponents<SkinnedMeshRenderer>();
                    var statueSmrs = slot.Value.GetComponents<SkinnedMeshRenderer>();

                    // Set up link between normal mesh and statue mesh
                    var meshCopy = statueSyncMeshes.AttachComponent<ValueCopy<bool>>();
                    meshCopy.Source.Value = slot.Key.ActiveSelf_Field.ReferenceID;
                    meshCopy.Target.Value = slot.Value.ActiveSelf_Field.ReferenceID;

                    for (var i = 0; i < normalSmrs.Count; i++)
                    {
                        var count = 0;
                        for (var j = 0; j < normalSmrs[i].BlendShapeCount; j++)
                        {
                            var normalBlendshapeName = normalSmrs[i].BlendShapeName(j);
                            var statueBlendshapeName = statueSmrs[i].BlendShapeName(j);
                            var normalBlendshape = normalSmrs[i].GetBlendShape(normalBlendshapeName);
                            var statueBlendshape = statueSmrs[i].GetBlendShape(statueBlendshapeName);

                            // Only ValueCopy driven blendshapes
                            if (normalBlendshapeName == statueBlendshapeName && normalBlendshape?.IsDriven == true && statueBlendshape != null)
                            {
                                var valueCopy = blendshapeDrivers.AttachComponent<ValueCopy<float>>();
                                valueCopy.Source.Value = normalBlendshape.ReferenceID;
                                valueCopy.Target.Value = statueBlendshape.ReferenceID;

                                count++;
                            }
                        }

                        this.LogInfo(string.Format("Linked {0} blend shapes for {1}", count, slot.Key.Name));
                    }
                }

                // Set up enabling drivers
                this.LogInfo($"Creating drivers for enabling/disabling normal/statue bodies");

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

                this.LogInfo($"Linking to BodyNormal");
                foreach (var smr in normalSkinnedMeshRenderers)
                {
                    normalDriver.Drives.Add().ForceLink(smr.EnabledField);
                }
                this.LogInfo($"Linked {normalSkinnedMeshRenderers.Count} MeshRenderers");

                var statueDriverSlot = driverSlot.AddSlot("Avatar/Statue.BodyStatue");
                var statueVarReader = statueDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
                var statueDriver = statueDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

                statueVarReader.VariableName.Value = "Avatar/Statue.BodyStatue";
                statueVarReader.DefaultValue.Value = false;
                statueVarReader.Target.Value = statueDriver.Value.ReferenceID;

                //statueSlots.ToList().ForEach((slot) =>
                //{
                //    statueDriver.Drives.Add();
                //    statueDriver.Drives[statueDriver.Drives.Count - 1].ForceLink(slot.ActiveSelf_Field);
                //});

                this.LogInfo($"Linking to BodyStatue");
                foreach (var smr in statueSkinnedMeshRenderers)
                {
                    statueDriver.Drives.Add().ForceLink(smr.EnabledField);
                }
                this.LogInfo($"Linked {statueSkinnedMeshRenderers.Count} MeshRenderers");

                var disableOnFreezeDriverSlot = driverSlot.AddSlot("Avatar/Statue.DisableOnFreeze");
                var dofVarReader = disableOnFreezeDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
                var dofDriver = disableOnFreezeDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

                dofVarReader.VariableName.Value = "Avatar/Statue.DisableOnFreeze";
                dofVarReader.DefaultValue.Value = true;
                dofVarReader.Target.Value = dofDriver.Value.ReferenceID;

                this.LogInfo($"Driving VRIK");

                AddFieldToMultidriver(dofDriver, avatarRootSlot.GetComponent<VRIK>().EnabledField);

                this.LogInfo($"Searching for bones to drive");
                var boneChainSlots = new Dictionary<RefID, Slot>();

                avatarRootSlot.GetComponentsInChildren<DynamicBoneChain>().ForEach((dbc) =>
                {
                    if (!boneChainSlots.ContainsKey(dbc.Slot.ReferenceID))
                    {
                        boneChainSlots.Add(dbc.Slot.ReferenceID, dbc.Slot);
                    }
                });

                boneChainSlots.ToList().ForEach((dbcSlot) => AddFieldToMultidriver(dofDriver, dbcSlot.Value.ActiveSelf_Field));

                this.LogInfo($"Added {boneChainSlots.Count} bones");

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

                avatarRootSlot.GetComponentsInChildren<AvatarToolAnchor>().ForEach((ata) =>
                {
                    if (ata.AnchorPoint.Value == AvatarToolAnchor.Point.Toolshelf)
                    {
                        AddFieldToMultidriver(dofDriver, ata.Slot.ActiveSelf_Field);
                    }
                });

                // Detect any name badges
                var nameBadges = avatarRootSlot.GetComponentsInChildren<AvatarNameTagAssigner>().Select((anta) => anta.Slot).ToList();
                if (nameBadges.Count > 0)
                {
                    foreach (var nameBadge in nameBadges)
                    {
                        var newParent = nameBadge.Parent.AddSlot("Name Badge Parent (Statufication)");
                        nameBadge.SetParent(newParent, true);
                        AddFieldToMultidriver(dofDriver, newParent.ActiveSelf_Field);
                        this.LogInfo($"Driving name badge {nameBadge.Name}/{nameBadge.ReferenceID}");
                    }
                }
                else
                {
                    this.LogWarn("No custom name badge found, name will be visible upon statufication");
                }

                this.LogInfo($"Driving WhisperVolume");
                var whisperVolSlot = driverSlot.AddSlot("Avatar/Statue.WhisperVolume");
                var whisperDriver = whisperVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
                whisperDriver.DefaultValue.Value = 0.75f;
                whisperDriver.VariableName.Value = "Avatar/Statue.WhisperVolume";
                whisperDriver.Target.Value = avatarRootSlot.GetComponentInChildren<AvatarAudioOutputManager>().WhisperConfig.Volume.ReferenceID;

                this.LogInfo($"Driving Voice and Shout");
                var voiceVolSlot = driverSlot.AddSlot("Avatar/Statue.VoiceVolume");
                var voiceDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
                var shoutDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
                var broadcastDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
                voiceDriver.DefaultValue.Value = 1.0f;
                voiceDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
                voiceDriver.Target.Value = avatarRootSlot.GetComponentInChildren<AvatarAudioOutputManager>().NormalConfig.Volume.ReferenceID;
                shoutDriver.DefaultValue.Value = 1.0f;
                shoutDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
                shoutDriver.Target.Value = avatarRootSlot.GetComponentInChildren<AvatarAudioOutputManager>().ShoutConfig.Volume.ReferenceID;
                broadcastDriver.DefaultValue.Value = 1.0f;
                broadcastDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
                broadcastDriver.Target.Value = avatarRootSlot.GetComponentInChildren<AvatarAudioOutputManager>().BroadcastConfig.Volume.ReferenceID;

                scratchSpace.Destroy();

                this.LogSuccess($"Setup completed successfully!");
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

            public Slot GetStatueSystem(Slot x, CloudValueVariable<string> uriVariable)
            {
                if (this.statueSystemFallback.Reference.Value != RefID.Null)
                {
                    this.LogInfo("Using statue system override from RefID " + this.statueSystemFallback.Reference.Value);

                    return this.statueSystemFallback.Reference.Target.Duplicate(x);
                }
                else
                {
                    this.LogInfo("Getting statue system from cloud");
                    // Yoinked from FrooxEngine.FileMetadata.OnImportFile
                    var fileName = uriVariable.Value.Value;
                    var fileUri = new Uri(fileName);

                    var record = x.Engine.RecordManager.FetchRecord(fileUri).GetAwaiter().GetResult();

                    this.LogDebug("Got Record " + record.ToString());
                    this.LogDebug("Fetching from " + record.Entity.AssetURI);

                    string fileData = x.Engine.AssetManager.GatherAssetFile(new Uri(record.Entity.AssetURI), 100.0f).GetAwaiter().GetResult();
                    
                    Msg(fileUri);
                    Msg(fileData);

                    if (fileData != null)
                    {
                        x.LocalUser.GetPointInFrontOfUser(out var point, out var rotation, float3.Backward);

                        this.LogInfo("Got file successfully");

                        return SpawnSlot(x, fileData, x.World, point, new float3(1.0f, 1.0f, 1.0f));
                    }
                    else
                    {
                        this.LogError("ERROR: File was null after RequestGather");

                        return x.AddSlot("File was null after RequestGather");
                    }
                }
            }

            private void AddFieldToMultidriver<T>(ValueMultiDriver<T> driver, Sync<T> field)
            {
                driver.Drives.Add();
                driver.Drives[driver.Drives.Count - 1].ForceLink(field);
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
