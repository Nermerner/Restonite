using Elements.Core;
using FrooxEngine;
using System.Collections.Generic;
using System.Linq;

namespace Restonite;

internal partial class Avatar
{
    #region Public Properties

    public List<MeshRendererMap> MeshRenderers { get; } = new List<MeshRendererMap>();

    #endregion

    #region Public Methods

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
                    var foundMap = MeshRenderers.Find(toSearch => toSearch.NormalSlot == map.NormalSlot && (toSearch.NormalMeshRenderer?.Mesh.Value == renderer.Mesh.Value || toSearch.NormalMeshRenderer?.Mesh.Value.GetType() == renderer.Mesh.Value.GetType()));
                    if (foundMap != null)
                    {
                        if (renderer.Mesh.Value != foundMap.NormalMeshRenderer.Mesh.Value)
                        {
                            foundMap.StatueSlot.RemoveComponent(renderer.Mesh.Value);
                            renderer.Mesh.Value = foundMap.NormalMeshRenderer.Mesh.Value;
                        }

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

    public void RemoveUnmatchedMeshRenderers()
    {
        var toRemoveList = MeshRenderers.Where(x => x.NormalMeshRenderer != null && x.StatueMeshRenderer == null).ToList();
        foreach (var toRemove in toRemoveList)
            RemoveMeshRenderer(toRemove);
    }

    #endregion

    #region Private Methods

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

    private bool IsStatueMeshRenderer(MeshRenderer renderer)
    {
        // Check name first
        var names = new string[] { "statue", "petrified", "stone" };
        foreach (var name in names)
        {
            if (renderer.Slot.Name.ToLower().Contains(name) || renderer.Slot.Parent.Name.ToLower().Contains(name))
                return true;
        }

        var fields = new List<Sync<bool>>
            {
                renderer.EnabledField
            };

        var slot = renderer.Slot;
        while (slot != AvatarRoot)
        {
            fields.Add(slot.ActiveSelf_Field);
            slot = slot.Parent;
        }

        return fields.Exists(x => IsDrivenByKnownStatueDriver(x));
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
