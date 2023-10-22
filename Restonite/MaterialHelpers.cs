using Elements.Core;
using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Restonite
{
    internal class MaterialHelpers
    {
        static public IAssetProvider<Material> CreateStatueMaterial(IAssetProvider<Material> originalMaterial, IAssetProvider<Material> statueMaterial, Slot destination)
        {
            var newMaterial = destination.CopyComponent((AssetProvider<Material>)statueMaterial);

            CopyStatueMaterialProperties(originalMaterial, (IAssetProvider<Material>)newMaterial);

            return (IAssetProvider<Material>)newMaterial;
        }

        #region MaterialSetup
        #region AlphaFadeSetup
        static void SetupAlphaFadePBSMaterial(IPBS_Material oldMaterial, PBS_Material newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            // Save original albedo
            var multiplierGradientDriver = destination.AttachComponent<ValueGradientDriver<colorX>>();
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.AlbedoColor.Value;
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.AlbedoColor.Value * new colorX(1.0f, 1.0f, 1.0f, 0.0f);
            multiplierGradientDriver.Points.Last().Position.Value = 1.0f;
            multiplierGradientDriver.Target.ForceLink(newMaterial.AlbedoColor);

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Progress";
            alphaDriver.Target.ForceLink(multiplierGradientDriver.Progress);

            // Drive blendmode of material
            var blendModeDriver = destination.AttachComponent<DynamicValueVariableDriver<BlendMode>>();
            blendModeDriver.VariableName.Value = "Avatar/Statue.BlendMode";
            var blendModeActiveDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            blendModeActiveDriver.VariableName.Value = "Avatar/Statue.BodyNormal.GreaterThan0";
            var blendModeBoolDriver = destination.AttachComponent<BooleanValueDriver<BlendMode>>();
            blendModeBoolDriver.TargetField.ForceLink(newMaterial.BlendMode);
            // Save original blend mode
            blendModeBoolDriver.FalseValue.Value = oldMaterial.BlendMode;
            blendModeDriver.Target.ForceLink(blendModeBoolDriver.TrueValue);
            blendModeActiveDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        static void SetupAlphaFadeDualsidedMaterial(PBS_DualSidedMaterial oldMaterial, PBS_DualSidedMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Progress";
            alphaDriver.Target.ForceLink(newMaterial.AlphaClip);

            newMaterial.AlphaHandling.Value = AlphaHandling.AlphaBlend;
            newMaterial.OffsetFactor.Value = -0.1f;
        }

        static void SetupAlphaFadeXiexeMaterial(XiexeToonMaterial oldMaterial, XiexeToonMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            // Save original albedo
            var multiplierGradientDriver = destination.AttachComponent<ValueGradientDriver<colorX>>();
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.Color.Value;
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.Color.Value * new colorX(1.0f, 1.0f, 1.0f, 0.0f);
            multiplierGradientDriver.Points.Last().Position.Value = 1.0f;
            multiplierGradientDriver.Target.ForceLink(newMaterial.Color);

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Progress";
            alphaDriver.Target.ForceLink(multiplierGradientDriver.Progress);

            // Drive blendmode of material
            var blendModeDriver = destination.AttachComponent<DynamicValueVariableDriver<BlendMode>>();
            blendModeDriver.VariableName.Value = "Avatar/Statue.BlendMode";
            var blendModeActiveDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            blendModeActiveDriver.VariableName.Value = "Avatar/Statue.BodyNormal.GreaterThan0";
            var blendModeBoolDriver = destination.AttachComponent<BooleanValueDriver<BlendMode>>();
            blendModeBoolDriver.TargetField.ForceLink(newMaterial.BlendMode);
            // Save original blend mode
            blendModeBoolDriver.FalseValue.Value = oldMaterial.BlendMode;
            blendModeDriver.Target.ForceLink(blendModeBoolDriver.TrueValue);
            blendModeActiveDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }
        #endregion AlphaFadeSetup
        #region AlphaCutoutSetup
        static void SetupAlphaCutoutPBSMaterial(IPBS_Material oldMaterial, PBS_Material newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);
            
            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Cutout";
            alphaDriver.Target.ForceLink(newMaterial.AlphaCutoff);
            // Drive blendmode of material
            var blendModeActiveDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            blendModeActiveDriver.VariableName.Value = "Avatar/Statue.BodyNormal.GreaterThan0";
            var blendModeBoolDriver = destination.AttachComponent<BooleanValueDriver<BlendMode>>();
            blendModeBoolDriver.TargetField.ForceLink(newMaterial.BlendMode);
            // Save original blend mode
            blendModeBoolDriver.FalseValue.Value = oldMaterial.BlendMode;
            blendModeBoolDriver.TrueValue.Value = BlendMode.Cutout;
            blendModeActiveDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        static void SetupAlphaCutoutDualsidedMaterial(PBS_DualSidedMaterial oldMaterial, PBS_DualSidedMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Cutout";
            alphaDriver.Target.ForceLink(newMaterial.AlphaClip);

            newMaterial.AlphaHandling.Value = AlphaHandling.AlphaClip;
            newMaterial.OffsetFactor.Value = -0.1f;
        }

        static void SetupAlphaCutoutXiexeMaterial(XiexeToonMaterial oldMaterial, XiexeToonMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Cutout";
            alphaDriver.Target.ForceLink(newMaterial.AlphaClip);
            // Drive blendmode of material
            var blendModeActiveDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            blendModeActiveDriver.VariableName.Value = "Avatar/Statue.BodyNormal.GreaterThan0";
            var blendModeBoolDriver = destination.AttachComponent<BooleanValueDriver<BlendMode>>();
            blendModeBoolDriver.TargetField.ForceLink(newMaterial.BlendMode);
            // Save original blend mode
            blendModeBoolDriver.FalseValue.Value = oldMaterial.BlendMode;
            blendModeBoolDriver.TrueValue.Value = BlendMode.Cutout;
            blendModeActiveDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }
        #endregion AlphaCutoutSetup
        #region SlicerPlaneSetup
        static void SetupSlicerPlanePBSMaterial(IPBS_Material oldMaterial, PBS_Slice newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);
            var plane = newMaterial.Slicers.Add();

            var posDriver = destination.AttachComponent<DynamicValueVariableDriver<float3>>();
            posDriver.VariableName.Value = "Avatar/Statue.Slicer.Position";
            posDriver.Target.ForceLink(plane.Position);

            var dirDriver = destination.AttachComponent<DynamicValueVariableDriver<float3>>();
            dirDriver.VariableName.Value = "Avatar/Statue.Slicer.Normal";
            dirDriver.Target.ForceLink(plane.Normal);

            // Future proofing :)
            var edgeColorDriver = destination.AttachComponent<DynamicValueVariableDriver<colorX>>();
            edgeColorDriver.VariableName.Value = "Avatar/Statue.Slicer.EdgeEmissiveColor";
            edgeColorDriver.Target.ForceLink(newMaterial.EdgeEmissiveColor);
            edgeColorDriver.DefaultValue.Value = new colorX(1.41f, 1.41f, 1.41f, 1.0f);

            var edgeTransitionEnd = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            edgeTransitionEnd.VariableName.Value = "Avatar/Statue.Slicer.EdgeTransitionEnd";
            edgeTransitionEnd.Target.ForceLink(newMaterial.EdgeTransitionEnd);
            edgeTransitionEnd.DefaultValue.Value = 0.02f;

            newMaterial.OffsetFactor.Value = -0.1f;
        }
        #endregion SlicerPlaneSetup
        #region RadialDisplace
        static void SetupRadialDisplacePBSMaterial(IPBS_Material oldMaterial, PBS_DistanceLerpMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);
            var sphere = newMaterial.Points.Add();

            var posDriver = destination.AttachComponent<DynamicValueVariableDriver<float3>>();
            posDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.GlobalPosition";
            posDriver.Target.ForceLink(sphere.Position);

            var edgeColorDriver = destination.AttachComponent<DynamicValueVariableDriver<colorX>>();
            edgeColorDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.Config.BorderColor";
            edgeColorDriver.Target.ForceLink(newMaterial.EmissionColorFrom);
            newMaterial.EmissionColorTo.Value = new colorX(0.0f);

            // This needs to be the case because of behavior if both are 0; if 0, defaults to To
            var displaceMagnitudeFromDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceMagnitudeFromDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.Config.MagnitudeFrom";
            displaceMagnitudeFromDriver.DefaultValue.Value = -0.05f;
            displaceMagnitudeFromDriver.Target.ForceLink(newMaterial.DisplaceMagnitudeFrom);

            var displaceMagnitudeToDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceMagnitudeToDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.Config.MagnitudeTo";
            displaceMagnitudeToDriver.DefaultValue.Value = 0.05f;
            displaceMagnitudeToDriver.Target.ForceLink(newMaterial.DisplaceMagnitudeTo);

            var displaceFromDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceFromDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.GlobalRadius2";
            displaceFromDriver.Target.ForceLink(newMaterial.DisplaceFrom);
            var displaceToDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceToDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.GlobalRadius1";
            displaceToDriver.Target.ForceLink(newMaterial.DisplaceTo);
            var emissionFromDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            emissionFromDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.GlobalRadius2";
            emissionFromDriver.Target.ForceLink(newMaterial.EmissionFrom);
            var emissionToDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            emissionToDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.GlobalRadius";
            emissionToDriver.Target.ForceLink(newMaterial.EmissionTo);

            newMaterial.OffsetFactor.Value = -0.1f;
        }
        #endregion RadialDisplace
        #endregion MaterialSetup

        static public IAssetProvider<Material> CreateAlphaMaterial(IAssetProvider<Material> originalMaterial, StatueType statueType, Slot destination)
        {
            switch(statueType)
            {
                case StatueType.AlphaFade:
                    switch(originalMaterial)
                    {
                        case PBS_DualSidedMetallic dsm:
                            {
                                var newMaterial = destination.AttachComponent<PBS_DualSidedMetallic>();
                                SetupAlphaFadeDualsidedMaterial(dsm, newMaterial, destination);
                                return newMaterial;
                            }
                        case PBS_DualSidedSpecular dss:
                            {
                                var newMaterial = destination.AttachComponent<PBS_DualSidedSpecular>();
                                SetupAlphaFadeDualsidedMaterial(dss, newMaterial, destination);
                                return newMaterial;
                            }
                        case IPBS_Metallic m:
                            {
                                var newMaterial = destination.AttachComponent<PBS_Metallic>();
                                SetupAlphaFadePBSMaterial(m, newMaterial, destination);
                                return newMaterial;
                            }
                        case IPBS_Specular s:
                            {
                                var newMaterial = destination.AttachComponent<PBS_Specular>();
                                SetupAlphaFadePBSMaterial(s, newMaterial, destination);
                                return newMaterial;
                            }
                        case XiexeToonMaterial x:
                            {
                                var newMaterial = destination.AttachComponent<XiexeToonMaterial>();
                                SetupAlphaFadeXiexeMaterial(x, newMaterial, destination);
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
                                    var newMaterial = destination.AttachComponent<PBS_DualSidedMetallic>();
                                    SetupAlphaCutoutDualsidedMaterial(dsm, newMaterial, destination);
                                    return newMaterial;
                                }
                            case PBS_DualSidedSpecular dss:
                                {
                                    var newMaterial = destination.AttachComponent<PBS_DualSidedSpecular>();
                                    SetupAlphaCutoutDualsidedMaterial(dss, newMaterial, destination);
                                    return newMaterial;
                                }
                            case IPBS_Metallic m:
                                {
                                    var newMaterial = destination.AttachComponent<PBS_Metallic>();
                                    SetupAlphaCutoutPBSMaterial(m, newMaterial, destination);
                                    return newMaterial;
                                }
                            case IPBS_Specular s:
                                {
                                    var newMaterial = destination.AttachComponent<PBS_Specular>();
                                    SetupAlphaCutoutPBSMaterial(s, newMaterial, destination);
                                    return newMaterial;
                                }
                            case XiexeToonMaterial x:
                                {
                                    var newMaterial = destination.AttachComponent<XiexeToonMaterial>();
                                    SetupAlphaCutoutXiexeMaterial(x, newMaterial, destination);
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
                                    var newMaterial = destination.AttachComponent<PBS_SliceMetallic>();
                                    SetupSlicerPlanePBSMaterial(m, newMaterial, destination);
                                    return newMaterial;
                                }
                            case IPBS_Specular s:
                                {
                                    var newMaterial = destination.AttachComponent<PBS_SliceSpecular>();
                                    SetupSlicerPlanePBSMaterial(s, newMaterial, destination);
                                    return newMaterial;
                                }
                            default:
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
                                    var newMaterial = destination.AttachComponent<PBS_DistanceLerpMetallic>();
                                    SetupRadialDisplacePBSMaterial(m, newMaterial, destination);
                                    return newMaterial;
                                }
                            case IPBS_Specular s:
                                {
                                    var newMaterial = destination.AttachComponent<PBS_DistanceLerpSpecular>();
                                    SetupRadialDisplacePBSMaterial(s, newMaterial, destination);
                                    return newMaterial;
                                }
                            default:
                                destination.Name = "Material type was not supported for RadialSlicer";
                                break;
                        }
                        break;
                    }
            }

            destination.NameField.Value = "Failed To Create Material";
            return originalMaterial;
        }

        public static void CopyStatueMaterialProperties(IAssetProvider<Material> from, IAssetProvider<Material> to)
        {
            ICommonMaterial commonMaterial = from as ICommonMaterial;
            ICommonMaterial commonMaterial2 = to as ICommonMaterial;
            if (commonMaterial != null && commonMaterial2 != null)
            {
                commonMaterial2.NormalScale = commonMaterial.NormalScale;
                commonMaterial2.NormalTextureScale = commonMaterial.NormalTextureScale;
                commonMaterial2.NormalTextureOffset = commonMaterial.NormalTextureOffset;
                commonMaterial2.NormalMap = commonMaterial.NormalMap;
            }

            IPBS_Material iPBS_Material = from as IPBS_Material;
            IPBS_Material iPBS_Material2 = to as IPBS_Material;
            if (iPBS_Material2 != null && commonMaterial != null)
            {
                iPBS_Material2.TextureOffset = commonMaterial.TextureOffset;
                iPBS_Material2.TextureScale = commonMaterial.TextureScale;
                iPBS_Material2.NormalMap = commonMaterial.NormalMap;
            }

            if (commonMaterial2 != null && iPBS_Material != null)
            {
                commonMaterial2.TextureOffset = iPBS_Material.TextureOffset;
                commonMaterial2.TextureScale = iPBS_Material.TextureScale;
                commonMaterial2.NormalMap = iPBS_Material.NormalMap;
            }

            if (iPBS_Material != null && iPBS_Material2 != null)
            {
                iPBS_Material2.TextureScale = iPBS_Material.TextureScale;
                iPBS_Material2.TextureOffset = iPBS_Material.TextureOffset;
                iPBS_Material2.NormalScale = iPBS_Material.NormalScale;
                iPBS_Material2.NormalMap = iPBS_Material.NormalMap;
                iPBS_Material2.OcclusionMap = iPBS_Material.OcclusionMap;
            }
        }
    }
}
