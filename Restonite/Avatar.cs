using Elements.Core;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Restonite
{
    internal class Avatar
    {
        #region Public Properties

        public Slot AvatarRoot { get; private set; }
        public bool HasExistingSystem { get; private set; }
        public bool HasLegacySystem { get; private set; }
        public List<MeshRendererMap> MeshRenderers { get; } = new List<MeshRendererMap>();
        public Slot StatueRoot { get; private set; }

        #endregion

        #region Public Methods

        public void CollectMaterials()
        {
            Log.Info("=== Collecting avatar materials");

            var normalMaterials = _scratchSpace.AddSlot("Normal Materials");
            var statueMaterials = _scratchSpace.AddSlot("Statue Materials");

            // Move all materials to scratch space slot temporarily
            foreach (var material in MeshRenderers.SelectMany(x => x.MaterialSets).SelectMany(x => x))
            {
                if (material.Normal != null && material.Normal.Slot != normalMaterials)
                {
                    Log.Debug($"Copying {material.Normal.ToLongString()} to {normalMaterials.ToShortString()}");
                    var newMaterial = MaterialHelpers.CopyMaterialToSlot(material.Normal, normalMaterials);

                    ChangeMaterialReferences(material.Normal, newMaterial);
                }

                if (material.Statue != null && material.Statue.Slot != statueMaterials)
                {
                    Log.Debug($"Copying {material.Statue.ToLongString()} to {statueMaterials.ToShortString()}");
                    var newMaterial = MaterialHelpers.CopyMaterialToSlot(material.Statue, statueMaterials);

                    ChangeMaterialReferences(material.Statue, newMaterial);
                }
            }
        }

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
                var visemeDriver = map.StatueSlot.GetComponent<DirectVisemeDriver>();
                if (visemeDriver != null)
                {
                    map.StatueSlot.RemoveComponent(visemeDriver);
                    Log.Info($"Removed DirectVisemeDriver on {map.StatueSlot.ToShortString()}");
                }

                var blendshapeDrivers = _blendshapes.AddSlot(map.NormalSlot.Name);

                // Set up link between normal mesh and statue mesh
                var meshCopy = _meshes.AttachComponent<ValueCopy<bool>>();
                meshCopy.Source.Value = map.NormalSlot.ActiveSelf_Field.ReferenceID;
                meshCopy.Target.Value = map.StatueSlot.ActiveSelf_Field.ReferenceID;

                var count = 0;
                for (var j = 0; j < normalSmr.BlendShapeCount; j++)
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

        public void CreateOrUpdateSlots()
        {
            Log.Info("=== Setting up statue root slot on avatar");

            if (StatueRoot == null)
                StatueRoot = AvatarRoot.AddSlot("Statue");
            StatueRoot.Tag = "StatueSystemSetupSlot";

            // Reparent old setups
            if (_generatedMaterials != null && _generatedMaterials.Parent != StatueRoot)
                _generatedMaterials.SetParent(StatueRoot, false);

            if (_drivers != null && _drivers.Parent != StatueRoot)
                _drivers.SetParent(StatueRoot, false);

            // Find existing slots
            _defaults = StatueRoot.FindChildOrAdd("Defaults");
            _userCustomization = StatueRoot.GetChildrenWithTag("StatueUserConfig").FirstOrDefault();

            _drivers = StatueRoot.FindChildOrAdd("Drivers");
            _meshes = _drivers.FindChildOrAdd("Meshes");
            _blendshapes = _drivers.FindChildOrAdd("Blend Shapes");

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

        public bool DuplicateMeshes()
        {
            var count = 0;

            Log.Info("=== Duplicating normal MeshRenderers to statue MeshRenderers");

            foreach (var map in MeshRenderers)
            {
                if (map.NormalSlot != null && map.StatueSlot == null)
                {
                    map.StatueSlot = map.NormalSlot.Duplicate();
                    map.StatueSlot.Name = map.NormalSlot.Name + "_Statue";

                    var statueRenderers = (_skinnedMeshRenderersOnly
                        ? map.StatueSlot.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>()
                        : map.StatueSlot.GetComponentsInChildren<MeshRenderer>()
                        ).ToList();

                    foreach (var renderer in statueRenderers)
                    {
                        var foundMap = MeshRenderers.Find(toSearch => toSearch.NormalMeshRenderer?.Mesh.Value == renderer.Mesh.Value && toSearch.NormalSlot == map.NormalSlot);
                        if (foundMap != null)
                        {
                            UpdateMeshRenderer(foundMap, map.StatueSlot, renderer);
                        }
                        else
                        {
                            Log.Error($"Couldn't find matching normal MeshRenderer for {renderer.ToLongString()}");
                            return false;
                        }
                    }

                    count++;

                    Log.Debug($"Duplicated {map.NormalSlot.ToShortString()} to {map.StatueSlot.ToShortString()}");
                }
            }

            foreach (var map in MeshRenderers)
            {
                if (map.NormalMaterialSet != null && map.StatueMaterialSet == null)
                {
                    var slot = map.StatueMeshRenderer.Slot;

                    map.StatueMaterialSet = slot.AttachComponent<MaterialSet>();
                    map.StatueMaterialSet.Target.ForceLink(map.StatueMeshRenderer.Materials);

                    foreach (var setNormal in map.NormalMaterialSet.Sets)
                    {
                        var setStatue = map.StatueMaterialSet.Sets.Add();
                        foreach (var mat in setNormal)
                            setStatue.Add();
                    }

                    var indexValueDriver = slot.AttachComponent<ValueCopy<int>>();
                    indexValueDriver.Source.Value = map.NormalMaterialSet.ActiveSetIndex.ReferenceID;
                    indexValueDriver.Target.Value = map.StatueMaterialSet.ActiveSetIndex.ReferenceID;
                }
                else if (map.NormalMaterialSet != null && map.StatueMaterialSet != null)
                {
                    // Ensure the same amount of sets
                    while (map.StatueMaterialSet.Sets.Count < map.NormalMaterialSet.Sets.Count)
                        map.StatueMaterialSet.Sets.Add();

                    while (map.StatueMaterialSet.Sets.Count > map.NormalMaterialSet.Sets.Count)
                        map.StatueMaterialSet.Sets.RemoveAt(map.StatueMaterialSet.Sets.Count - 1);

                    for (int i = 0; i < map.NormalMaterialSet.Sets.Count; i++)
                    {
                        // Ensure the same amount of material slots
                        while (map.StatueMaterialSet.Sets[i].Count < map.NormalMaterialSet.Sets[i].Count)
                            map.StatueMaterialSet.Sets[i].Add();

                        while (map.StatueMaterialSet.Sets[i].Count > map.NormalMaterialSet.Sets[i].Count)
                            map.StatueMaterialSet.Sets[i].RemoveAt(map.StatueMaterialSet.Sets[i].Count - 1);
                    }
                }
            }

            Log.Info($"Duplicated {count} statue slots");

            return true;
        }

        public void GenerateNormalMaterials()
        {
            Log.Info("=== Generating normal materials");

            // Destroy all existing children
            _normalMaterials.DestroyChildren();

            // Create alpha material and swap normal material for it
            var oldMaterialToNewNormalMaterialMap = new Dictionary<string, IAssetProvider<Material>>();
            for (int i = 0; i < MeshRenderers.Count; i++)
            {
                MeshRendererMap map = MeshRenderers[i];

                if (map.NormalMeshRenderer == null)
                    continue;

                for (int set = 0; set < map.MaterialSets.Count; set++)
                {
                    for (int slot = 0; slot < map.MaterialSets[set].Count; ++slot)
                    {
                        var name = $"{map.NormalMeshRenderer.ToLongString()}, material set {set}, slot {slot}";

                        var oldMaterial = map.MaterialSets[set][slot].Normal;
                        var statueType = map.MaterialSets[set][slot].TransitionType;
                        var key = $"{oldMaterial.ReferenceID}_{statueType}";

                        if (!oldMaterialToNewNormalMaterialMap.ContainsKey(key))
                        {
                            Log.Info($"Creating normal material {oldMaterialToNewNormalMaterialMap.Count} for {oldMaterial.ToLongString()} using {statueType}");

                            var newSlot = _normalMaterials.AddSlot($"Normal {oldMaterialToNewNormalMaterialMap.Count}");

                            // Create material based on transition type
                            var newMaterial = MaterialHelpers.CreateAlphaMaterial(oldMaterial, statueType, newSlot);

                            // Add dynvar with information about what transition type was used
                            var typeDynVar = newSlot.AttachComponent<DynamicValueVariable<string>>();
                            typeDynVar.VariableName.Value = $"Avatar/Statue.TransitionType{oldMaterialToNewNormalMaterialMap.Count}";
                            typeDynVar.Value.Value = $"{statueType}";

                            oldMaterialToNewNormalMaterialMap[key] = newMaterial;
                        }

                        if (map.NormalMaterialSet != null)
                        {
                            map.NormalMaterialSet.Sets[set][slot] = oldMaterialToNewNormalMaterialMap[key];
                        }
                        else
                        {
                            var materialSlot = map.NormalMeshRenderer.Materials.GetElement(slot);
                            var element = materialSlot.ActiveLink as SyncElement;
                            if (materialSlot.IsDriven && materialSlot.IsLinked)
                                Log.Warn($"{name} appears to already be driven by {element.Component.ToLongString()}, attempting to set anyway");

                            map.NormalMeshRenderer.Materials[slot] = oldMaterialToNewNormalMaterialMap[key];
                        }
                    }
                }
            }
        }

        public void GenerateStatueMaterials()
        {
            Log.Info("=== Generating statue materials");

            // Destroy all existing children
            _statueMaterials.DestroyChildren();

            // Create Material objects for each statue material
            var oldMaterialToStatueMaterialMap = new Dictionary<RefID, ReferenceMultiDriver<IAssetProvider<Material>>>();
            for (int i = 0; i < MeshRenderers.Count; i++)
            {
                MeshRendererMap map = MeshRenderers[i];
                var isBlinder = map.NormalMeshRenderer == null && map.StatueMeshRenderer == null;

                for (int set = 0; set < map.MaterialSets.Count; ++set)
                {
                    for (int slot = 0; slot < map.MaterialSets[set].Count; ++slot)
                    {
                        var name = map.StatueMeshRenderer == null ? "Blinder" : $"{map.StatueMeshRenderer.ToLongString()}, material set {set}, slot {slot}";

                        var normalMaterial = map.MaterialSets[set][slot].Normal;
                        var statueMaterial = map.MaterialSets[set][slot].Statue;
                        var defaultMaterialAsIs = isBlinder || map.MaterialSets[set][slot].UseAsIs;
                        var key = defaultMaterialAsIs ? statueMaterial.ReferenceID : normalMaterial.ReferenceID;

                        if (!isBlinder && normalMaterial == null && statueMaterial != null)
                        {
                            Log.Warn($"{map.NormalMeshRenderer.ToLongString()}, material {slot} is null, skipping statue material");
                            continue;
                        }

                        if (!oldMaterialToStatueMaterialMap.ContainsKey(key))
                        {
                            Log.Info($"Creating statue material {oldMaterialToStatueMaterialMap.Count} as duplicate of {key}");
                            Log.Debug(defaultMaterialAsIs ? "Using material as-is" : "Merging with normal material maps");

                            // If assigned == null, use default

                            // Create a new statue material object (i.e. drives material slot on
                            // statue SMR, has default material with normal map)
                            var newMaterialHolder = _statueMaterials.AddSlot($"Statue {oldMaterialToStatueMaterialMap.Count}");

                            var newDefaultMaterialRefId = defaultMaterialAsIs
                                ? newMaterialHolder.CopyComponent((AssetProvider<Material>)statueMaterial).ReferenceID
                                : MaterialHelpers.CreateStatueMaterial(normalMaterial, statueMaterial, newMaterialHolder).ReferenceID;

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
                            booleanReferenceDriver.TrueTarget.Value = newDefaultMaterialRefId;
                            bassignedMaterialDriver.Target.ForceLink(booleanReferenceDriver.FalseTarget);

                            // Checks if assigned material is null and writes that value to boolean
                            // ref driver
                            var equalityDriver = newMaterialHolder.AttachComponent<ReferenceEqualityDriver<IAssetProvider<Material>>>();
                            equalityDriver.TargetReference.Target = assignedMaterialField.Reference;
                            equalityDriver.Target.ForceLink(booleanReferenceDriver.State);

                            // Makes material accessible elsewhere
                            var dynMaterialVariable = newMaterialHolder.AttachComponent<DynamicReferenceVariable<IAssetProvider<Material>>>();
                            dynMaterialVariable.VariableName.Value = $"Avatar/Statue.Material{oldMaterialToStatueMaterialMap.Count}";

                            // boolean ref driver drives this, which drives everything else
                            var multiDriver = newMaterialHolder.AttachComponent<ReferenceMultiDriver<IAssetProvider<Material>>>();
                            booleanReferenceDriver.TargetReference.ForceLink(multiDriver.Reference);

                            // Drive that dynvar
                            multiDriver.Drives.Add();
                            multiDriver.Drives[0].ForceLink(dynMaterialVariable.Reference);

                            oldMaterialToStatueMaterialMap.Add(key, multiDriver);
                        }

                        if (map.StatueMeshRenderer != null && slot < map.StatueMeshRenderer.Materials.Count)
                        {
                            var drives = oldMaterialToStatueMaterialMap[key].Drives;

                            if (map.StatueMaterialSet != null)
                            {
                                drives.Add().ForceLink(map.StatueMaterialSet.Sets[set].GetElement(slot));
                            }
                            else
                            {
                                var materialSlot = map.StatueMeshRenderer.Materials.GetElement(slot);
                                var element = materialSlot.ActiveLink as SyncElement;
                                if (materialSlot.IsDriven && materialSlot.IsLinked)
                                    Log.Warn($"{name} appears to already be driven by {element.Component.ToLongString()}, attempting to set anyway");

                                drives.Add().ForceLink(materialSlot);
                            }
                        }

                        // Thanks Dann :)
                    }
                }
            }
        }

        public void InstallRemasterSystem(Slot systemSlot)
        {
            Log.Info("=== Installing Remaster system on avatar");

            // Remove the old system
            StatueRoot.DestroyChildren(filter: x => x.Tag == "CopyToStatue");

            // Install the new system
            foreach (var copySlot in systemSlot.GetChildrenWithTag("CopyToStatue"))
            {
                Log.Info($"Adding {copySlot.ToShortString()} with tag {copySlot.Tag}");
                copySlot.Duplicate(StatueRoot);
            }

            var oldDefaults = _defaults.GetComponentsInChildren<IDynamicVariable>().ConvertAll(x => new { Slot = ((Component)x).Slot, DynamicVariable = x });

            // Update user config slot
            if (_userCustomization == null)
            {
                var updateSlot = systemSlot.GetChildrenWithTag("StatueUserConfig").FirstOrDefault();

                if (updateSlot != null)
                {
                    Log.Info($"Adding {updateSlot.ToShortString()} with tag {updateSlot.Tag}");

                    updateSlot = updateSlot.Duplicate(StatueRoot);

                    var dynVars = updateSlot
                        .GetComponentsInChildren<IDynamicVariable>(filter: x => x.VariableName.StartsWith("Avatar/"))
                        .ConvertAll(x => new { Slot = ((Component)x).Slot, DynamicVariable = x });

                    // Update existing user config slots
                    var updateDynVars = oldDefaults.Join(dynVars,
                        defaults => defaults.DynamicVariable.VariableName,
                        system => system.DynamicVariable.VariableName,
                        (defaults, system) => new { Defaults = defaults, System = system }).ToList();
                    foreach (var dynVar in updateDynVars)
                    {
                        var a = dynVar.Defaults.DynamicVariable as dynamic;
                        var b = dynVar.System.DynamicVariable as dynamic;
                        var typeA = dynVar.Defaults.DynamicVariable.GetType();
                        var typeB = dynVar.System.DynamicVariable.GetType();

                        if (typeA.GetGenericTypeDefinition() == typeof(DynamicValueVariable<>) && typeB.GetGenericTypeDefinition() == typeof(DynamicValueVariable<>) && typeA.GenericTypeArguments[0] == typeB.GenericTypeArguments[0])
                        {
                            Log.Info($"Migrating user config slot for {dynVar.Defaults.DynamicVariable.VariableName}");
                            b.Value.Value = a.Value.Value;
                            dynVar.Defaults.Slot.Name = dynVar.System.Slot.Name;
                        }
                        else if (typeA.GetGenericTypeDefinition() == typeof(DynamicReferenceVariable<>) && typeB.GetGenericTypeDefinition() == typeof(DynamicReferenceVariable<>) && typeA.GenericTypeArguments[0] == typeB.GenericTypeArguments[0])
                        {
                            Log.Info($"Migrating user config slot for {dynVar.Defaults.DynamicVariable.VariableName}");
                            b.Reference.Value = a.Reference.Value;
                            dynVar.Defaults.Slot.Name = dynVar.System.Slot.Name;
                        }
                        else
                        {
                            Log.Warn($"User config slot for {dynVar.Defaults.DynamicVariable.VariableName} have differing data types, skipping");
                        }
                    }

                    _defaults?.Destroy();
                    _defaults = null;
                }
            }
            else
            {
                var existingDynVars = _userCustomization
                    .GetComponentsInChildren<IDynamicVariable>(filter: x => x.VariableName.StartsWith("Avatar/"))
                    .ConvertAll(x => new { Slot = ((Component)x).Slot, DynamicVariable = x });

                existingDynVars.AddRange(oldDefaults);

                var updateSlot = systemSlot.GetChildrenWithTag("StatueUserConfig").FirstOrDefault();

                if (updateSlot != null)
                {
                    Log.Info($"Updating {updateSlot.ToShortString()} with tag {updateSlot.Tag}");

                    var dynVars = updateSlot
                        .GetComponentsInChildren<IDynamicVariable>(filter: x => x.VariableName.StartsWith("Avatar/"))
                        .ConvertAll(x => new { Slot = ((Component)x).Slot, DynamicVariable = x });

                    // Clean up old user config slots no longer present in the system
                    var oldDynVars = existingDynVars.Where(x => !dynVars.Exists(y => y.DynamicVariable.VariableName == x.DynamicVariable.VariableName)).ToList();
                    foreach (var dynVar in oldDynVars)
                    {
                        Log.Info($"Removing old user config slot for {dynVar.DynamicVariable.VariableName}");
                        dynVar.Slot.Destroy();
                        existingDynVars.Remove(dynVar);
                    }

                    // Add new user config slots present in the system
                    var newDynVars = dynVars.Where(x => !existingDynVars.Exists(y => y.DynamicVariable.VariableName == x.DynamicVariable.VariableName)).ToList();
                    foreach (var dynVar in newDynVars)
                    {
                        Log.Info($"Adding new user config slot for {dynVar.DynamicVariable.VariableName}");
                        dynVar.Slot.Duplicate(_userCustomization);
                    }

                    // Update existing user config slots
                    var updateDynVars = existingDynVars.Join(dynVars,
                        existing => existing.DynamicVariable.VariableName,
                        system => system.DynamicVariable.VariableName,
                        (existing, system) => new { Existing = existing, System = system }).ToList();
                    foreach (var dynVar in updateDynVars)
                    {
                        var typeA = dynVar.Existing.DynamicVariable.GetType();
                        var typeB = dynVar.System.DynamicVariable.GetType();

                        if (typeA == typeB)
                        {
                            Log.Info($"Updating user config slot for {dynVar.Existing.DynamicVariable.VariableName}");
                            dynVar.Existing.Slot.Name = dynVar.System.Slot.Name;
                        }
                        else
                        {
                            Log.Warn($"User config slot for {dynVar.Existing.DynamicVariable.VariableName} have differing data types, overwriting");
                            dynVar.Existing.Slot.Destroy();
                            dynVar.System.Slot.Duplicate(_userCustomization);
                        }
                    }

                    _defaults?.Destroy();
                    _defaults = null;
                }
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

            var statue0Material = (IAssetProvider<Material>)_generatedMaterials?
                .FindChild("Statue Materials")?
                .FindChild("Statue 0")?
                .GetComponent<AssetProvider<Material>>();

            if ((defaultMaterial == null || defaultMaterial.ReferenceID == RefID.Null) && statue0Material != null)
            {
                defaultMaterial = statue0Material;
                Log.Debug($"Using existing default statue material, {statue0Material.ToShortString()}");
            }
            else if (defaultMaterial != null && defaultMaterial.ReferenceID != RefID.Null)
            {
                Log.Info($"Using user supplied default statue material, {defaultMaterial.ToShortString()}");
            }
            else
            {
                Log.Warn("Couldn't find a material to use for default statue material");
            }

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

        public void RemoveLegacySystem()
        {
            if (_legacySystem != null)
            {
                Log.Info("=== Removing legacy system");

                var legacyDisableOnStatueSlot = _legacySystem.FindChild("Disable on Statue", false, false, -1);
                if (legacyDisableOnStatueSlot != null)
                {
                    var existingMultiDriver = legacyDisableOnStatueSlot.GetComponent<ValueMultiDriver<bool>>();
                    if (existingMultiDriver != null)
                    {
                        foreach (var drive in existingMultiDriver.Drives)
                        {
                            var worker = drive.Target.FindNearestParent<Worker>();
                            if (worker is DynamicBoneChain dbc)
                            {
                                Log.Debug($"Moving drive for {dbc.ToShortString()} to slot {dbc.Slot.ToShortString()}");
                                _existingDrivesForDisableOnFreeze.Add(dbc.Slot.ActiveSelf_Field);
                            }
                            else if (worker is EyeManager em)
                            {
                                Log.Debug($"Moving drive for {em.ToShortString()} to slot {em.Slot.ToShortString()}");
                                _existingDrivesForDisableOnFreeze.Add(em.Slot.ActiveSelf_Field);
                            }
                            else
                            {
                                _existingDrivesForDisableOnFreeze.Add(drive.Target);
                            }
                        }
                    }
                }
                Log.Info($"Collected {_existingDrivesForDisableOnFreeze.Count} drivers for Disable on Statue");

                var blinder = AvatarRoot.GetComponent<VRIK>()?.Solver.BoneReferences.head.Slot?.FindChild("Blinder");
                if (blinder?.ActiveSelf_Field.IsDriven == true)
                {
                    Log.Info($"Removing {blinder.ToShortString()}");
                    blinder.Destroy();
                }

                var smoothTransforms = AvatarRoot.GetComponentsInChildren<SmoothTransform>(slotFilter: x => x.Name == "Target" && x.Parent.Name.EndsWith("Proxy"));
                foreach (var component in smoothTransforms)
                {
                    Log.Info($"Removing {component.ToLongString()}");
                    component.Slot.RemoveComponent(component);
                }

                _legacySystem.Destroy();
                _legacyAddons.Destroy();
            }
        }

        public void RemoveMeshRenderer(RefID refID)
        {
            var map = MeshRenderers.Find(x => x.NormalMeshRenderer?.ReferenceID == refID);
            RemoveMeshRenderer(map);
        }

        public void RemoveMeshRenderer(MeshRendererMap map)
        {
            if (map != null)
            {
                Log.Info($"Removing {map.NormalMeshRenderer.ToLongString()} from setup");
                MeshRenderers.Remove(map);
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

        public bool VerifyInstallRequirements()
        {
            foreach (var map in MeshRenderers)
            {
                if (map.NormalMeshRenderer != null)
                {
                    // Check for any nested MeshRenderers
                    var smrsInChildren = map.NormalMeshRenderer.Slot.GetComponentsInChildren<MeshRenderer>();
                    if (smrsInChildren.Exists(x => x != map.NormalMeshRenderer))
                    {
                        Log.Error($"Slot {map.NormalMeshRenderer.Slot.ToShortString()} has nested MeshRenderers, aborting");
                        return false;
                    }
                }

                for (int set = 0; set < map.MaterialSets.Count; set++)
                {
                    for (int i = 0; i < map.MaterialSets[set].Count; i++)
                    {
                        var material = map.MaterialSets[set][i];

                        if (material.Normal != null)
                        {
                            // Check for incompatible transition types for materials
                            if ((material.TransitionType == StatueType.PlaneSlicer || material.TransitionType == StatueType.RadialSlicer)
                                && !(material.Normal is IPBS_Metallic) && !(material.Normal is IPBS_Specular) && !(material.Normal is PBS_DistanceLerpMaterial))
                            {
                                Log.Error($"{material.GetType().Name} does not support {material.TransitionType}, aborting");
                                return false;
                            }
                            else if ((material.TransitionType == StatueType.AlphaFade || material.TransitionType == StatueType.AlphaCutout)
                                && !(material.Normal is PBS_DualSidedMetallic) && !(material.Normal is PBS_DualSidedSpecular)
                                && !(material.Normal is IPBS_Metallic) && !(material.Normal is IPBS_Specular)
                                && !(material.Normal is XiexeToonMaterial) && !(material.Normal is PBS_DistanceLerpMaterial))
                            {
                                Log.Error($"{material.GetType().Name} does not support {material.TransitionType}, aborting");
                                return false;
                            }
                        }

                        // Check for missing statue materials
                        if (material.Statue == null || material.Statue.ReferenceID == RefID.Null)
                        {
                            Log.Error("Missing default statue material for some material slots, aborting");
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        #endregion

        #region Private Fields

        private Slot _blendshapes;
        private Slot _defaults;
        private Slot _drivers;
        private List<IField<bool>> _existingDrivesForDisableOnFreeze = new List<IField<bool>>();
        private Slot _generatedMaterials;
        private Slot _legacyAddons;
        private Slot _legacySystem;
        private Slot _meshes;
        private Slot _normalMaterials;
        private Slot _scratchSpace;
        private bool _skinnedMeshRenderersOnly;
        private Slot _statueMaterials;
        private Slot _userCustomization;

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

        private void AddMeshRenderer(MeshRenderer normal, MeshRenderer statue, IAssetProvider<Material> defaultMaterial, StatueType transitionType, bool useDefaultAsIs)
        {
            var rendererMap = new MeshRendererMap
            {
                NormalSlot = normal.Slot,
                NormalMeshRenderer = normal,

                StatueSlot = statue?.Slot,
                StatueMeshRenderer = statue,
            };

            Log.Debug($"Mapping {normal.ToLongString()} to {statue.ToLongString()}");

            if (HasMaterialSet(normal, out var normalMaterialSet))
            {
                rendererMap.NormalMaterialSet = normalMaterialSet;
                Log.Debug($"    Normal MeshRenderer has {normalMaterialSet.ToShortString()} with {normalMaterialSet.Sets.Count} sets");
            }

            if (HasMaterialSet(statue, out var statueMaterialSet))
            {
                rendererMap.StatueMaterialSet = statueMaterialSet;
                Log.Debug($"    Statue MeshRenderer has {statueMaterialSet.ToShortString()} with {statueMaterialSet.Sets.Count} sets");
            }

            if (rendererMap.NormalMaterialSet != null)
            {
                rendererMap.MaterialSets = rendererMap.NormalMaterialSet.Sets.Select(set => set.Select(material => new MaterialMap
                {
                    Normal = material,
                    Statue = defaultMaterial,
                    TransitionType = transitionType,
                    UseAsIs = useDefaultAsIs,
                }).ToList()).ToList();
            }
            else
            {
                rendererMap.MaterialSets = new List<List<MaterialMap>>()
                {
                    normal.Materials.Select(x => new MaterialMap
                    {
                        Normal = x,
                        Statue = defaultMaterial,
                        TransitionType = transitionType,
                        UseAsIs = useDefaultAsIs,
                    }).ToList()
                };
            }

            for (int set = 0; set < rendererMap.MaterialSets.Count; set++)
            {
                for (int i = 0; i < rendererMap.MaterialSets[set].Count; i++)
                {
                    MaterialMap material = rendererMap.MaterialSets[set][i];
                    if (material.Statue != null)
                        Log.Debug($"    Material set {set}, slot {i} with {material.Normal.ToShortString()} linked to {material.Statue.ToShortString()}");
                }
            }

            MeshRenderers.Add(rendererMap);
        }

        private void ChangeMaterialReferences(IAssetProvider<Material> material, IAssetProvider<Material> newMaterial)
        {
            foreach (var map in MeshRenderers.SelectMany(x => x.MaterialSets).SelectMany(x => x))
            {
                // Update material references for normal
                if (map.Normal == material)
                    map.Normal = newMaterial;

                // Update material references for statue
                if (map.Statue == material)
                    map.Statue = newMaterial;
            }
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

        private bool HasMaterialSet(MeshRenderer renderer, out MaterialSet materialSet)
        {
            if (renderer != null && renderer.Materials.IsDriven && renderer.Materials.IsLinked)
            {
                var element = renderer.Materials.ActiveLink as SyncElement;
                if (element.Component is MaterialSet)
                {
                    materialSet = element.Component as MaterialSet;
                    return true;
                }
            }

            materialSet = null;
            return false;
        }

        private bool IsHierarchyEnabled(Slot slot)
        {
            var hierarchy = new List<Slot>();

            while (slot != AvatarRoot)
            {
                hierarchy.Add(slot);
                slot = slot.Parent;
            }

            return hierarchy.TrueForAll(x => x.ActiveSelf);
        }

        private bool IsStatueMeshRenderer(MeshRenderer renderer)
        {
            // Check name first
            var names = new string[] { "statue", "petrified", "stone" };
            foreach (var name in names)
            {
                if (renderer.Slot.Name.ToLower().Contains(name) || renderer.Slot.Parent.Name.ToLower().Contains(name))
                    return true;
            }

            // Check if Enabled is driven by existing system
            if (renderer.EnabledField.IsDriven && renderer.EnabledField.IsLinked)
            {
                var element = renderer.EnabledField.ActiveLink as SyncElement;
                if (element.Slot.Name == "Avatar/Statue.BodyStatue" || element.Slot.Name == "Body Statue Active")
                    return true;
            }

            // Check if Slot is driven by existing system
            if (renderer.Slot.ActiveSelf_Field.IsDriven && renderer.Slot.ActiveSelf_Field.IsLinked)
            {
                var element = renderer.Slot.ActiveSelf_Field.ActiveLink as SyncElement;
                if (element.Slot.Name == "Avatar/Statue.BodyStatue" || element.Slot.Name == "Body Statue Active")
                    return true;
            }

            // Check if Slot's parent is driven by existing system
            if (renderer.Slot.Parent.ActiveSelf_Field.IsDriven && renderer.Slot.Parent.ActiveSelf_Field.IsLinked)
            {
                var element = renderer.Slot.Parent.ActiveSelf_Field.ActiveLink as SyncElement;
                if (element.Slot.Name == "Avatar/Statue.BodyStatue" || element.Slot.Name == "Body Statue Active")
                    return true;
            }

            return false;
        }

        private void UpdateMeshRenderer(MeshRendererMap rendererMap, Slot statueSlot, MeshRenderer statue)
        {
            Log.Debug($"Mapping {rendererMap.NormalMeshRenderer.ToLongString()} to {statue.ToLongString()}");

            rendererMap.StatueSlot = statueSlot;
            rendererMap.StatueMeshRenderer = statue;

            if (HasMaterialSet(statue, out var statueMaterialSet))
            {
                rendererMap.StatueMaterialSet = statueMaterialSet;
                Log.Debug($"    Statue MeshRenderer has {statueMaterialSet.ToShortString()} with {statueMaterialSet.Sets.Count} sets");
            }
        }

        #endregion
    }
}
