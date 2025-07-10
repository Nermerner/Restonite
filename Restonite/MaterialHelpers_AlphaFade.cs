using Elements.Core;
using FrooxEngine;
using System.Linq;

namespace Restonite;

internal static partial class MaterialHelpers
{
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
}