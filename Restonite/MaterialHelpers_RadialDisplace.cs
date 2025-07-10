using Elements.Core;
using FrooxEngine;

namespace Restonite;

internal static partial class MaterialHelpers
{
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
}