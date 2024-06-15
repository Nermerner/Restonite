using Elements.Core;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Restonite
{
    internal partial class Avatar
    {
        #region Public Properties

        public Slot AvatarRoot { get; private set; }
        public bool HasExistingSystem { get; private set; }
        public bool HasLegacySystem { get; private set; }
        public Slot StatueRoot { get; private set; }

        #endregion

        #region Public Methods

        public void CopyBlendshapes()
        {
            Log.Info("=== Creating drivers between normal/statue slots and blend shapes");

            // Remove existing drivers
            _meshes.RemoveAllComponents(_ => true);
            _blendshapes.DestroyChildren();

            var meshCount = 0;
            foreach (var map in MeshRenderers)
            {
                if (map.NormalSlot == null || map.StatueSlot == null)
                    continue;

                if (!(map.NormalMeshRenderer is SkinnedMeshRenderer normalSmr) || !(map.StatueMeshRenderer is SkinnedMeshRenderer statueSmr))
                    continue;

                // Remove any DirectVisemeDrivers from statue slots as these will be driven by ValueCopys
                var visemeDrivers = map.StatueSlot.GetComponents<DirectVisemeDriver>();
                foreach (var visemeDriver in visemeDrivers)
                {
                    map.StatueSlot.RemoveComponent(visemeDriver);
                    Log.Info($"Removed DirectVisemeDriver on {map.StatueSlot.ToShortString()}");
                }

                // Remove any AvatarExpressionDrivers from statue slots as these will be driven by ValueCopys
                var expressionDrivers = map.StatueSlot.GetComponents<AvatarExpressionDriver>();
                foreach (var expressionDriver in expressionDrivers)
                {
                    map.StatueSlot.RemoveComponent(expressionDriver);
                    Log.Info($"Removed AvatarExpressionDriver on {map.StatueSlot.ToShortString()}");
                }

                var blendshapeDrivers = _blendshapes.AddSlot(map.NormalSlot.Name);

                // Set up link between normal mesh and statue mesh
                var meshCopy = _meshes.AttachComponent<ValueCopy<bool>>();
                meshCopy.Source.Value = map.NormalSlot.ActiveSelf_Field.ReferenceID;
                meshCopy.Target.Value = map.StatueSlot.ActiveSelf_Field.ReferenceID;

                var count = 0;
                for (var j = 0; j < normalSmr.BlendShapeCount; j++)
                {
                    try
                    {
                        // Get the blendshape for the normal and statue mesh for a given index
                        var normalBlendshapeName = normalSmr.BlendShapeName(j);
                        var normalBlendshape = normalSmr.GetBlendShape(normalBlendshapeName);

                        var statueBlendshapeName = statueSmr.BlendShapeName(j);
                        var statueBlendshape = statueSmr.GetBlendShape(statueBlendshapeName);

                        // Only ValueCopy driven blendshapes
                        if (normalBlendshapeName == statueBlendshapeName && normalBlendshape?.IsDriven == true && statueBlendshape != null)
                        {
                            var valueCopy = blendshapeDrivers.AttachComponent<ValueCopy<float>>();
                            valueCopy.Source.Value = normalBlendshape.ReferenceID;
                            valueCopy.Target.Value = statueBlendshape.ReferenceID;

                            count++;
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Have seen this happen Ignore and continue
                    }
                }

                Log.Info($"Linked {count} blend shapes for {map.NormalSlot.ToShortString()}");
                meshCount++;
            }

            Log.Info($"Linked active state for {meshCount} slots");
        }

        public void CreateOrUpdateDefaults()
        {
            if (_defaults != null)
            {
                Log.Info("=== Creating defaults configuration");

                var durationDefault = _defaults.GetComponent<DynamicValueVariable<float>>(x => x.VariableName.Value == "Avatar/Statue.Duration.Default");
                if (durationDefault == null)
                {
                    durationDefault = _defaults.AttachComponent<DynamicValueVariable<float>>();
                    durationDefault.VariableName.Value = "Avatar/Statue.Duration.Default";
                    durationDefault.Value.Value = 10;
                }

                var whisperPersist = _defaults.GetComponent<DynamicValueVariable<bool>>(x => x.VariableName.Value == "Avatar/Statue.Whisper.Persist");
                if (whisperPersist == null)
                {
                    whisperPersist = _defaults.AttachComponent<DynamicValueVariable<bool>>();
                    whisperPersist.VariableName.Value = "Avatar/Statue.Whisper.Persist";
                    whisperPersist.Value.Value = true;
                }
            }
        }

        public void CreateOrUpdateDisableOnFreeze()
        {
            Log.Info("=== Creating drivers for disable on freeze");

            void AddFieldToMultidriver<T>(ValueMultiDriver<T> driver, IField<T> field) => driver.Drives.Add().ForceLink(field);

            // Check for existing configuration and save the fields being driven
            var disableOnFreezeDriverSlot = _drivers.FindChildOrAdd("Avatar/Statue.DisableOnFreeze");
            var existingMultiDriver = disableOnFreezeDriverSlot.GetComponent<ValueMultiDriver<bool>>();
            if (existingMultiDriver != null)
            {
                foreach (var drive in existingMultiDriver.Drives)
                {
                    _existingDrivesForDisableOnFreeze.Add(drive.Target);
                }
            }

            disableOnFreezeDriverSlot.RemoveAllComponents(_ => true);

            var dofVarReader = disableOnFreezeDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var dofDriver = disableOnFreezeDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            dofVarReader.VariableName.Value = "Avatar/Statue.DisableOnFreeze";
            dofVarReader.DefaultValue.Value = true;
            dofVarReader.Target.Value = dofDriver.Value.ReferenceID;

            Log.Info("Driving VRIK");

            AvatarRoot.GetComponents<VRIK>().ForEach((component) => AddFieldToMultidriver(dofDriver, component.EnabledField));

            var boneChainSlots = new Dictionary<RefID, Slot>();

            AvatarRoot.GetComponentsInChildren<DynamicBoneChain>().ForEach((dbc) =>
            {
                if (!boneChainSlots.ContainsKey(dbc.Slot.ReferenceID))
                {
                    boneChainSlots.Add(dbc.Slot.ReferenceID, dbc.Slot);
                }
            });

            boneChainSlots.ToList().ForEach((dbcSlot) => AddFieldToMultidriver(dofDriver, dbcSlot.Value.ActiveSelf_Field));

            Log.Info($"Driving {boneChainSlots.Count} dynamic bones");

            // Disable visemes
            var count = 0;
            AvatarRoot.GetComponentsInChildren<VisemeAnalyzer>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<DirectVisemeDriver>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            Log.Info($"Driving {count} viseme components");

            // Disable animation systems (Wigglers, Panners, etc.)
            count = 0;
            AvatarRoot.GetComponentsInChildren<Wiggler>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Panner1D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Panner2D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Panner3D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Panner4D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Wobbler1D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Wobbler2D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Wobbler3D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<Wobbler4D>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            Log.Info($"Driving {count} animation system components");

            // Disable avatar expressions
            count = 0;
            AvatarRoot.GetComponentsInChildren<AvatarExpressionDriver>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<HandPoser>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.EnabledField); count++; });
            AvatarRoot.GetComponentsInChildren<EyeManager>().ForEach((component) => { AddFieldToMultidriver(dofDriver, component.Slot.ActiveSelf_Field); count++; });
            Log.Info($"Driving {count} avatar expression components");

            // Disable toolshelfs
            count = 0;
            AvatarRoot.GetComponentsInChildren<AvatarToolAnchor>().ForEach((component) =>
            {
                if (component.AnchorPoint.Value == AvatarToolAnchor.Point.Toolshelf)
                {
                    AddFieldToMultidriver(dofDriver, component.Slot.ActiveSelf_Field);
                    count++;
                }
            });
            Log.Info($"Driving {count} tool shelves");

            // Detect any name badges
            var nameBadges = AvatarRoot.GetComponentsInChildren<AvatarNameTagAssigner>().ConvertAll((anta) => anta.Slot);
            if (nameBadges.Count > 0)
            {
                foreach (var nameBadge in nameBadges)
                {
                    var newParent = nameBadge.Parent.Name == "Name Badge Parent (Statufication)"
                        ? nameBadge.Parent
                        : nameBadge.Parent.AddSlot("Name Badge Parent (Statufication)");

                    if (newParent != nameBadge.Parent)
                        nameBadge.SetParent(newParent, true);

                    AddFieldToMultidriver(dofDriver, newParent.ActiveSelf_Field);
                    Log.Info($"Driving name badge {nameBadge.ToShortString()}");
                }
            }
            else
            {
                Log.Warn("No custom name badge found, name will be visible upon statufication");
            }

            // Add any custom drives from the previous setup to the multidriver
            count = 0;
            var customDrives = _existingDrivesForDisableOnFreeze.Except(dofDriver.Drives.Select(x => x.Target)).Distinct();
            foreach (var customDrive in customDrives)
            {
                AddFieldToMultidriver(dofDriver, customDrive);
                count++;
            }
            Log.Info($"Driving {count} custom components/slots");
        }

        public void CreateOrUpdateEnableDrivers()
        {
            Log.Info("=== Creating drivers for enabling/disabling normal/statue bodies");

            var normalDriverSlot = _drivers.FindChildOrAdd("Avatar/Statue.BodyNormal");
            normalDriverSlot.RemoveAllComponents(_ => true);

            var normalVarReader = normalDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var normalDriver = normalDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            normalVarReader.VariableName.Value = "Avatar/Statue.BodyNormal";
            normalVarReader.DefaultValue.Value = true;
            normalVarReader.Target.Value = normalDriver.Value.ReferenceID;

            var count = 0;
            foreach (var smr in MeshRenderers.Where(x => x.NormalMeshRenderer != null))
            {
                normalDriver.Drives.Add().ForceLink(smr.NormalMeshRenderer.EnabledField);
                count++;
            }
            Log.Info($"Linked {count} normal MeshRenderers to BodyNormal");

            var statueDriverSlot = _drivers.FindChildOrAdd("Avatar/Statue.BodyStatue");
            statueDriverSlot.RemoveAllComponents(_ => true);

            var statueVarReader = statueDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var statueDriver = statueDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            statueVarReader.VariableName.Value = "Avatar/Statue.BodyStatue";
            statueVarReader.DefaultValue.Value = false;
            statueVarReader.Target.Value = statueDriver.Value.ReferenceID;

            count = 0;
            foreach (var smr in MeshRenderers.Where(x => x.StatueMeshRenderer != null))
            {
                statueDriver.Drives.Add().ForceLink(smr.StatueMeshRenderer.EnabledField);
                count++;
            }
            Log.Info($"Linked {count} statue MeshRenderers to BodyStatue");
        }

        public void CreateOrUpdateSlots(SyncRef<Slot> installSlot)
        {
            Log.Info("=== Setting up statue root slot on avatar");

            var rootInstallSlot = AvatarRoot;
            if (installSlot.Value != RefID.Null)
                rootInstallSlot = installSlot.Target;

            // Install to the selected slot
            if (StatueRoot == null)
                StatueRoot = rootInstallSlot.AddSlot("Statue");

            // Reparent if a slot is given and the root is not already parented under it
            if (installSlot.Value != RefID.Null && StatueRoot.Parent != rootInstallSlot)
                StatueRoot.SetParent(rootInstallSlot, false);

            StatueRoot.Tag = "StatueSystemSetupSlot";

            // Reparent old setups
            if (_generatedMaterials != null && _generatedMaterials.Parent != StatueRoot)
                _generatedMaterials.SetParent(StatueRoot, false);

            if (_drivers != null && _drivers.Parent != StatueRoot)
                _drivers.SetParent(StatueRoot, false);

            // Find existing slots
            _defaults = StatueRoot.FindChildOrAdd("Defaults");
            _userConfig = StatueRoot.GetChildrenWithTag("StatueUserConfig").FirstOrDefault();

            _drivers = StatueRoot.FindChildOrAdd("Drivers");
            _meshes = _drivers.FindChildOrAdd("Meshes");
            _blendshapes = _drivers.FindChildOrAdd("Blend Shapes");

            _originalMaterials = StatueRoot.FindChildOrAdd("Original Materials");
            _originalStatueMaterials = _originalMaterials.FindChildOrAdd("Statue Materials");
            _originalNormalMaterials = _originalMaterials.FindChildOrAdd("Normal Materials");

            _generatedMaterials = StatueRoot.FindChildOrAdd("Generated Materials");
            _statueMaterials = _generatedMaterials.FindChildOrAdd("Statue Materials");
            _normalMaterials = _generatedMaterials.FindChildOrAdd("Normal Materials");

            // Clear up tags from adding slots
            foreach (var slot in StatueRoot.GetChildrenWithTag("StatueSystemSetupSlot").Where(x => x != StatueRoot))
                slot.Tag = null;
        }

        public void CreateOrUpdateVoiceDrivers()
        {
            Log.Info("=== Driving whisper volume");

            var whisperVolSlot = _drivers.FindChildOrAdd("Avatar/Statue.WhisperVolume");
            whisperVolSlot.RemoveAllComponents(_ => true);

            var whisperDriver = whisperVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            whisperDriver.DefaultValue.Value = 0.75f;
            whisperDriver.VariableName.Value = "Avatar/Statue.WhisperVolume";
            whisperDriver.Target.Value = AvatarRoot.GetComponentInChildren<AvatarAudioOutputManager>().WhisperConfig.Volume.ReferenceID;

            Log.Info("=== Driving normal, shout and broadcast volume");

            var voiceVolSlot = _drivers.FindChildOrAdd("Avatar/Statue.VoiceVolume");
            voiceVolSlot.RemoveAllComponents(_ => true);

            var voiceDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            var shoutDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            var broadcastDriver = voiceVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            voiceDriver.DefaultValue.Value = 1.0f;
            voiceDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
            voiceDriver.Target.Value = AvatarRoot.GetComponentInChildren<AvatarAudioOutputManager>().NormalConfig.Volume.ReferenceID;
            shoutDriver.DefaultValue.Value = 1.0f;
            shoutDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
            shoutDriver.Target.Value = AvatarRoot.GetComponentInChildren<AvatarAudioOutputManager>().ShoutConfig.Volume.ReferenceID;
            broadcastDriver.DefaultValue.Value = 1.0f;
            broadcastDriver.VariableName.Value = "Avatar/Statue.VoiceVolume";
            broadcastDriver.Target.Value = AvatarRoot.GetComponentInChildren<AvatarAudioOutputManager>().BroadcastConfig.Volume.ReferenceID;
        }

        public void OpenUserConfigInspector()
        {
            if (_userConfig != null)
            {
                _userConfig.OpenInspectorForTarget();
                Log.Success("Check Statue User Config slot for system configuration options.");
            }
            else if (_defaults != null)
            {
                _defaults.OpenInspectorForTarget();
                Log.Success("Check Defaults slot for system configuration options.");
            }
        }

        public void ReadAvatarRoot(Slot newAvatarRoot, IAssetProvider<Material> defaultMaterial, bool skinnedMeshRenderersOnly, bool useDefaultAsIs, StatueType transitionType)
        {
            MeshRenderers.Clear();
            AvatarRoot = newAvatarRoot;
            StatueRoot = null;
            HasExistingSystem = false;
            HasLegacySystem = false;
            _skinnedMeshRenderersOnly = skinnedMeshRenderersOnly;
            _existingDrivesForDisableOnFreeze.Clear();

            if (AvatarRoot == null)
                return;

            Log.Clear();
            Log.Info($"=== Reading avatar {AvatarRoot.ToShortString()}");

            var children = AvatarRoot.GetAllChildren();

            StatueRoot = FindSlot(children, slot => slot.FindChild("Drivers") != null && slot.FindChild("Generated Materials") != null, "Statue", "StatueSystemSetupSlot");
            _generatedMaterials = FindSlot(children, slot => slot.FindChild("Statue Materials") != null && slot.FindChild("Normal Materials") != null, "Generated Materials");
            _drivers = FindSlot(children, slot => slot.FindChild("Avatar/Statue.BodyNormal") != null, "Drivers");

            _legacySystem = AvatarRoot.FindChildInHierarchy("<color=#dadada>Statuefication</color>");
            _legacyAddons = AvatarRoot.FindChildInHierarchy("<color=#dadada>Statue Add-Ons</color>");

            Log.Debug($"Statue root is {StatueRoot.ToShortString()}");
            Log.Debug($"Generated materials is {_generatedMaterials.ToShortString()}");
            Log.Debug($"Drivers is {_drivers.ToShortString()}");
            Log.Debug($"Legacy system is {_legacySystem.ToShortString()}");
            Log.Debug($"Legacy addons is {_legacyAddons.ToShortString()}");

            if (StatueRoot != null || (_drivers != null && _generatedMaterials != null))
            {
                HasExistingSystem = true;
                Log.Info("Avatar has existing Remaster system");
            }

            if (_legacySystem != null || _legacyAddons != null)
            {
                HasLegacySystem = true;
                Log.Info("Avatar has legacy system installed");
            }

            defaultMaterial = GetDefaultMaterial(defaultMaterial);

            MeshRenderers.Add(new MeshRendererMap
            {
                MaterialSets = new List<List<MaterialMap>>()
                    {
                        new List<MaterialMap>()
                        {
                            new MaterialMap
                            {
                                Statue = defaultMaterial,
                            }
                        }
                    }
            });

            var renderers = (skinnedMeshRenderersOnly
                ? AvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>()
                : AvatarRoot.GetComponentsInChildren<MeshRenderer>()
                ).ToList();

            var meshes = renderers.Select(x => x.Mesh.Value).Distinct();
            foreach (var mesh in meshes)
            {
                var renderersForMesh = renderers.Where(x => x.Mesh.Value == mesh).ToList();

                var normal = renderersForMesh.Where(x => !IsStatueMeshRenderer(x)).ToList();
                var statue = renderersForMesh.Where(x => IsStatueMeshRenderer(x)).ToList();

                if (normal.Count == 1 && statue.Count == 1)
                {
                    AddMeshRenderer(normal[0], statue[0], defaultMaterial, transitionType, useDefaultAsIs);
                }
                else
                {
                    foreach (var normalRenderer in normal)
                    {
                        var statueRenderer = statue.Select(x => (CommonStartSubstring(normalRenderer.Slot.Name, x.Slot.Name), x)).OrderByDescending(x => x.Item1).Take(1).Select(x => x.Item2).FirstOrDefault();
                        if (statueRenderer != null)
                        {
                            AddMeshRenderer(normalRenderer, statueRenderer, defaultMaterial, transitionType, useDefaultAsIs);
                            statue.Remove(statueRenderer);
                        }
                        else
                        {
                            AddMeshRenderer(normalRenderer, null, defaultMaterial, transitionType, useDefaultAsIs);
                        }
                    }
                }
            }
        }

        public void SetScratchSpace(Slot scratchSpace)
        {
            _scratchSpace = scratchSpace;
        }

        public void SetupRootDynVar()
        {
            // Attach dynvar space
            var avatarSpace = AvatarRoot.GetComponent<DynamicVariableSpace>((space) => space.SpaceName == "Avatar");
            if (avatarSpace == null)
            {
                avatarSpace = AvatarRoot.AttachComponent<DynamicVariableSpace>();
                avatarSpace.SpaceName.Value = "Avatar";
                Log.Info("Created Avatar DynVarSpace");
            }
            else
            {
                Log.Info("Avatar DynVarSpace already exists, skipping");
            }
        }

        public void UpdateParameters(IAssetProvider<Material> defaultMaterial, bool useDefaultAsIs, StatueType transitionType)
        {
            defaultMaterial = GetDefaultMaterial(defaultMaterial);

            foreach (var map in MeshRenderers)
            {
                foreach (var set in map.MaterialSets)
                {
                    foreach (var mat in set)
                    {
                        mat.Statue = defaultMaterial;
                        mat.UseAsIs = useDefaultAsIs;
                        mat.TransitionType = transitionType;
                    }
                }
            }
        }

        #endregion

        #region Private Fields

        private readonly List<IField<bool>> _existingDrivesForDisableOnFreeze = new List<IField<bool>>();
        private Slot _blendshapes;
        private Slot _defaults;
        private Slot _drivers;
        private Slot _generatedMaterials;
        private Slot _legacyAddons;
        private Slot _legacySystem;
        private Slot _meshes;
        private Slot _normalMaterials;
        private Slot _originalMaterials;
        private Slot _originalNormalMaterials;
        private Slot _originalStatueMaterials;
        private Slot _scratchSpace;
        private bool _skinnedMeshRenderersOnly;
        private Slot _statueMaterials;
        private Slot _userConfig;

        #endregion

        #region Private Methods

        private static Slot FindSlot(List<Slot> slots, Predicate<Slot> predicate, string name = null, string tag = null)
        {
            foreach (var slot in slots)
            {
                if (((name == null && tag == null) || (tag != null && slot.Tag == tag) || (name != null && slot.Name == name)) && predicate(slot))
                    return slot;
            }

            return null;
        }

        private int CommonStartSubstring(string a, string b)
        {
            var commonLength = 0;

            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                if (a[i] == b[i])
                    commonLength++;
                else
                    break;
            }

            return commonLength;
        }

        private bool IsDrivenByKnownStatueDriver(Sync<bool> field)
        {
            if (field.IsDriven && field.IsLinked && field.ActiveLink is SyncElement element)
            {
                if (element.Slot.Name == "Avatar/Statue.BodyStatue" || element.Slot.Name == "Body Statue Active")
                    return true;

                var dynVar = element.Slot.GetComponent<DynamicValueVariableDriver<bool>>(x => x.VariableName == "Avatar/Statue.BodyStatue");
                if (dynVar != null)
                    return true;

                if (element.Component is ValueMultiDriver<bool> multiDriver)
                    return IsDrivenByKnownStatueDriver(multiDriver.Value);
            }

            return false;
        }

        #endregion
    }
}
