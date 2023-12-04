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

        public void CopyBlendshapes()
        {
            Log.Info("=== Creating drivers between normal/statue slots and blend shapes");

            // Remove existing drivers
            _meshes.RemoveAllComponents(_ => true);
            _blendshapes.DestroyChildren();

            foreach (var slot in _slotsMap)
            {
                var normal = slot.Key;
                var statue = slot.Value;

                // Remove any DirectVisemeDrivers from statue slots as these will be driven by ValueCopys
                var visemeDriver = statue.GetComponent<DirectVisemeDriver>();
                if (visemeDriver != null)
                {
                    statue.RemoveComponent(visemeDriver);
                    Log.Info($"Removed DirectVisemeDriver on {statue.ToShortString()}");
                }

                var blendshapeDrivers = _blendshapes.AddSlot(normal.Name);

                // Since statue is duplicated from normal it is assumed there's the same number of SMRs
                var normalSmrs = normal.GetComponents<SkinnedMeshRenderer>();
                var statueSmrs = statue.GetComponents<SkinnedMeshRenderer>();

                // Set up link between normal mesh and statue mesh
                var meshCopy = _meshes.AttachComponent<ValueCopy<bool>>();
                meshCopy.Source.Value = normal.ActiveSelf_Field.ReferenceID;
                meshCopy.Target.Value = statue.ActiveSelf_Field.ReferenceID;

                for (var i = 0; i < normalSmrs.Count; i++)
                {
                    var count = 0;
                    for (var j = 0; j < normalSmrs[i].BlendShapeCount; j++)
                    {
                        // Get the blendshape for the normal and statue mesh for a given index
                        var normalBlendshapeName = normalSmrs[i].BlendShapeName(j);
                        var normalBlendshape = normalSmrs[i].GetBlendShape(normalBlendshapeName);

                        var statueBlendshapeName = statueSmrs[i].BlendShapeName(j);
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

                    Log.Info($"Linked {count} blend shapes for {normal.ToShortString()}");
                }
            }
        }

        public void CreateOrUpdateDefaults()
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

        public void CreateOrUpdateDisableOnFreeze()
        {
            Log.Info("=== Creating drivers for disable on freeze");

            void AddFieldToMultidriver<T>(ValueMultiDriver<T> driver, IField<T> field) => driver.Drives.Add().ForceLink(field);

            var disableOnFreezeDriverSlot = _drivers.FindChildOrAdd("Avatar/Statue.DisableOnFreeze");

            // Check for existing configuration and save the fields being driven
            var existingMultiDriver = disableOnFreezeDriverSlot.GetComponent<ValueMultiDriver<bool>>();
            var existingDrives = new List<IField<bool>>();
            if (existingMultiDriver != null)
            {
                foreach (var drive in existingMultiDriver.Drives)
                {
                    existingDrives.Add(drive.Target);
                }
            }

            disableOnFreezeDriverSlot.RemoveAllComponents(_ => true);

            var dofVarReader = disableOnFreezeDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var dofDriver = disableOnFreezeDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            dofVarReader.VariableName.Value = "Avatar/Statue.DisableOnFreeze";
            dofVarReader.DefaultValue.Value = true;
            dofVarReader.Target.Value = dofDriver.Value.ReferenceID;

            Log.Info("Driving VRIK");

            AddFieldToMultidriver(dofDriver, AvatarRoot.GetComponent<VRIK>().EnabledField);

            Log.Info("Searching for bones to drive");
            var boneChainSlots = new Dictionary<RefID, Slot>();

            AvatarRoot.GetComponentsInChildren<DynamicBoneChain>().ForEach((dbc) =>
            {
                if (!boneChainSlots.ContainsKey(dbc.Slot.ReferenceID))
                {
                    boneChainSlots.Add(dbc.Slot.ReferenceID, dbc.Slot);
                }
            });

            boneChainSlots.ToList().ForEach((dbcSlot) => AddFieldToMultidriver(dofDriver, dbcSlot.Value.ActiveSelf_Field));

            Log.Info($"Added {boneChainSlots.Count} bones");

            AddFieldToMultidriver(dofDriver, AvatarRoot.GetComponentInChildren<VisemeAnalyzer>().EnabledField);

            AvatarRoot.GetComponentsInChildren<AvatarExpressionDriver>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });

            AvatarRoot.GetComponentsInChildren<DirectVisemeDriver>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });

            // Disable animation systems (Wigglers, Panners, etc.)
            AvatarRoot.GetComponentsInChildren<Wiggler>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Panner1D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Panner2D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Panner3D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Panner4D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Wobbler1D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Wobbler2D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Wobbler3D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });
            AvatarRoot.GetComponentsInChildren<Wobbler4D>().ForEach((aed) =>
            {
                AddFieldToMultidriver(dofDriver, aed.EnabledField);
            });

            AvatarRoot.GetComponentsInChildren<HandPoser>().ForEach((hp) =>
            {
                AddFieldToMultidriver(dofDriver, hp.EnabledField);
            });

            AvatarRoot.GetComponentsInChildren<EyeManager>().ForEach((em) =>
            {
                AddFieldToMultidriver(dofDriver, em.Slot.ActiveSelf_Field);
            });

            AvatarRoot.GetComponentsInChildren<AvatarToolAnchor>().ForEach((ata) =>
            {
                if (ata.AnchorPoint.Value == AvatarToolAnchor.Point.Toolshelf)
                {
                    AddFieldToMultidriver(dofDriver, ata.Slot.ActiveSelf_Field);
                }
            });

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
            var customDrives = existingDrives.Except(dofDriver.Drives.Select(x => x.Target));
            foreach (var customDrive in customDrives)
            {
                AddFieldToMultidriver(dofDriver, customDrive);
            }
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

            Log.Info($"=== Linking normal slots to BodyNormal");
            foreach (var smr in MeshRenderers.Where(x => x.NormalMeshRenderer != null))
            {
                normalDriver.Drives.Add().ForceLink(smr.NormalMeshRenderer.EnabledField);
            }

            var statueDriverSlot = _drivers.FindChildOrAdd("Avatar/Statue.BodyStatue");
            statueDriverSlot.RemoveAllComponents(_ => true);

            var statueVarReader = statueDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var statueDriver = statueDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            statueVarReader.VariableName.Value = "Avatar/Statue.BodyStatue";
            statueVarReader.DefaultValue.Value = false;
            statueVarReader.Target.Value = statueDriver.Value.ReferenceID;

            Log.Info($"=== Linking statue slots to BodyStatue");
            foreach (var smr in MeshRenderers.Where(x => x.StatueMeshRenderer != null))
            {
                statueDriver.Drives.Add().ForceLink(smr.StatueMeshRenderer.EnabledField);
            }

            Log.Info($"Linked {MeshRenderers.Count} MeshRenderers");
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

            _drivers = StatueRoot.FindChildOrAdd("Drivers");
            _meshes = _drivers.FindChildOrAdd("Meshes");
            _blendshapes = _drivers.FindChildOrAdd("Blend Shapes");

            _generatedMaterials = StatueRoot.FindChildOrAdd("Generated Materials");
            _statueMaterials = _generatedMaterials.FindChildOrAdd("Statue Materials");
            _normalMaterials = _generatedMaterials.FindChildOrAdd("Normal Materials");
        }

        public void CreateOrUpdateVoiceDrivers()
        {
            Log.Info("=== Driving WhisperVolume");

            var whisperVolSlot = _drivers.FindChildOrAdd("Avatar/Statue.WhisperVolume");
            whisperVolSlot.RemoveAllComponents(_ => true);

            var whisperDriver = whisperVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            whisperDriver.DefaultValue.Value = 0.75f;
            whisperDriver.VariableName.Value = "Avatar/Statue.WhisperVolume";
            whisperDriver.Target.Value = AvatarRoot.GetComponentInChildren<AvatarAudioOutputManager>().WhisperConfig.Volume.ReferenceID;

            Log.Info("=== Driving Voice and Shout");

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
            var error = false;

            Log.Info("=== Duplicating normal meshes to statue meshes");

            _slotsMap.ToList().ForEach(x =>
            {
                if (x.Value == null)
                {
                    var statue = x.Key.Duplicate();
                    statue.Name = x.Key.Name + "_Statue";

                    var statueRenderers = (_skinnedMeshRenderersOnly
                        ? statue.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>()
                        : statue.GetComponentsInChildren<MeshRenderer>()
                        ).ToList();

                    foreach (var renderer in statueRenderers)
                    {
                        var map = MeshRenderers.Find(toSearch => toSearch.NormalMeshRenderer?.Mesh.Value == renderer.Mesh.Value);
                        if (map != null)
                        {
                            map.StatueMeshRenderer = renderer;

                            Log.Debug($"{map.NormalMeshRenderer.ToLongString()} linked to {map.StatueMeshRenderer.ToLongString()}");
                        }
                        else
                        {
                            Log.Error($"Couldn't find matching normal MeshRenderer for {renderer.ToLongString()}");
                            error |= true;
                        }
                    }

                    _slotsMap[x.Key] = statue;
                    count++;

                    Log.Debug($"Duplicated {x.Key.ToShortString()} to {statue.ToShortString()}");
                }
            });

            Log.Info($"Duplicated {count} statue slots");

            return !error;
        }

        public void GenerateNormalMaterials()
        {
            Log.Info("=== Generating normal materials");

            // Move all statue materials to slot temporarily
            foreach (var material in MeshRenderers.SelectMany(x => x.Materials).Select(x => x.Normal).Where(x => x != null))
            {
                if (material.Slot.Parent == _normalMaterials)
                {
                    Log.Debug($"Copying {material.ToLongString()} to {_normalMaterials.ToShortString()}");
                    var newMaterial = (AssetProvider<Material>)_normalMaterials.CopyComponent((AssetProvider<Material>)material);

                    ChangeMaterialReferences(material, newMaterial);
                }
            }

            // Destroy all existing children
            _normalMaterials.DestroyChildren();

            var oldMaterialToNewNormalMaterialMap = new Dictionary<string, IAssetProvider<Material>>();
            // Create alpha material and swap normal material for it
            MeshRenderers.ForEach((map) =>
            {
                if (map.NormalMeshRenderer == null)
                    return;

                for (int i = 0; i < map.Materials.Count; ++i)
                {
                    var name = $"{map.NormalMeshRenderer.ToLongString()}, material {i}";

                    var oldMaterial = map.Materials[i].Normal;
                    var statueType = map.Materials[i].TransitionType;
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
                    else
                    {
                        Log.Info($"Material on {name} was already created");
                    }

                    if (!map.NormalMeshRenderer.Materials[i].IsDriven)
                        map.NormalMeshRenderer.Materials[i] = oldMaterialToNewNormalMaterialMap[key];
                    else
                        Log.Warn($"{name} is already driven");
                }
            });

            _normalMaterials.RemoveAllComponents(_ => true);
        }

        public void GenerateStatueMaterials()
        {
            Log.Info("=== Generating statue materials");

            // Move all statue materials to slot temporarily
            foreach (var material in MeshRenderers.SelectMany(x => x.Materials).Select(x => x.Statue))
            {
                if (material != null && material.Slot.Parent == _statueMaterials)
                {
                    Log.Debug($"Copying {material.ToLongString()} to {_statueMaterials.ToShortString()}");
                    var newMaterial = (AssetProvider<Material>)_statueMaterials.MoveComponent((AssetProvider<Material>)material);

                    ChangeMaterialReferences(material, newMaterial);
                }
            }

            // Destroy all existing children
            _statueMaterials.DestroyChildren();

            // Create Material objects for each statue material
            var oldMaterialToStatueMaterialMap = new Dictionary<RefID, ReferenceMultiDriver<IAssetProvider<Material>>>();
            MeshRenderers.ForEach((map) =>
            {
                var isBlinder = map.NormalMeshRenderer == null && map.StatueMeshRenderer == null;

                for (int i = 0; i < map.Materials.Count; ++i)
                {
                    var name = map.StatueMeshRenderer == null ? "Blinder" : $"{map.StatueMeshRenderer.ToLongString()}, material {i}";

                    var normalMaterial = map.Materials[i].Normal;
                    var statueMaterial = map.Materials[i].Statue;
                    var defaultMaterialAsIs = isBlinder || map.Materials[i].UseAsIs;
                    var key = defaultMaterialAsIs ? statueMaterial.ReferenceID : normalMaterial.ReferenceID;

                    if (!isBlinder && normalMaterial == null && statueMaterial != null)
                    {
                        Log.Warn($"{map.NormalMeshRenderer.ToLongString()}, material {i} is null, skipping statue material");
                        continue;
                    }

                    if (!oldMaterialToStatueMaterialMap.ContainsKey(key))
                    {
                        Log.Info($"Creating statue material {oldMaterialToStatueMaterialMap.Count} as duplicate of {key}");
                        Log.Debug(defaultMaterialAsIs ? "Using material as-is" : "Merging with normal material maps");

                        // If assigned == null, use default

                        // Create a new statue material object (i.e. drives material slot on statue
                        // SMR, has default material with normal map)
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

                        // Checks if assigned material is null and writes that value to boolean ref driver
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
                    else
                    {
                        Log.Info($"Material on {name} was already created");
                    }

                    if (map.StatueMeshRenderer != null)
                    {
                        if (!map.StatueMeshRenderer.Materials[i].IsDriven)
                        {
                            Log.Debug($"Driving {name} using {key}");
                            var drives = oldMaterialToStatueMaterialMap[key].Drives;
                            drives.Add().ForceLink(map.StatueMeshRenderer.Materials.GetElement(i));
                        }
                        else
                        {
                            Log.Warn($"{name} is already driven");
                        }
                    }

                    // Thanks Dann :)
                }
            });

            _statueMaterials.RemoveAllComponents(_ => true);
        }

        public void InstallRemasterSystem(Slot systemSlot)
        {
            Log.Info("=== Installing Remaster system on avatar");

            // Remove the old system
            StatueRoot.DestroyChildren(filter: x => x.Tag == "CopyToStatue");

            // Install the new system
            systemSlot.GetChildrenWithTag("CopyToStatue").ForEach((childSlot) =>
            {
                Log.Info($"Adding {childSlot.Name} with tag {childSlot.Tag}");
                childSlot.Duplicate(StatueRoot);
            });
        }

        public void ReadAvatarRoot(Slot newAvatarRoot, IAssetProvider<Material> defaultMaterial, bool skinnedMeshRenderersOnly, bool useDefaultAsIs, StatueType transitionType)
        {
            MeshRenderers.Clear();
            AvatarRoot = newAvatarRoot;
            StatueRoot = null;
            HasExistingSystem = false;
            HasLegacySystem = false;
            _skinnedMeshRenderersOnly = skinnedMeshRenderersOnly;

            if (AvatarRoot == null)
                return;

            Log.Info($"=== Reading avatar {AvatarRoot.ToShortString()}");

            var children = AvatarRoot.GetAllChildren();

            var renderers = (skinnedMeshRenderersOnly
                ? AvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>()
                : AvatarRoot.GetComponentsInChildren<MeshRenderer>()
                ).ToList();

            // Find unique slot
            var uniqueSlots = new Dictionary<RefID, Slot>();
            foreach (var smr in renderers)
            {
                if (!uniqueSlots.ContainsKey(smr.Slot.ReferenceID))
                {
                    uniqueSlots.Add(smr.Slot.ReferenceID, smr.Slot);
                }
            }

            // Map normal slots to statue slots
            _slotsMap = new Dictionary<Slot, Slot>();
            foreach (var slot in uniqueSlots)
            {
                if (!slot.Value.Name.Contains("Statue"))
                {
                    var existingStatueSlot = uniqueSlots
                        .Where(x => x.Value.Name.Contains(slot.Value.Name) && x.Value.Name.Contains("Statue"))
                        .Select(x => x.Value)
                        .FirstOrDefault();

                    _slotsMap.Add(slot.Value, existingStatueSlot);

                    Log.Debug($"Mapping {slot.Value.ToShortString()} to {existingStatueSlot.ToShortString()}");
                }
            }

            Log.Info($"Found {_slotsMap.Count} unique slots");

            foreach (var map in _slotsMap)
            {
                var normalRenderers = (skinnedMeshRenderersOnly
                    ? map.Key.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>()
                    : map.Key.GetComponentsInChildren<MeshRenderer>()
                    ).ToList();
                var statueRenderers = (skinnedMeshRenderersOnly
                    ? map.Value?.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>() ?? Enumerable.Empty<MeshRenderer>()
                    : map.Value?.GetComponentsInChildren<MeshRenderer>() ?? Enumerable.Empty<MeshRenderer>()
                    ).ToList();

                MeshRenderers.Add(new MeshRendererMap
                {
                    Materials = new List<MaterialMap>()
                        {
                            new MaterialMap
                            {
                                Statue = defaultMaterial,
                            }
                        }
                });

                foreach (var normal in normalRenderers)
                {
                    var statue = statueRenderers.Find(x => x.Mesh.Value == normal.Mesh.Value);

                    var rendererMap = new MeshRendererMap
                    {
                        NormalMeshRenderer = normal,
                        StatueMeshRenderer = statue,
                        Materials = normal.Materials.Select(x => new MaterialMap
                        {
                            Normal = x,
                            Statue = defaultMaterial,
                            TransitionType = transitionType,
                            UseAsIs = useDefaultAsIs,
                        }).ToList()
                    };

                    Log.Debug($"Linking {normal.ToLongString()} to {statue.ToLongString()}");

                    for (int i = 0; i < rendererMap.Materials.Count; i++)
                    {
                        MaterialMap material = rendererMap.Materials[i];
                        if(material.Statue != null)
                            Log.Debug($"Material slot {i} with {material.Normal.ToShortString()} linked to {material.Statue.ToShortString()}");
                    }

                    MeshRenderers.Add(rendererMap);
                }
            }

            StatueRoot = FindSlot(children, slot => slot.FindChild("Drivers") != null && slot.FindChild("Generated Materials") != null, "Statue", "StatueSystemSetupSlot");
            _generatedMaterials = FindSlot(children, slot => slot.FindChild("Statue Materials") != null && slot.FindChild("Normal Materials") != null, "Generated Materials");
            _drivers = FindSlot(children, slot => slot.FindChild("Avatar/Statue.BodyNormal") != null, "Drivers");

            var legacySystem = AvatarRoot.FindChildInHierarchy("<color=#dadada>Statuefication</color>");
            var legacyAddons = AvatarRoot.FindChildInHierarchy("<color=#dadada>Statue Add-Ons</color>");

            Log.Debug($"Statue root is {StatueRoot.ToShortString()}");
            Log.Debug($"Generated materials is {_generatedMaterials.ToShortString()}");
            Log.Debug($"Drivers is {_drivers.ToShortString()}");
            Log.Debug($"Legacy system is {legacySystem.ToShortString()}");
            Log.Debug($"Legacy addons is {legacyAddons.ToShortString()}");

            if (StatueRoot != null || (_drivers != null && _generatedMaterials != null))
            {
                HasExistingSystem = true;
                Log.Info("Avatar has existing Remaster system");
            }

            if (legacySystem != null || legacyAddons != null)
            {
                HasLegacySystem = true;
                Log.Info("Avatar has legacy system installed");
            }

            if (defaultMaterial == null || defaultMaterial.ReferenceID == RefID.Null)
            {
                var statue0Material = (IAssetProvider<Material>)_generatedMaterials?
                    .FindChild("Statue Materials")?
                    .FindChild("Statue 0")?
                    .GetComponent<AssetProvider<Material>>();

                if (statue0Material != null)
                {
                    Log.Info("Using existing avatar default statue material");

                    foreach (var map in MeshRenderers)
                    {
                        if (map.NormalMeshRenderer == null && map.StatueMeshRenderer == null)
                            Log.Debug($"Using {statue0Material.ToShortString()} as blinder material");
                        else
                            Log.Debug($"Updating {map.NormalMeshRenderer.ToLongString()} material mappings");

                        for (var i = 0; i < map.Materials.Count; i++)
                        {
                            if (map.Materials[i].Statue != statue0Material)
                            {
                                map.Materials[i].Statue = statue0Material;
                                if (map.NormalMeshRenderer != null && map.StatueMeshRenderer != null)
                                    Log.Debug($"Material slot {i} with {map.Materials[i].Normal.ToShortString()} linked to {statue0Material.ToShortString()}");
                            }
                        }
                    }
                }
                else
                {
                    Log.Warn("Couldn't find a material to use for default statue material");
                }
            }
            else
            {
                foreach (var map in MeshRenderers)
                {
                    if (map.NormalMeshRenderer == null && map.StatueMeshRenderer == null)
                        Log.Debug($"Using {defaultMaterial.ToShortString()} as blinder material");
                    else
                        Log.Debug($"Updating {map.NormalMeshRenderer.ToLongString()} material mappings");

                    for (var i = 0; i < map.Materials.Count; i++)
                    {
                        if (map.Materials[i].Statue != defaultMaterial)
                        {
                            map.Materials[i].Statue = defaultMaterial;
                            if (map.NormalMeshRenderer != null && map.StatueMeshRenderer != null)
                                Log.Debug($"Material slot {i} with {map.Materials[i].Normal.ToShortString()} linked to {defaultMaterial.ToShortString()}");
                        }
                    }
                }

                Log.Info("Using user supplied default statue material");
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

                foreach (var material in map.Materials)
                {
                    if (material.Normal != null)
                    {
                        // Check for incompatible transition types for materials
                        if ((material.TransitionType == StatueType.PlaneSlicer || material.TransitionType == StatueType.RadialSlicer)
                            && !(material.Normal is IPBS_Metallic) && !(material.Normal is IPBS_Specular))
                        {
                            Log.Error($"{material.GetType().Name} does not support {material.TransitionType}, aborting");
                            return false;
                        }
                        else if ((material.TransitionType == StatueType.AlphaFade || material.TransitionType == StatueType.AlphaCutout)
                            && !(material.Normal is PBS_DualSidedMetallic) && !(material.Normal is PBS_DualSidedSpecular)
                            && !(material.Normal is IPBS_Metallic) && !(material.Normal is IPBS_Specular)
                            && !(material.Normal is XiexeToonMaterial))
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

            return true;
        }

        #endregion

        #region Private Fields

        private Slot _blendshapes;
        private Slot _defaults;
        private Slot _drivers;
        private Slot _generatedMaterials;
        private Slot _meshes;
        private Slot _normalMaterials;
        private bool _skinnedMeshRenderersOnly;
        private Dictionary<Slot, Slot> _slotsMap = new Dictionary<Slot, Slot>();
        private Slot _statueMaterials;

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

        private void ChangeMaterialReferences(IAssetProvider<Material> material, IAssetProvider<Material> newMaterial)
        {
            foreach (var map in MeshRenderers.SelectMany(x => x.Materials))
            {
                // Update material references for normal
                if (map.Normal == material)
                    map.Normal = newMaterial;

                // Update material references for statue
                if (map.Statue == material)
                    map.Statue = newMaterial;
            }
        }

        #endregion
    }
}
