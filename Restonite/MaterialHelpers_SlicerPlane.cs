using Elements.Core;
using FrooxEngine;

namespace Restonite;

internal static partial class MaterialHelpers
{
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
}