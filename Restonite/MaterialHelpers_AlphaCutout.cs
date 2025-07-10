using FrooxEngine;

namespace Restonite;

internal static partial class MaterialHelpers
{
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

    private static void SetupAlphaCutoutUnlitMaterial(UnlitMaterial oldMaterial, UnlitMaterial newMaterial, Slot destination)
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
}