using Elements.Core;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using Microsoft.SqlServer.Server;
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

        private bool _skinnedMeshRenderersOnly;

        public List<MeshRendererMap> MeshRenderers { get; } = new List<MeshRendererMap>();
        public Slot StatueRoot { get; private set; }

        private Slot _generatedMaterials;

        #endregion

        #region Public Methods

        public void CopyBlendshapes()
        {
            Log.Info("Creating drivers between normal/statue meshes and blend shapes");

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
                    Log.Info(string.Format("Removed DirectVisemeDriver on {0}", statue.Name));
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

                    Log.Info(string.Format("Linked {0} blend shapes for {1}", count, normal.Name));
                }
            }
        }

        public void CreateOrUpdateDefaults()
        {
            Log.Info("Creating defaults configuration");

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

                    if(newParent != nameBadge.Parent)
                        nameBadge.SetParent(newParent, true);

                    AddFieldToMultidriver(dofDriver, newParent.ActiveSelf_Field);
                    Log.Info($"Driving name badge {nameBadge.Name}/{nameBadge.ReferenceID}");
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
            Log.Info($"Creating drivers for enabling/disabling normal/statue bodies");

            var normalDriverSlot = _drivers.FindChildOrAdd("Avatar/Statue.BodyNormal");
            normalDriverSlot.RemoveAllComponents(_ => true);

            var normalVarReader = normalDriverSlot.AttachComponent<DynamicValueVariableDriver<bool>>();
            var normalDriver = normalDriverSlot.AttachComponent<ValueMultiDriver<bool>>();

            normalVarReader.VariableName.Value = "Avatar/Statue.BodyNormal";
            normalVarReader.DefaultValue.Value = true;
            normalVarReader.Target.Value = normalDriver.Value.ReferenceID;

            Log.Info($"Linking to BodyNormal");
            foreach (var smr in MeshRenderers)
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

            Log.Info($"Linking to BodyStatue");
            foreach (var smr in MeshRenderers)
            {
                statueDriver.Drives.Add().ForceLink(smr.StatueMeshRenderer.EnabledField);
            }

            Log.Info($"Linked {MeshRenderers.Count} MeshRenderers");
        }

        public void CreateOrUpdateSlots()
        {
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
            Log.Info("Driving WhisperVolume");

            var whisperVolSlot = _drivers.FindChildOrAdd("Avatar/Statue.WhisperVolume");
            whisperVolSlot.RemoveAllComponents(_ => true);

            var whisperDriver = whisperVolSlot.AttachComponent<DynamicValueVariableDriver<float>>();
            whisperDriver.DefaultValue.Value = 0.75f;
            whisperDriver.VariableName.Value = "Avatar/Statue.WhisperVolume";
            whisperDriver.Target.Value = AvatarRoot.GetComponentInChildren<AvatarAudioOutputManager>().WhisperConfig.Volume.ReferenceID;

            Log.Info("Driving Voice and Shout");

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

        public void DuplicateMeshes()
        {
            var count = 0;
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
                        var map = MeshRenderers.Find(toSearch => toSearch.NormalMeshRenderer.Mesh.Value == renderer.Mesh.Value);
                        map.StatueMeshRenderer = renderer;

                        Log.Debug($"MeshRenderer {map.NormalMeshRenderer.ReferenceID} linked to {map.StatueMeshRenderer?.ReferenceID ?? RefID.Null}");
                    }

                    _slotsMap[x.Key] = statue;
                    count++;

                    Log.Debug($"Duplicated {x.Key.Name} to {statue.Name}");
                }
            });

            Log.Info($"Duplicated {count} statue slots");
        }

        public void GenerateNormalMaterials(StatueType statueType)
        {
            Log.Info("Converting original materials to transparent versions");

            // Move all statue materials to slot temporarily
            foreach (var map in MeshRenderers)
            {
                for(var i = 0; i < map.NormalMaterials.Count; i++)
                {
                    var material = map.NormalMaterials[i];

                    if (material.Slot.Parent == _normalMaterials)
                    {
                        Log.Debug($"Moving material {material.ReferenceID} from {material.Slot.Name} to {_normalMaterials.Name}");
                        map.NormalMaterials[i] = (AssetProvider<Material>)_normalMaterials.MoveComponent((AssetProvider<Material>)material);
                    }
                }
            }

            // Destroy all existing children
            _normalMaterials.DestroyChildren();

            var oldMaterialToNewNormalMaterialMap = new Dictionary<RefID, IAssetProvider<Material>>();
            // Create alpha material and swap normal material for it
            MeshRenderers.ForEach((map) =>
            {
                for (int i = 0; i < map.NormalMaterials.Count; ++i)
                {
                    var oldMaterial = map.NormalMaterials[i];

                    if (!oldMaterialToNewNormalMaterialMap.ContainsKey(oldMaterial.ReferenceID))
                    {
                        Log.Info($"Creating material for {oldMaterial.ReferenceID}");
                        var newSlot = _normalMaterials.AddSlot($"Normal {oldMaterialToNewNormalMaterialMap.Count}");
                        var newMaterial = MaterialHelpers.CreateAlphaMaterial(oldMaterial, statueType, newSlot);
                        oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID] = newMaterial;
                    }
                    else
                    {
                        Log.Info($"Material {i} was already created as {oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID].ReferenceID}");
                    }

                    map.NormalMeshRenderer.Materials[i] = oldMaterialToNewNormalMaterialMap[oldMaterial.ReferenceID];
                }
            });

            _normalMaterials.RemoveAllComponents(_ => true);
        }

        public void GenerateStatueMaterials(bool defaultMaterialAsIs)
        {
            // Move all statue materials to slot temporarily
            foreach (var map in MeshRenderers)
            {
                for (var i = 0; i < map.StatueMaterials.Count; i++)
                {
                    var material = map.StatueMaterials[i];

                    if (material.Slot.Parent == _normalMaterials)
                    {
                        Log.Debug($"Moving material {material.ReferenceID} from {material.Slot.Name} to {_normalMaterials.Name}");
                        map.StatueMaterials[i] = (AssetProvider<Material>)_normalMaterials.MoveComponent((AssetProvider<Material>)material);
                    }
                }
            }
            if (_blinderMaterial.Slot.Parent == _statueMaterials)
            {
                Log.Debug($"Moving material {_blinderMaterial.ReferenceID} from {_blinderMaterial.Slot.Name} to {_statueMaterials.Name}");
                _statueMaterials.MoveComponent((AssetProvider<Material>)_blinderMaterial);
            }

            // Destroy all existing children
            _statueMaterials.DestroyChildren();

            // Creating blinder material
            {
                var blinderMaterialHolder = _statueMaterials.AddSlot("Statue 0");
                var blinderDefaultMaterial = blinderMaterialHolder.CopyComponent((AssetProvider<Material>)_blinderMaterial);

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

            Log.Info("Created blinder material");

            // Create Material objects for each statue material
            var oldMaterialToStatueMaterialMap = new Dictionary<RefID, ReferenceMultiDriver<IAssetProvider<Material>>>();
            MeshRenderers.ForEach((map) =>
            {
                for (int i = 0; i < map.NormalMaterials.Count; ++i)
                {
                    var normalMaterial = map.NormalMaterials[i];
                    var statueMaterial = map.StatueMaterials[i];

                    if (!oldMaterialToStatueMaterialMap.ContainsKey(normalMaterial.ReferenceID))
                    {
                        Log.Info($"Creating material {oldMaterialToStatueMaterialMap.Count + 1} as duplicate of {normalMaterial.ReferenceID}");
                        // If assigned == null, use default

                        // Create a new statue material object (i.e. drives material slot on statue
                        // SMR, has default material with normal map)
                        var newMaterialHolder = _statueMaterials.AddSlot($"Statue {oldMaterialToStatueMaterialMap.Count + 1}");
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

                        // boolean ref driver drives this, which drives everything else
                        var multiDriver = newMaterialHolder.AttachComponent<ReferenceMultiDriver<IAssetProvider<Material>>>();
                        booleanReferenceDriver.TargetReference.ForceLink(multiDriver.Reference);

                        // Makes material accessible elsewhere
                        var dynMaterialVariable = newMaterialHolder.AttachComponent<DynamicReferenceVariable<IAssetProvider<Material>>>();
                        dynMaterialVariable.VariableName.Value = $"Avatar/Statue.Material{oldMaterialToStatueMaterialMap.Count + 1}";

                        // Drive that dynvar
                        multiDriver.Drives.Add();
                        multiDriver.Drives[0].ForceLink(dynMaterialVariable.Reference);

                        oldMaterialToStatueMaterialMap.Add(normalMaterial.ReferenceID, multiDriver);
                    }
                    else
                    {
                        Log.Info($"Material {i} was already created as {oldMaterialToStatueMaterialMap[normalMaterial.ReferenceID].ReferenceID}");
                    }

                    var drives = oldMaterialToStatueMaterialMap[normalMaterial.ReferenceID].Drives;
                    drives.Add().ForceLink(map.StatueMeshRenderer.Materials.GetElement(i));
                    // Thanks Dann :)
                }
            });

            _statueMaterials.RemoveAllComponents(_ => true);
        }

        public void InstallRemasterSystem(Slot systemSlot)
        {
            Log.Info("Duplicating slots onto avatar");

            // Remove the old system
            StatueRoot.DestroyChildren(filter: x => x.Tag == "CopyToStatue");

            // Install the new system
            systemSlot.GetChildrenWithTag("CopyToStatue").ForEach((childSlot) =>
            {
                Log.Info($"Adding {childSlot.Name} with tag {childSlot.Tag}");
                childSlot.Duplicate(StatueRoot);
            });
        }

        public void ReadAvatarRoot(Slot newAvatarRoot, IAssetProvider<Material> defaultMaterial, bool skinnedMeshRenderersOnly)
        {
            MeshRenderers.Clear();
            AvatarRoot = newAvatarRoot;
            StatueRoot = null;
            HasExistingSystem = false;
            HasLegacySystem = false;
            _skinnedMeshRenderersOnly = skinnedMeshRenderersOnly;

            if (AvatarRoot == null)
                return;

            try
            {
                Log.Info($"Using avatar {AvatarRoot.Name}/{AvatarRoot.ReferenceID}");

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
                    if(!slot.Value.Name.Contains("Statue"))
                    {
                        var existingStatueSlot = uniqueSlots
                            .Where(x => x.Value.Name.Contains(slot.Value.Name) && x.Value.Name.Contains("Statue"))
                            .Select(x => x.Value)
                            .FirstOrDefault();

                        _slotsMap.Add(slot.Value, existingStatueSlot);

                        Log.Debug($"Mapping {slot.Value.Name} to {existingStatueSlot?.Name ?? "null"}");
                    }
                }

                Log.Info($"Found {_slotsMap.Count} unique slots");

                foreach(var map in _slotsMap)
                {
                    var normalRenderers = (skinnedMeshRenderersOnly
                        ? map.Key.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>()
                        : map.Key.GetComponentsInChildren<MeshRenderer>()
                        ).ToList();
                    var statueRenderers = (skinnedMeshRenderersOnly
                        ? map.Value?.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<MeshRenderer>() ?? Enumerable.Empty<MeshRenderer>()
                        : map.Value?.GetComponentsInChildren<MeshRenderer>() ?? Enumerable.Empty<MeshRenderer>()
                        ).ToList();

                    foreach(var normal in normalRenderers)
                    {
                        var statue = statueRenderers.Find(x => x.Mesh.Value == normal.Mesh.Value);

                        var rendererMap = new MeshRendererMap
                        {
                            NormalMeshRenderer = normal,
                            StatueMeshRenderer = statue,
                            NormalMaterials = normal.Materials.ToList()
                        };

                        Log.Debug($"MeshRenderer {normal.ReferenceID} linked to {statue?.ReferenceID ?? RefID.Null}");

                        foreach(var material in rendererMap.NormalMaterials)
                        {
                            rendererMap.StatueMaterials.Add(material);
                            Log.Debug($"Material {material.ReferenceID} linked to {defaultMaterial?.ReferenceID ?? RefID.Null}");
                        }

                        MeshRenderers.Add(rendererMap);
                    }
                }

                StatueRoot = FindSlot(children, slot => slot.FindChild("Drivers") != null && slot.FindChild("Generated Materials") != null, "Statue", "StatueSystemSetupSlot");
                _generatedMaterials = FindSlot(children, slot => slot.FindChild("Statue Materials") != null && slot.FindChild("Normal Materials") != null, "Generated Materials");
                _drivers = FindSlot(children, slot => slot.FindChild("Avatar/Statue.BodyNormal") != null, "Drivers");

                var legacySystem = AvatarRoot.FindChildInHierarchy("<color=#dadada>Statuefication</color>");
                var legacyAddons = AvatarRoot.FindChildInHierarchy("<color=#dadada>Statue Add-Ons</color>");

                Log.Debug($"Statue root is {StatueRoot?.ReferenceID ?? RefID.Null}");
                Log.Debug($"Generated materials is {_generatedMaterials?.ReferenceID ?? RefID.Null}");
                Log.Debug($"Drivers is {_drivers?.ReferenceID ?? RefID.Null}");
                Log.Debug($"Legacy system is {legacySystem?.ReferenceID ?? RefID.Null}");
                Log.Debug($"Legacy addons is {legacyAddons?.ReferenceID ?? RefID.Null}");

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
                    var statue0Material = _generatedMaterials?
                        .FindChild("Statue Materials")?
                        .FindChild("Statue 0")?
                        .GetComponent<AssetProvider<Material>>();

                    if (statue0Material != null)
                    {
                        Log.Info("Using existing avatar default statue material");

                        foreach(var map in MeshRenderers)
                        {
                            for(var i = 0; i < map.StatueMaterials.Count; i++)
                            {
                                map.StatueMaterials[i] = statue0Material;
                                Log.Debug($"Material {map.NormalMaterials[i].ReferenceID} linked to {statue0Material.ReferenceID}");
                            }
                        }

                        _blinderMaterial = statue0Material;
                        Log.Debug($"Using material {statue0Material.ReferenceID} as blinder material");
                    }
                    else
                    {
                        Log.Debug("Unable to find statue 0 material");
                    }
                }
                else
                {
                    _blinderMaterial = defaultMaterial;
                    Log.Debug($"Using material {defaultMaterial.ReferenceID} as blinder material");

                    Log.Info("Using user supplied default statue material");
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex.Message.Replace("\r\n", "<br>"));
            }
        }

        private static Slot FindSlot(List<Slot> slots, Predicate<Slot> predicate, string name = null, string tag = null)
        {
            foreach (var slot in slots)
            {
                if (((name == null && tag == null) || (tag != null && slot.Tag == tag) || (name != null && slot.Name == name)) && predicate(slot))
                    return slot;
            }

            return null;
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
            // Find unique slots to duplicate
            foreach (var map in MeshRenderers)
            {
                var smrsInChildren = map.NormalMeshRenderer.Slot.GetComponentsInChildren<MeshRenderer>();
                if (smrsInChildren.Exists(x => x != map.NormalMeshRenderer))
                {
                    Log.Error($"Slot {map.NormalMeshRenderer.Slot.Name} has nested MeshRenderers, aborting");
                    return false;
                }

                foreach(var material in map.StatueMaterials)
                {
                    if(material == null || material.ReferenceID == RefID.Null)
                    {
                        Log.Error("Missing default statue material for some material slots, aborting");
                        return false;
                    }
                }
            }

            if (_blinderMaterial == null || _blinderMaterial.ReferenceID == RefID.Null)
            {
                Log.Error("No default statue material found for the install, aborting");
                return false;
            }

            return true;
        }

        #endregion

        #region Private Fields

        private Slot _blendshapes;
        private IAssetProvider<Material> _blinderMaterial;
        private Slot _defaults;
        private Slot _drivers;
        private Slot _meshes;
        private Slot _normalMaterials;
        private Dictionary<Slot, Slot> _slotsMap = new Dictionary<Slot, Slot>();
        private Slot _statueMaterials;

        #endregion
    }
}
