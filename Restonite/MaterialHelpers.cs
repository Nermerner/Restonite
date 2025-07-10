using FrooxEngine;

namespace Restonite;

internal static partial class MaterialHelpers
{
    public static IAssetProvider<Material> CreateStatueMaterial(IAssetProvider<Material> originalMaterial, IAssetProvider<Material> statueMaterial, Slot destination)
    {
        var newMaterial = destination.CopyComponent((AssetProvider<Material>)statueMaterial);

        ICommonMaterial? commonMaterial = originalMaterial as ICommonMaterial;
        ICommonMaterial? commonMaterial2 = (IAssetProvider<Material>)newMaterial as ICommonMaterial;
        if (commonMaterial is not null && commonMaterial2 is not null)
        {
            commonMaterial2.NormalScale = commonMaterial.NormalScale;
            commonMaterial2.NormalTextureScale = commonMaterial.NormalTextureScale;
            commonMaterial2.NormalTextureOffset = commonMaterial.NormalTextureOffset;
            commonMaterial2.NormalMap = commonMaterial.NormalMap;
        }

        IPBS_Material? iPBS_Material = originalMaterial as IPBS_Material;
        IPBS_Material? iPBS_Material2 = (IAssetProvider<Material>)newMaterial as IPBS_Material;
        if (iPBS_Material2 is not null && commonMaterial is not null)
        {
            iPBS_Material2.TextureOffset = commonMaterial.TextureOffset;
            iPBS_Material2.TextureScale = commonMaterial.TextureScale;
            iPBS_Material2.NormalMap = commonMaterial.NormalMap;
        }

        if (commonMaterial2 is not null && iPBS_Material is not null)
        {
            commonMaterial2.TextureOffset = iPBS_Material.TextureOffset;
            commonMaterial2.TextureScale = iPBS_Material.TextureScale;
            commonMaterial2.NormalMap = iPBS_Material.NormalMap;
        }

        if (iPBS_Material is not null && iPBS_Material2 is not null)
        {
            iPBS_Material2.TextureScale = iPBS_Material.TextureScale;
            iPBS_Material2.TextureOffset = iPBS_Material.TextureOffset;
            iPBS_Material2.NormalScale = iPBS_Material.NormalScale;
            iPBS_Material2.NormalMap = iPBS_Material.NormalMap;
            iPBS_Material2.OcclusionMap = iPBS_Material.OcclusionMap;
        }

        return (IAssetProvider<Material>)newMaterial;
    }

    public static IAssetProvider<Material> CopyMaterialToSlot(IAssetProvider<Material> originalMaterial, Slot destination)
    {
        switch (originalMaterial)
        {
            case PBS_DistanceLerpMetallic dlm:
                {
                    var newMaterial = destination.AttachComponent<PBS_Metallic>();
                    FrooxEngine.MaterialHelper.CopyMaterialProperties(dlm, newMaterial);
                    newMaterial.Persistent = true;
                    return newMaterial;
                }
            case PBS_DistanceLerpSpecular dls:
                {
                    var newMaterial = destination.AttachComponent<PBS_Specular>();
                    FrooxEngine.MaterialHelper.CopyMaterialProperties(dls, newMaterial);
                    newMaterial.Persistent = true;
                    return newMaterial;
                }
            case PBS_SliceMetallic sm:
                {
                    var newMaterial = destination.AttachComponent<PBS_Metallic>();
                    FrooxEngine.MaterialHelper.CopyMaterialProperties(sm, newMaterial);
                    newMaterial.Persistent = true;
                    return newMaterial;
                }
            case PBS_SliceSpecular ss:
                {
                    var newMaterial = destination.AttachComponent<PBS_Specular>();
                    FrooxEngine.MaterialHelper.CopyMaterialProperties(ss, newMaterial);
                    newMaterial.Persistent = true;
                    return newMaterial;
                }
            default:
                {
                    var newMaterial = destination.CopyComponent((AssetProvider<Material>)originalMaterial);
                    newMaterial.Persistent = true;
                    return (IAssetProvider<Material>)newMaterial;
                }
        }
    }

    public static IAssetProvider<Material> CreateAlphaMaterial(IAssetProvider<Material> originalMaterial, StatueType statueType, Slot destination)
    {
        switch (statueType)
        {
            case StatueType.AlphaFade:
                switch (originalMaterial)
                {
                    case PBS_DualSidedMetallic dsm:
                        {
                            Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                            var newMaterial = destination.AttachComponent<PBS_DualSidedMetallic>();
                            MaterialHelpers.SetupAlphaFadeDualsidedMaterial(dsm, newMaterial, destination);
                            newMaterial.Persistent = true;
                            return newMaterial;
                        }
                    case PBS_DualSidedSpecular dss:
                        {
                            Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                            var newMaterial = destination.AttachComponent<PBS_DualSidedSpecular>();
                            MaterialHelpers.SetupAlphaFadeDualsidedMaterial(dss, newMaterial, destination);
                            newMaterial.Persistent = true;
                            return newMaterial;
                        }
                    case IPBS_Metallic m:
                        {
                            Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                            var newMaterial = destination.AttachComponent<PBS_Metallic>();
                            MaterialHelpers.SetupAlphaFadePBSMaterial(m, newMaterial, destination);
                            newMaterial.Persistent = true;
                            return newMaterial;
                        }
                    case IPBS_Specular s:
                        {
                            Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                            var newMaterial = destination.AttachComponent<PBS_Specular>();
                            MaterialHelpers.SetupAlphaFadePBSMaterial(s, newMaterial, destination);
                            newMaterial.Persistent = true;
                            return newMaterial;
                        }
                    case XiexeToonMaterial x:
                        {
                            Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                            var newMaterial = destination.AttachComponent<XiexeToonMaterial>();
                            MaterialHelpers.SetupAlphaFadeXiexeMaterial(x, newMaterial, destination);
                            newMaterial.Persistent = true;
                            return newMaterial;
                        }
                    case UnlitMaterial u:
                        {
                            Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                            var newMaterial = destination.AttachComponent<UnlitMaterial>();
                            MaterialHelpers.SetupAlphaFadeUnlitMaterial(u, newMaterial, destination);
                            newMaterial.Persistent = true;
                            return newMaterial;
                        }
                }

                break;

            case StatueType.AlphaCutout:
                {
                    switch (originalMaterial)
                    {
                        case PBS_DualSidedMetallic dsm:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_DualSidedMetallic>();
                                MaterialHelpers.SetupAlphaCutoutDualsidedMaterial(dsm, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        case PBS_DualSidedSpecular dss:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_DualSidedSpecular>();
                                MaterialHelpers.SetupAlphaCutoutDualsidedMaterial(dss, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        case IPBS_Metallic m:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_Metallic>();
                                MaterialHelpers.SetupAlphaCutoutPBSMaterial(m, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        case IPBS_Specular s:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_Specular>();
                                MaterialHelpers.SetupAlphaCutoutPBSMaterial(s, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        case XiexeToonMaterial x:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<XiexeToonMaterial>();
                                MaterialHelpers.SetupAlphaCutoutXiexeMaterial(x, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        case UnlitMaterial u:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<UnlitMaterial>();
                                MaterialHelpers.SetupAlphaCutoutUnlitMaterial(u, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                    }

                    break;
                }
            case StatueType.PlaneSlicer:
                {
                    switch (originalMaterial)
                    {
                        case IPBS_Metallic m:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_SliceMetallic>();
                                SetupSlicerPlanePBSMaterial(m, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        case IPBS_Specular s:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_SliceSpecular>();
                                SetupSlicerPlanePBSMaterial(s, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        default:
                            Log.Error($"Material type {originalMaterial.GetType().Name} not supported as {statueType}");
                            destination.Name = "Material type was not supported for PlaneSlicer";
                            break;
                    }
                    break;
                }

            case StatueType.RadialSlicer:
                {
                    switch (originalMaterial)
                    {
                        case IPBS_Metallic m:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_DistanceLerpMetallic>();
                                SetupRadialDisplacePBSMaterial(m, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        case IPBS_Specular s:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_DistanceLerpSpecular>();
                                SetupRadialDisplacePBSMaterial(s, newMaterial, destination);
                                newMaterial.Persistent = true;
                                return newMaterial;
                            }
                        default:
                            Log.Error($"Material type {originalMaterial.GetType().Name} not supported as {statueType}");
                            destination.Name = "Material type was not supported for RadialSlicer";
                            break;
                    }
                    break;
                }
        }

        destination.NameField.Value = "Failed To Create Material";
        return originalMaterial;
    }
}