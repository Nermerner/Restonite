using Elements.Core;
using FrooxEngine;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Restonite;

internal partial class Avatar
{
    #region Public Methods

    public void CollectMaterials()
    {
        if (_scratchSpace is null || _originalNormalMaterials is null || _originalStatueMaterials is null)
            return;

        Log.Info("=== Collecting avatar materials");

        var normalMaterials = _scratchSpace.AddSlot("Normal Materials");
        var statueMaterials = _scratchSpace.AddSlot("Statue Materials");

        // Move all materials to scratch space slot temporarily
        foreach (var material in MeshRenderers.SelectMany(x => x.MaterialSets).SelectMany(x => x))
        {
            if (material.Normal is not null && material.Normal.Slot != normalMaterials)
            {
                Log.Debug($"Copying {material.Normal.ToLongString()} to {normalMaterials.ToShortString()}");
                var newMaterial = MaterialHelpers.CopyMaterialToSlot(material.Normal, normalMaterials);

                ChangeMaterialReferences(material.Normal, newMaterial);
            }

            if (material.Statue is not null && material.Statue.Slot != statueMaterials)
            {
                Log.Debug($"Copying {material.Statue.ToLongString()} to {statueMaterials.ToShortString()}");
                var newMaterial = MaterialHelpers.CopyMaterialToSlot(material.Statue, statueMaterials);

                ChangeMaterialReferences(material.Statue, newMaterial);
            }
        }

        _originalNormalMaterials.DestroyChildren();
        _originalStatueMaterials.DestroyChildren();

        // Generate original material lists
        var normalList = new List<RefID>();
        var statueList = new List<RefID>();

        foreach (var meshRendererMap in MeshRenderers)
        {
            for (int materialSet = 0; materialSet < meshRendererMap.MaterialSets.Count; materialSet++)
            {
                for (int materialIndex = 0; materialIndex < meshRendererMap.MaterialSets[materialSet].Count; materialIndex++)
                {
                    var material = meshRendererMap.MaterialSets[materialSet][materialIndex];

                    if (material.Normal is not null && !normalList.Contains(material.Normal.ReferenceID))
                    {
                        var slot = _originalNormalMaterials.AddSlot($"{normalList.Count}: {meshRendererMap.NormalSlot!.Name}.Set{materialSet}.Material{materialIndex}");
                        var newMaterial = MaterialHelpers.CopyMaterialToSlot(material.Normal, slot);
                        ChangeMaterialReferences(material.Normal, newMaterial);
                        normalList.Add(material.Normal.ReferenceID);
                    }

                    if (material.Statue is not null && !statueList.Contains(material.Statue.ReferenceID))
                    {
                        var slot = _originalStatueMaterials.AddSlot(meshRendererMap.StatueSlot is null
                            ? $"{statueList.Count}: Default"
                            : $"{statueList.Count}: {meshRendererMap.StatueSlot.Name}.Set{materialSet}.Material{materialIndex}");
                        var newMaterial = MaterialHelpers.CopyMaterialToSlot(material.Statue, slot);
                        ChangeMaterialReferences(material.Statue, newMaterial);
                        statueList.Add(material.Statue.ReferenceID);
                    }
                }
            }
        }
    }

    public void GenerateNormalMaterials()
    {
        if (_normalMaterials is null)
            return;

        Log.Info("=== Generating normal materials");

        // Destroy all existing children
        _normalMaterials.DestroyChildren();

        // Create alpha material and swap normal material for it
        var oldMaterialToNewNormalMaterialMap = new Dictionary<string, ReferenceMultiDriver<IAssetProvider<Material>>>();
        for (int i = 0; i < MeshRenderers.Count; i++)
        {
            MeshRendererMap map = MeshRenderers[i];

            if (map.NormalMeshRenderer is null)
                continue;

            for (int set = 0; set < map.MaterialSets.Count; set++)
            {
                for (int slot = 0; slot < map.MaterialSets[set].Count; ++slot)
                {
                    if (map.MaterialSets[set][slot].Normal is null)
                        continue;

                    var name = $"{map.NormalMeshRenderer.ToLongString()}, material set {set}, slot {slot}";

                    var oldMaterial = map.MaterialSets[set][slot].Normal;
                    var statueType = map.MaterialSets[set][slot].TransitionType;
                    var key = $"{oldMaterial!.ReferenceID}_{statueType}";

                    if (!oldMaterialToNewNormalMaterialMap.ContainsKey(key))
                    {
                        Log.Info($"Creating normal material {oldMaterialToNewNormalMaterialMap.Count} for {oldMaterial.ToLongString()} using {statueType}");

                        var newSlot = _normalMaterials.AddSlot($"{oldMaterialToNewNormalMaterialMap.Count}: {map.NormalSlot!.Name}.Set{set}.Material{slot}");

                        // Create material based on transition type
                        var newMaterial = MaterialHelpers.CreateAlphaMaterial(oldMaterial, statueType, newSlot);

                        // Add dynvar with information about what transition type was used
                        var typeDynVar = newSlot.AttachComponent<DynamicValueVariable<string>>();
                        typeDynVar.VariableName.Value = $"Avatar/Statue.TransitionType{oldMaterialToNewNormalMaterialMap.Count}";
                        typeDynVar.Value.Value = $"{statueType}";

                        // Add dynvar with information about the original material
                        var originalDynVar = newSlot.AttachComponent<DynamicReferenceVariable<Slot>>();
                        originalDynVar.VariableName.Value = $"Avatar/Statue.OriginalNormalMaterial{oldMaterialToNewNormalMaterialMap.Count}";
                        originalDynVar.Reference.Target = oldMaterial.Slot;

                        // boolean ref driver drives this, which drives everything else
                        var multiDriver = newSlot.AttachComponent<ReferenceMultiDriver<IAssetProvider<Material>>>();

                        if (map.MaterialSets[set][slot].Clothes)
                        {
                            var dynVarDriver = newSlot.AttachComponent<DynamicValueVariableDriver<int>>();
                            dynVarDriver.VariableName.Value = "Avatar/Statue.Clothing.TransitionType";

                            var valueField = newSlot.AttachComponent<ValueField<int>>();
                            dynVarDriver.Target.ForceLink(valueField.Value);

                            var valueEqualityDriver = newSlot.AttachComponent<ValueEqualityDriver<int>>();
                            valueEqualityDriver.Reference.Value = 0;
                            valueEqualityDriver.Invert.Value = true;
                            valueEqualityDriver.TargetValue.Target = valueField.Value;

                            var booleanReferenceDriver = newSlot.AttachComponent<BooleanReferenceDriver<IAssetProvider<Material>>>();
                            booleanReferenceDriver.FalseTarget.Value = oldMaterial.ReferenceID;
                            booleanReferenceDriver.TrueTarget.Value = newMaterial.ReferenceID;
                            valueEqualityDriver.Target.ForceLink(booleanReferenceDriver.State);
                            booleanReferenceDriver.TargetReference.ForceLink(multiDriver.Reference);
                        }
                        else
                        {
                            multiDriver.Reference.Target = newMaterial;
                        }

                        oldMaterialToNewNormalMaterialMap.Add(key, multiDriver);
                    }

                    var drives = oldMaterialToNewNormalMaterialMap[key].Drives;

                    if (map.NormalMaterialSet is not null)
                    {
                        drives.Add().ForceLink(map.NormalMaterialSet.Sets[set].GetElement(slot));
                    }
                    else
                    {
                        var materialSlot = map.NormalMeshRenderer.Materials.GetElement(slot);
                        var element = materialSlot.ActiveLink as SyncElement;
                        if (element is not null && materialSlot.IsDriven && materialSlot.IsLinked)
                            Log.Warn($"{name} appears to already be driven by {element.Component.ToLongString()}, attempting to set anyway");

                        drives.Add().ForceLink(materialSlot);
                    }
                }
            }
        }
    }

    public void GenerateStatueMaterials()
    {
        if (_statueMaterials is null)
            return;

        Log.Info("=== Generating statue materials");

        // Destroy all existing children
        _statueMaterials.DestroyChildren();
        IAssetProvider<Material>? transparentMaterial = null;
        if (MeshRenderers.SelectMany(x => x.MaterialSets.SelectMany(y => y)).Any(x => x.Clothes))
        {
            var slot = _statueMaterials.AddSlot("Transparent");
            var mat = slot.AttachComponent<PBS_Metallic>();
            mat.AlbedoColor.Value = new colorX(r: 0.0f, g: 0.0f, b: 0.0f, a: 0.0f);
            mat.BlendMode.Value = BlendMode.Alpha;
            transparentMaterial = mat;
        }

        // Create Material objects for each statue material
        var oldMaterialToStatueMaterialMap = new Dictionary<string, ReferenceMultiDriver<IAssetProvider<Material>>>();
        for (int i = 0; i < MeshRenderers.Count; i++)
        {
            MeshRendererMap map = MeshRenderers[i];
            var isBlinder = map.NormalMeshRenderer is null && map.StatueMeshRenderer is null;

            for (int set = 0; set < map.MaterialSets.Count; ++set)
            {
                for (int slot = 0; slot < map.MaterialSets[set].Count; ++slot)
                {
                    var name = map.StatueMeshRenderer is null ? "Blinder" : $"{map.StatueMeshRenderer.ToLongString()}, material set {set}, slot {slot}";

                    var normalMaterial = map.MaterialSets[set][slot].Normal;
                    var statueMaterial = map.MaterialSets[set][slot].Statue;
                    var defaultMaterialAsIs = isBlinder || map.MaterialSets[set][slot].UseAsIs;

                    if (statueMaterial is null)
                        continue;

                    if (!isBlinder && normalMaterial is null && statueMaterial is not null)
                    {
                        Log.Warn($"{map.NormalMeshRenderer.ToLongString()}, material {slot} is null, skipping statue material");
                        continue;
                    }

                    var key = defaultMaterialAsIs && !map.MaterialSets[set][slot].Clothes
                        ? $"{statueMaterial!.ReferenceID}"
                        : $"{normalMaterial!.ReferenceID}_{map.MaterialSets[set][slot].Clothes}";

                    if (!oldMaterialToStatueMaterialMap.ContainsKey(key))
                    {
                        Log.Info($"Creating statue material {oldMaterialToStatueMaterialMap.Count} as duplicate of {key}");
                        Log.Debug(defaultMaterialAsIs ? "Using material as-is" : "Merging with normal material maps");

                        // If assigned is null, use default

                        // Create a new statue material object (i.e. drives material slot on statue
                        // SMR, has default material with normal map)
                        var newMaterialHolder = _statueMaterials.AddSlot(map.StatueSlot is null
                            ? $"{oldMaterialToStatueMaterialMap.Count}: Default"
                            : $"{oldMaterialToStatueMaterialMap.Count}: {map.StatueSlot.Name}.Set{set}.Material{slot}");

                        var newDefaultMaterialRefId = defaultMaterialAsIs
                            ? newMaterialHolder.CopyComponent((AssetProvider<Material>)statueMaterial!).ReferenceID
                            : MaterialHelpers.CreateStatueMaterial(normalMaterial!, statueMaterial!, newMaterialHolder).ReferenceID;

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

                        if (map.MaterialSets[set][slot].Clothes)
                        {
                            var dynVarDriver = newMaterialHolder.AttachComponent<DynamicValueVariableDriver<int>>();
                            dynVarDriver.VariableName.Value = "Avatar/Statue.Clothing.TransitionType";

                            var multiplexer = newMaterialHolder.AttachComponent<ReferenceMultiplexer<IAssetProvider<Material>>>();
                            dynVarDriver.Target.ForceLink(multiplexer.Index);
                            multiplexer.References.Add().Target = normalMaterial!;
                            booleanReferenceDriver.TargetReference.ForceLink(multiplexer.References.Add());
                            multiplexer.References.Add().Target = transparentMaterial!;

                            multiplexer.Target.ForceLink(multiDriver.Reference);
                        }
                        else
                        {
                            booleanReferenceDriver.TargetReference.ForceLink(multiDriver.Reference);
                        }

                        // Drive that dynvar
                        multiDriver.Drives.Add();
                        multiDriver.Drives[0].ForceLink(dynMaterialVariable.Reference);

                        // Add dynvar with information about the original material
                        var originalDynVar = newMaterialHolder.AttachComponent<DynamicReferenceVariable<Slot>>();
                        originalDynVar.VariableName.Value = $"Avatar/Statue.OriginalStatueMaterial{oldMaterialToStatueMaterialMap.Count}";
                        originalDynVar.Reference.Target = statueMaterial!.Slot;

                        if (!defaultMaterialAsIs)
                        {
                            // Add dynvar with information about the original material it was based on
                            var basedOnDynVar = newMaterialHolder.AttachComponent<DynamicReferenceVariable<Slot>>();
                            basedOnDynVar.VariableName.Value = $"Avatar/Statue.BasedOnNormalMaterial{oldMaterialToStatueMaterialMap.Count}";
                            basedOnDynVar.Reference.Target = normalMaterial!.Slot;
                        }

                        oldMaterialToStatueMaterialMap.Add(key, multiDriver);
                    }

                    if (map.StatueMeshRenderer is not null && slot < map.StatueMeshRenderer.Materials.Count)
                    {
                        var drives = oldMaterialToStatueMaterialMap[key].Drives;

                        if (map.StatueMaterialSet is not null)
                        {
                            drives.Add().ForceLink(map.StatueMaterialSet.Sets[set].GetElement(slot));
                        }
                        else
                        {
                            var materialSlot = map.StatueMeshRenderer.Materials.GetElement(slot);
                            var element = materialSlot.ActiveLink as SyncElement;
                            if (element is not null && materialSlot.IsDriven && materialSlot.IsLinked)
                                Log.Warn($"{name} appears to already be driven by {element.Component.ToLongString()}, attempting to set anyway");

                            drives.Add().ForceLink(materialSlot);
                        }
                    }

                    // Thanks Dann :)
                }
            }
        }
    }

    #endregion

    #region Private Methods

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

    private IAssetProvider<Material>? GetDefaultMaterial(IAssetProvider<Material>? defaultMaterial)
    {
        var statue0Material = (IAssetProvider<Material>?)_generatedMaterials?
            .FindChild("Statue Materials")?
            .FindChild("Statue 0")?
            .GetComponent<AssetProvider<Material>>();
        statue0Material ??= (IAssetProvider<Material>?)_generatedMaterials?
            .FindChild("Statue Materials")?
            .FindChild("0: Default")?
            .GetComponent<AssetProvider<Material>>();

        if ((defaultMaterial is null || defaultMaterial.ReferenceID == RefID.Null) && statue0Material is not null)
        {
            defaultMaterial = statue0Material;
            Log.Debug($"Using existing default statue material, {statue0Material.ToShortString()}");
        }
        else if (defaultMaterial is not null && defaultMaterial.ReferenceID != RefID.Null)
        {
            Log.Info($"Using user supplied default statue material, {defaultMaterial.ToShortString()}");
        }
        else
        {
            Log.Warn("Couldn't find a material to use for default statue material");
        }

        return defaultMaterial;
    }

    private bool HasMaterialSet(MeshRenderer? renderer, [NotNullWhen(true)] out MaterialSet? materialSet)
    {
        if (renderer?.Materials.IsDriven == true && renderer.Materials.IsLinked)
        {
            var element = renderer.Materials.ActiveLink as SyncElement;
            if (element?.Component is MaterialSet set)
            {
                materialSet = set;
                return true;
            }
        }

        materialSet = null;
        return false;
    }

    #endregion
}
