using Elements.Core;
using FrooxEngine;
using System.Linq;

namespace Restonite
{
    internal static class MaterialHelpers
    {
        public static IAssetProvider<Material> CreateStatueMaterial(IAssetProvider<Material> originalMaterial, IAssetProvider<Material> statueMaterial, Slot destination)
        {
            var newMaterial = destination.CopyComponent((AssetProvider<Material>)statueMaterial);

            CopyStatueMaterialProperties(originalMaterial, (IAssetProvider<Material>)newMaterial);

            return (IAssetProvider<Material>)newMaterial;
        }

        #region MaterialSetup

        #region AlphaFadeSetup

        private static void SetupAlphaFadePBSMaterial(IPBS_Material oldMaterial, PBS_Material newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            var bodyNormalPersistMultiDriver = destination.AttachComponent<ValueMultiDriver<bool>>();
            var bodyNormalPersistDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            bodyNormalPersistDriver.VariableName.Value = "Avatar/Statue.BodyNormal.Persist";
            bodyNormalPersistDriver.Target.ForceLink(bodyNormalPersistMultiDriver.Value);

            // Save original albedo
            var multiplierGradientDriver = destination.AttachComponent<ValueGradientDriver<colorX>>();
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.AlbedoColor.Value;
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.AlbedoColor.Value * new colorX(1.0f, 1.0f, 1.0f, 0.0f);
            multiplierGradientDriver.Points.Last().Position.Value = 1.0f;

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Progress";
            alphaDriver.Target.ForceLink(multiplierGradientDriver.Progress);

            // Gate gradiant driver through BodyNormal.Persist
            var bodyNormalPersistGate = destination.AttachComponent<BooleanValueDriver<colorX>>();
            bodyNormalPersistGate.TargetField.ForceLink(newMaterial.AlbedoColor);
            bodyNormalPersistGate.TrueValue.Value = newMaterial.AlbedoColor.Value;
            multiplierGradientDriver.Target.ForceLink(bodyNormalPersistGate.FalseValue);
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersistGate.State);

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

            // Gate blendmode through BodyNormal.Persist
            var bodyNormalPersist = destination.AttachComponent<ValueField<bool>>();
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersist.Value);
            var bodyNormalGreaterThan0 = destination.AttachComponent<ValueField<bool>>();
            blendModeActiveDriver.Target.ForceLink(bodyNormalGreaterThan0.Value);

            var bodyNormalConditionDriver = destination.AttachComponent<MultiBoolConditionDriver>();
            var condition1 = bodyNormalConditionDriver.Conditions.Add();
            condition1.Field.Value = bodyNormalPersist.Value.ReferenceID;
            condition1.Invert.Value = true;
            var condition2 = bodyNormalConditionDriver.Conditions.Add();
            condition2.Field.Value = bodyNormalGreaterThan0.Value.ReferenceID;
            bodyNormalConditionDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        private static void SetupAlphaFadeDualsidedMaterial(PBS_DualSidedMaterial oldMaterial, PBS_DualSidedMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            var bodyNormalPersistDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            bodyNormalPersistDriver.VariableName.Value = "Avatar/Statue.BodyNormal.Persist";

            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Progress";

            // Gate alpha driver through BodyNormal.Persist
            var bodyNormalPersistGate = destination.AttachComponent<BooleanValueDriver<float>>();
            bodyNormalPersistGate.TargetField.ForceLink(newMaterial.AlphaClip);
            bodyNormalPersistGate.TrueValue.Value = 0.0f;

            alphaDriver.Target.ForceLink(bodyNormalPersistGate.FalseValue);
            bodyNormalPersistDriver.Target.ForceLink(bodyNormalPersistGate.State);

            newMaterial.AlphaHandling.Value = AlphaHandling.AlphaBlend;
            newMaterial.OffsetFactor.Value = -0.1f;
        }

        private static void SetupAlphaFadeXiexeMaterial(XiexeToonMaterial oldMaterial, XiexeToonMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            var bodyNormalPersistMultiDriver = destination.AttachComponent<ValueMultiDriver<bool>>();
            var bodyNormalPersistDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            bodyNormalPersistDriver.VariableName.Value = "Avatar/Statue.BodyNormal.Persist";
            bodyNormalPersistDriver.Target.ForceLink(bodyNormalPersistMultiDriver.Value);

            // Save original albedo
            var multiplierGradientDriver = destination.AttachComponent<ValueGradientDriver<colorX>>();
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.Color.Value;
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.Color.Value * new colorX(1.0f, 1.0f, 1.0f, 0.0f);
            multiplierGradientDriver.Points.Last().Position.Value = 1.0f;

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Progress";
            alphaDriver.Target.ForceLink(multiplierGradientDriver.Progress);

            // Gate gradiant driver through BodyNormal.Persist
            var bodyNormalPersistGate = destination.AttachComponent<BooleanValueDriver<colorX>>();
            bodyNormalPersistGate.TargetField.ForceLink(newMaterial.Color);
            bodyNormalPersistGate.TrueValue.Value = newMaterial.Color.Value;
            multiplierGradientDriver.Target.ForceLink(bodyNormalPersistGate.FalseValue);
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersistGate.State);

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

            // Gate blendmode through BodyNormal.Persist
            var bodyNormalPersist = destination.AttachComponent<ValueField<bool>>();
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersist.Value);
            var bodyNormalGreaterThan0 = destination.AttachComponent<ValueField<bool>>();
            blendModeActiveDriver.Target.ForceLink(bodyNormalGreaterThan0.Value);

            var bodyNormalConditionDriver = destination.AttachComponent<MultiBoolConditionDriver>();
            var condition1 = bodyNormalConditionDriver.Conditions.Add();
            condition1.Field.Value = bodyNormalPersist.Value.ReferenceID;
            condition1.Invert.Value = true;
            var condition2 = bodyNormalConditionDriver.Conditions.Add();
            condition2.Field.Value = bodyNormalGreaterThan0.Value.ReferenceID;
            bodyNormalConditionDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        private static void SetupAlphaFadeUnlitMaterial(UnlitMaterial oldMaterial, UnlitMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            var bodyNormalPersistMultiDriver = destination.AttachComponent<ValueMultiDriver<bool>>();
            var bodyNormalPersistDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            bodyNormalPersistDriver.VariableName.Value = "Avatar/Statue.BodyNormal.Persist";
            bodyNormalPersistDriver.Target.ForceLink(bodyNormalPersistMultiDriver.Value);

            // Save original albedo
            var multiplierGradientDriver = destination.AttachComponent<ValueGradientDriver<colorX>>();
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.TintColor.Value;
            multiplierGradientDriver.Points.Add().Value.Value = newMaterial.TintColor.Value * new colorX(1.0f, 1.0f, 1.0f, 0.0f);
            multiplierGradientDriver.Points.Last().Position.Value = 1.0f;

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Progress";
            alphaDriver.Target.ForceLink(multiplierGradientDriver.Progress);

            // Gate gradiant driver through BodyNormal.Persist
            var bodyNormalPersistGate = destination.AttachComponent<BooleanValueDriver<colorX>>();
            bodyNormalPersistGate.TargetField.ForceLink(newMaterial.TintColor);
            bodyNormalPersistGate.TrueValue.Value = newMaterial.TintColor.Value;
            multiplierGradientDriver.Target.ForceLink(bodyNormalPersistGate.FalseValue);
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersistGate.State);

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

            // Gate blendmode through BodyNormal.Persist
            var bodyNormalPersist = destination.AttachComponent<ValueField<bool>>();
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersist.Value);
            var bodyNormalGreaterThan0 = destination.AttachComponent<ValueField<bool>>();
            blendModeActiveDriver.Target.ForceLink(bodyNormalGreaterThan0.Value);

            var bodyNormalConditionDriver = destination.AttachComponent<MultiBoolConditionDriver>();
            var condition1 = bodyNormalConditionDriver.Conditions.Add();
            condition1.Field.Value = bodyNormalPersist.Value.ReferenceID;
            condition1.Invert.Value = true;
            var condition2 = bodyNormalConditionDriver.Conditions.Add();
            condition2.Field.Value = bodyNormalGreaterThan0.Value.ReferenceID;
            bodyNormalConditionDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        #endregion AlphaFadeSetup

        #region AlphaCutoutSetup

        private static void SetupAlphaCutoutPBSMaterial(IPBS_Material oldMaterial, PBS_Material newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            var bodyNormalPersistMultiDriver = destination.AttachComponent<ValueMultiDriver<bool>>();
            var bodyNormalPersistDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            bodyNormalPersistDriver.VariableName.Value = "Avatar/Statue.BodyNormal.Persist";
            bodyNormalPersistDriver.Target.ForceLink(bodyNormalPersistMultiDriver.Value);

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Cutout";

            // Gate alpha driver through BodyNormal.Persist
            var bodyNormalPersistGate = destination.AttachComponent<BooleanValueDriver<float>>();
            bodyNormalPersistGate.TargetField.ForceLink(newMaterial.AlphaCutoff);
            bodyNormalPersistGate.TrueValue.Value = 0.0f;

            alphaDriver.Target.ForceLink(bodyNormalPersistGate.FalseValue);
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersistGate.State);

            // Drive blendmode of material
            var blendModeActiveDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            blendModeActiveDriver.VariableName.Value = "Avatar/Statue.BodyNormal.GreaterThan0";
            var blendModeBoolDriver = destination.AttachComponent<BooleanValueDriver<BlendMode>>();
            blendModeBoolDriver.TargetField.ForceLink(newMaterial.BlendMode);

            // Save original blend mode
            blendModeBoolDriver.FalseValue.Value = oldMaterial.BlendMode;
            blendModeBoolDriver.TrueValue.Value = BlendMode.Cutout;

            // Gate blendmode through BodyNormal.Persist
            var bodyNormalPersist = destination.AttachComponent<ValueField<bool>>();
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersist.Value);
            var bodyNormalGreaterThan0 = destination.AttachComponent<ValueField<bool>>();
            blendModeActiveDriver.Target.ForceLink(bodyNormalGreaterThan0.Value);

            var bodyNormalConditionDriver = destination.AttachComponent<MultiBoolConditionDriver>();
            var condition1 = bodyNormalConditionDriver.Conditions.Add();
            condition1.Field.Value = bodyNormalPersist.Value.ReferenceID;
            condition1.Invert.Value = true;
            var condition2 = bodyNormalConditionDriver.Conditions.Add();
            condition2.Field.Value = bodyNormalGreaterThan0.Value.ReferenceID;
            bodyNormalConditionDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        private static void SetupAlphaCutoutDualsidedMaterial(PBS_DualSidedMaterial oldMaterial, PBS_DualSidedMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            // Drive gradient driver's progress
            var bodyNormalPersistDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            bodyNormalPersistDriver.VariableName.Value = "Avatar/Statue.BodyNormal.Persist";

            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Cutout";

            // Gate alpha driver through BodyNormal.Persist
            var bodyNormalPersistGate = destination.AttachComponent<BooleanValueDriver<float>>();
            bodyNormalPersistGate.TargetField.ForceLink(newMaterial.AlphaClip);
            bodyNormalPersistGate.TrueValue.Value = 0.0f;

            alphaDriver.Target.ForceLink(bodyNormalPersistGate.FalseValue);
            bodyNormalPersistDriver.Target.ForceLink(bodyNormalPersistGate.State);

            newMaterial.AlphaHandling.Value = AlphaHandling.AlphaClip;
            newMaterial.OffsetFactor.Value = -0.1f;
        }

        private static void SetupAlphaCutoutXiexeMaterial(XiexeToonMaterial oldMaterial, XiexeToonMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);

            var bodyNormalPersistMultiDriver = destination.AttachComponent<ValueMultiDriver<bool>>();
            var bodyNormalPersistDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            bodyNormalPersistDriver.VariableName.Value = "Avatar/Statue.BodyNormal.Persist";
            bodyNormalPersistDriver.Target.ForceLink(bodyNormalPersistMultiDriver.Value);

            // Drive gradient driver's progress
            var alphaDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            alphaDriver.VariableName.Value = "Avatar/Statue.Material.Cutout";

            // Gate alpha driver through BodyNormal.Persist
            var bodyNormalPersistGate = destination.AttachComponent<BooleanValueDriver<float>>();
            bodyNormalPersistGate.TargetField.ForceLink(newMaterial.AlphaClip);
            bodyNormalPersistGate.TrueValue.Value = 0.0f;

            alphaDriver.Target.ForceLink(bodyNormalPersistGate.FalseValue);
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersistGate.State);

            // Drive blendmode of material
            var blendModeActiveDriver = destination.AttachComponent<DynamicValueVariableDriver<bool>>();
            blendModeActiveDriver.VariableName.Value = "Avatar/Statue.BodyNormal.GreaterThan0";
            var blendModeBoolDriver = destination.AttachComponent<BooleanValueDriver<BlendMode>>();
            blendModeBoolDriver.TargetField.ForceLink(newMaterial.BlendMode);

            // Save original blend mode
            blendModeBoolDriver.FalseValue.Value = oldMaterial.BlendMode;
            blendModeBoolDriver.TrueValue.Value = BlendMode.Cutout;

            // Gate blendmode through BodyNormal.Persist
            var bodyNormalPersist = destination.AttachComponent<ValueField<bool>>();
            bodyNormalPersistMultiDriver.Drives.Add().ForceLink(bodyNormalPersist.Value);
            var bodyNormalGreaterThan0 = destination.AttachComponent<ValueField<bool>>();
            blendModeActiveDriver.Target.ForceLink(bodyNormalGreaterThan0.Value);

            var bodyNormalConditionDriver = destination.AttachComponent<MultiBoolConditionDriver>();
            var condition1 = bodyNormalConditionDriver.Conditions.Add();
            condition1.Field.Value = bodyNormalPersist.Value.ReferenceID;
            condition1.Invert.Value = true;
            var condition2 = bodyNormalConditionDriver.Conditions.Add();
            condition2.Field.Value = bodyNormalGreaterThan0.Value.ReferenceID;
            bodyNormalConditionDriver.Target.ForceLink(blendModeBoolDriver.State);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        #endregion AlphaCutoutSetup

        #region SlicerPlaneSetup

        private static void SetupSlicerPlanePBSMaterial(IPBS_Material oldMaterial, PBS_Slice newMaterial, Slot destination)
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

        private static void SetupRadialDisplacePBSMaterial(IPBS_Material oldMaterial, PBS_DistanceLerpMaterial newMaterial, Slot destination)
        {
            FrooxEngine.MaterialHelper.CopyMaterialProperties(oldMaterial, newMaterial);
            var sphere = newMaterial.Points.Add();

            var posDriver = destination.AttachComponent<DynamicValueVariableDriver<float3>>();
            posDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.GlobalOriginPoint";
            posDriver.Target.ForceLink(sphere.Position);

            var edgeColorDriver = destination.AttachComponent<DynamicValueVariableDriver<colorX>>();
            edgeColorDriver.VariableName.Value = "Avatar/Statue.Slicer.EdgeEmissiveColor";
            edgeColorDriver.Target.ForceLink(newMaterial.EmissionColorFrom);
            newMaterial.EmissionColorTo.Value = new colorX(0.0f);

            // This needs to be the case because of behavior if both are 0; if 0, defaults to To
            var displaceMagnitudeFromDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceMagnitudeFromDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.DisplaceMagnitudeFrom";
            displaceMagnitudeFromDriver.DefaultValue.Value = -0.001f;
            displaceMagnitudeFromDriver.Target.ForceLink(newMaterial.DisplaceMagnitudeFrom);

            var displaceMagnitudeToDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceMagnitudeToDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.DisplaceMagnitudeTo";
            displaceMagnitudeToDriver.DefaultValue.Value = 0.001f;
            displaceMagnitudeToDriver.Target.ForceLink(newMaterial.DisplaceMagnitudeTo);

            var displaceFromDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceFromDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.DisplaceFrom";
            displaceFromDriver.Target.ForceLink(newMaterial.DisplaceFrom);
            var displaceToDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            displaceToDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.DisplaceTo";
            displaceToDriver.Target.ForceLink(newMaterial.DisplaceTo);
            var emissionFromDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            emissionFromDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.EmissionFrom";
            emissionFromDriver.Target.ForceLink(newMaterial.EmissionFrom);
            var emissionToDriver = destination.AttachComponent<DynamicValueVariableDriver<float>>();
            emissionToDriver.VariableName.Value = "Avatar/Statue.RadialStatuefy.EmissionTo";
            emissionToDriver.Target.ForceLink(newMaterial.EmissionTo);

            newMaterial.OffsetFactor.Value = -0.1f;
        }

        #endregion RadialDisplace

        #endregion MaterialSetup

        public static IAssetProvider<Material> CopyMaterialToSlot(IAssetProvider<Material> originalMaterial, Slot destination)
        {
            switch (originalMaterial)
            {
                case PBS_DistanceLerpMetallic dlm:
                    {
                        var newMaterial = destination.AttachComponent<PBS_Metallic>();
                        FrooxEngine.MaterialHelper.CopyMaterialProperties(dlm, newMaterial);
                        return newMaterial;
                    }
                case PBS_DistanceLerpSpecular dls:
                    {
                        var newMaterial = destination.AttachComponent<PBS_Specular>();
                        FrooxEngine.MaterialHelper.CopyMaterialProperties(dls, newMaterial);
                        return newMaterial;
                    }
                case PBS_SliceMetallic sm:
                    {
                        var newMaterial = destination.AttachComponent<PBS_Metallic>();
                        FrooxEngine.MaterialHelper.CopyMaterialProperties(sm, newMaterial);
                        return newMaterial;
                    }
                case PBS_SliceSpecular ss:
                    {
                        var newMaterial = destination.AttachComponent<PBS_Specular>();
                        FrooxEngine.MaterialHelper.CopyMaterialProperties(ss, newMaterial);
                        return newMaterial;
                    }
                default:
                    return (IAssetProvider<Material>)destination.CopyComponent((AssetProvider<Material>)originalMaterial);
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
                                SetupAlphaFadeDualsidedMaterial(dsm, newMaterial, destination);
                                return newMaterial;
                            }
                        case PBS_DualSidedSpecular dss:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_DualSidedSpecular>();
                                SetupAlphaFadeDualsidedMaterial(dss, newMaterial, destination);
                                return newMaterial;
                            }
                        case IPBS_Metallic m:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_Metallic>();
                                SetupAlphaFadePBSMaterial(m, newMaterial, destination);
                                return newMaterial;
                            }
                        case IPBS_Specular s:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<PBS_Specular>();
                                SetupAlphaFadePBSMaterial(s, newMaterial, destination);
                                return newMaterial;
                            }
                        case XiexeToonMaterial x:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<XiexeToonMaterial>();
                                SetupAlphaFadeXiexeMaterial(x, newMaterial, destination);
                                return newMaterial;
                            }
                        case UnlitMaterial x:
                            {
                                Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                var newMaterial = destination.AttachComponent<UnlitMaterial>();
                                SetupAlphaFadeUnlitMaterial(x, newMaterial, destination);
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
                                    SetupAlphaCutoutDualsidedMaterial(dsm, newMaterial, destination);
                                    return newMaterial;
                                }
                            case PBS_DualSidedSpecular dss:
                                {
                                    Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                    var newMaterial = destination.AttachComponent<PBS_DualSidedSpecular>();
                                    SetupAlphaCutoutDualsidedMaterial(dss, newMaterial, destination);
                                    return newMaterial;
                                }
                            case IPBS_Metallic m:
                                {
                                    Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                    var newMaterial = destination.AttachComponent<PBS_Metallic>();
                                    SetupAlphaCutoutPBSMaterial(m, newMaterial, destination);
                                    return newMaterial;
                                }
                            case IPBS_Specular s:
                                {
                                    Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                    var newMaterial = destination.AttachComponent<PBS_Specular>();
                                    SetupAlphaCutoutPBSMaterial(s, newMaterial, destination);
                                    return newMaterial;
                                }
                            case XiexeToonMaterial x:
                                {
                                    Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
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
                                    Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                    var newMaterial = destination.AttachComponent<PBS_SliceMetallic>();
                                    SetupSlicerPlanePBSMaterial(m, newMaterial, destination);
                                    return newMaterial;
                                }
                            case IPBS_Specular s:
                                {
                                    Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                    var newMaterial = destination.AttachComponent<PBS_SliceSpecular>();
                                    SetupSlicerPlanePBSMaterial(s, newMaterial, destination);
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
                                    return newMaterial;
                                }
                            case IPBS_Specular s:
                                {
                                    Log.Debug($"Creating {originalMaterial.GetType().Name} as {statueType}");
                                    var newMaterial = destination.AttachComponent<PBS_DistanceLerpSpecular>();
                                    SetupRadialDisplacePBSMaterial(s, newMaterial, destination);
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

        public static void CopyStatueMaterialProperties(IAssetProvider<Material> from, IAssetProvider<Material> to)
        {
            ICommonMaterial commonMaterial = from as ICommonMaterial;
            ICommonMaterial commonMaterial2 = to as ICommonMaterial;
            if (commonMaterial is not null && commonMaterial2 is not null)
            {
                commonMaterial2.NormalScale = commonMaterial.NormalScale;
                commonMaterial2.NormalTextureScale = commonMaterial.NormalTextureScale;
                commonMaterial2.NormalTextureOffset = commonMaterial.NormalTextureOffset;
                commonMaterial2.NormalMap = commonMaterial.NormalMap;
            }

            IPBS_Material iPBS_Material = from as IPBS_Material;
            IPBS_Material iPBS_Material2 = to as IPBS_Material;
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
        }
    }
}
