using Elements.Core;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using System.Collections.Generic;
using System.Linq;

namespace Restonite;

internal partial class Avatar
{
    #region Public Methods

    public void InstallRemasterSystem(Slot systemSlot, SyncRef<Slot> contextMenuSlot)
    {
        if (AvatarRoot is null || StatueRoot is null || _defaults is null)
            return;

        Log.Info("=== Installing Remaster system on avatar");

        // Remove the old system
        foreach (var toDestroy in AvatarRoot.GetChildrenWithTag("CopyToStatue"))
            toDestroy.Destroy();

        // Install the new system
        foreach (var copySlot in systemSlot.GetChildrenWithTag("CopyToStatue"))
        {
            Log.Info($"Adding {copySlot.ToShortString()} with tag {copySlot.Tag}");
            copySlot.SetParent(StatueRoot, false);
        }

        // Place context menu elsewhere if desired
        if (contextMenuSlot.Value != RefID.Null)
        {
            var rootContextMenu = StatueRoot.GetComponentInChildren<RootContextMenuItem>();
            if (rootContextMenu is not null)
            {
                var slot = rootContextMenu.Slot;
                var menuSlot = slot.FindChild("Statufication");
                if (menuSlot is not null)
                {
                    // Remove RootContextMenuItem
                    slot.RemoveComponent(rootContextMenu);

                    // Add new menu item reference the submenu
                    var itemSource = slot.AttachComponent<ContextMenuItemSource>();
                    itemSource.LabelText = "Statue";
                    itemSource.Sprite.Target = menuSlot.GetComponent<SpriteProvider>();
                    var subMenu = slot.AttachComponent<ContextMenuSubmenu>();
                    subMenu.ItemsRoot.Target = menuSlot;

                    slot.SetParent(contextMenuSlot.Target, false);
                }
            }
        }

        var updateSlot = systemSlot.GetChildrenWithTag("StatueUserConfig").FirstOrDefault();

        if (updateSlot is not null)
        {
            var oldDefaults = _defaults.GetComponentsInChildren<IDynamicVariable>().ConvertAll(x => new DynVarSlot(((Component)x).Slot, x));

            List<DynVarSlot> GetAvatarDynVarSlots(Slot slot) => slot
                    .GetComponentsInChildren<IDynamicVariable>(filter: x => x.VariableName.StartsWith("Avatar/"), slotFilter: x => x == slot || x.Parent == slot)
                    .ConvertAll(x => new DynVarSlot(((Component)x).Slot, x));

            // Update user config slot
            if (_userConfig is null)
            {
                Log.Info($"Adding {updateSlot.ToShortString()} with tag {updateSlot.Tag}");

                updateSlot.SetParent(StatueRoot, false);
                _userConfig = StatueRoot.GetChildrenWithTag("StatueUserConfig")[0];

                var dynVars = GetAvatarDynVarSlots(_userConfig);

                Log.Info($"Found {dynVars.Count} configs on {_userConfig.ToShortString()}");

                // Update existing user config slots
                var updateDynVars = oldDefaults.Join(dynVars,
                    defaults => defaults.DynamicVariable.VariableName,
                    system => system.DynamicVariable.VariableName,
                    (defaults, system) => new { Defaults = defaults, System = system }).ToList();
                foreach (var dynVar in updateDynVars)
                {
                    var a = dynVar.Defaults.DynamicVariable as dynamic;
                    var b = dynVar.System.DynamicVariable as dynamic;
                    var typeA = dynVar.Defaults.DynamicVariable.GetType();
                    var typeB = dynVar.System.DynamicVariable.GetType();

                    if (typeA.GetGenericTypeDefinition() == typeof(DynamicValueVariable<>) && typeB.GetGenericTypeDefinition() == typeof(DynamicValueVariable<>) && typeA.GenericTypeArguments[0] == typeB.GenericTypeArguments[0])
                    {
                        Log.Info($"Migrating user config slot for {dynVar.Defaults.DynamicVariable.VariableName}");
                        b.Value.Value = a.Value.Value;
                        dynVar.Defaults.Slot.Name = dynVar.System.Slot.Name;
                    }
                    else if (typeA.GetGenericTypeDefinition() == typeof(DynamicReferenceVariable<>) && typeB.GetGenericTypeDefinition() == typeof(DynamicReferenceVariable<>) && typeA.GenericTypeArguments[0] == typeB.GenericTypeArguments[0])
                    {
                        Log.Info($"Migrating user config slot for {dynVar.Defaults.DynamicVariable.VariableName}");
                        b.Reference.Target = a.Reference.Target;
                        dynVar.Defaults.Slot.Name = dynVar.System.Slot.Name;
                    }
                    else
                    {
                        Log.Warn($"User config slot for {dynVar.Defaults.DynamicVariable.VariableName} have differing data types, skipping");
                    }
                }
            }
            else
            {
                Log.Info($"Updating {_userConfig.ToShortString()} from {updateSlot.ToShortString()} with tag {updateSlot.Tag}");

                var avatarDynVars = GetAvatarDynVarSlots(_userConfig);
                var systemDynVars = GetAvatarDynVarSlots(updateSlot);

                Log.Info($"Found {avatarDynVars.Count} configs on avatar and {systemDynVars.Count} configs on system");

                avatarDynVars.AddRange(oldDefaults);

                // Clean up old user config slots no longer present in the system
                var oldDynVars = avatarDynVars.Where(x => !systemDynVars.Exists(y => y.DynamicVariable.VariableName == x.DynamicVariable.VariableName)).ToList();
                foreach (var dynVar in oldDynVars)
                {
                    Log.Info($"Removing old user config slot for {dynVar.DynamicVariable.VariableName}");
                    dynVar.Slot.Destroy();
                    avatarDynVars.Remove(dynVar);
                }

                // Add new user config slots present in the system
                var newDynVars = systemDynVars.Where(x => !avatarDynVars.Exists(y => y.DynamicVariable.VariableName == x.DynamicVariable.VariableName)).ToList();
                foreach (var dynVar in newDynVars)
                {
                    Log.Info($"Adding new user config slot for {dynVar.DynamicVariable.VariableName}");
                    dynVar.Slot.SetParent(_userConfig, false);
                }

                // Update existing user config slots
                var updateDynVars = avatarDynVars.Join(systemDynVars,
                    existing => existing.DynamicVariable.VariableName,
                    system => system.DynamicVariable.VariableName,
                    (existing, system) => new { Existing = existing, System = system }).ToList();
                foreach (var dynVar in updateDynVars)
                {
                    var typeA = dynVar.Existing.DynamicVariable.GetType();
                    var typeB = dynVar.System.DynamicVariable.GetType();

                    if (typeA == typeB)
                    {
                        Log.Info($"Updating user config slot for {dynVar.Existing.DynamicVariable.VariableName}");
                        dynVar.Existing.Slot.Name = dynVar.System.Slot.Name;
                    }
                    else
                    {
                        Log.Warn($"User config slot for {dynVar.Existing.DynamicVariable.VariableName} have differing data types, overwriting");
                        dynVar.Existing.Slot.Destroy();
                        dynVar.System.Slot.SetParent(_userConfig, false);
                    }
                }
            }

            _defaults.Destroy();
            _defaults = null;

            // Set default configs if null
            if (_userConfig is not null)
            {
                var oldSlots = _userConfig.GetAllChildren().Where(x => x.Parent == _userConfig).ToList();
                var newSlots = updateSlot.GetAllChildren().Where(x => x.Parent == updateSlot).ToList();

                var toAdd = newSlots.Where(x => !oldSlots.Exists(y => y.Name == x.Name)).ToList();

                foreach (var slot in toAdd)
                {
                    slot.SetParent(_userConfig, false);
                    Log.Info($"Adding {slot.ToShortString()} to {_userConfig.ToShortString()}");
                }

                AvatarRoot.RunInUpdates(3, () =>
                {
                    Log.Info($"Updating user config values in {_userConfig.ToShortString()}");

                    var avatarRoot = _userConfig.GetComponentInChildren<DynamicReferenceVariable<Slot>>(x => x.VariableName == "Avatar/Statue.AvatarRoot");
                    if (avatarRoot is not null && avatarRoot.Reference.Target is null)
                    {
                        avatarRoot.Reference.Target = AvatarRoot;
                        Log.Info($"Setting user config {avatarRoot.Slot.ToShortString()} to {AvatarRoot.ToShortString()}");
                    }

                    var soundEffectDefault = _userConfig.GetComponentInChildren<DynamicReferenceVariable<IAssetProvider<AudioClip>>>(x => x.VariableName == "Avatar/Statue.SoundEffect.Default");
                    if (soundEffectDefault is not null)
                    {
                        var audioClip = soundEffectDefault.Slot.GetComponent<StaticAudioClip>();
                        if (audioClip is null)
                            Log.Warn($"Couldn't find audio clip in {soundEffectDefault.ToShortString()}");

                        if (audioClip is not null && soundEffectDefault.Reference.Target is null)
                        {
                            soundEffectDefault.Reference.Target = audioClip;
                            Log.Info($"Setting user config {soundEffectDefault.Slot.ToShortString()} to {audioClip.ToShortString()}");
                        }
                    }

                    var slicerRefScale = _userConfig.GetComponentInChildren<DynamicValueVariable<float3>>(x => x.VariableName == "Avatar/Statue.Slicer.RefScale");
                    if (slicerRefScale is not null)
                    {
                        slicerRefScale.Value.Value = AvatarRoot.GlobalScale;
                        Log.Info($"Setting user config {slicerRefScale.Slot.ToShortString()} to {AvatarRoot.GlobalScale}");
                    }

                    var transitionTypes = MeshRenderers.SelectMany(x => x.MaterialSets).SelectMany(x => x).Select(x => x.TransitionType).Distinct().ToList();

                    var enableAlphaFade = _userConfig.GetComponentInChildren<DynamicValueVariable<bool>>(x => x.VariableName == "Avatar/Statue.Material.EnableAlphaFade");
                    if (enableAlphaFade is not null)
                    {
                        enableAlphaFade.Value.Value = transitionTypes.Contains(StatueType.AlphaFade);
                        Log.Info($"Setting user config {enableAlphaFade.Slot.ToShortString()} to {enableAlphaFade.Value.Value}");
                    }

                    var enableAlphaCutout = _userConfig.GetComponentInChildren<DynamicValueVariable<bool>>(x => x.VariableName == "Avatar/Statue.Material.EnableAlphaCutout");
                    if (enableAlphaCutout is not null)
                    {
                        enableAlphaCutout.Value.Value = transitionTypes.Contains(StatueType.AlphaCutout);
                        Log.Info($"Setting user config {enableAlphaCutout.Slot.ToShortString()} to {enableAlphaCutout.Value.Value}");
                    }

                    var enablePlanarSlice = _userConfig.GetComponentInChildren<DynamicValueVariable<bool>>(x => x.VariableName == "Avatar/Statue.Material.EnablePlanarSlice");
                    if (enablePlanarSlice is not null)
                    {
                        enablePlanarSlice.Value.Value = transitionTypes.Contains(StatueType.PlaneSlicer);
                        Log.Info($"Setting user config {enablePlanarSlice.Slot.ToShortString()} to {enablePlanarSlice.Value.Value}");
                    }

                    var enableRadialSlicer = _userConfig.GetComponentInChildren<DynamicValueVariable<bool>>(x => x.VariableName == "Avatar/Statue.Material.EnableRadialSlicer");
                    if (enableRadialSlicer is not null)
                    {
                        enableRadialSlicer.Value.Value = transitionTypes.Contains(StatueType.RadialSlicer);
                        Log.Info($"Setting user config {enableRadialSlicer.Slot.ToShortString()} to {enableRadialSlicer.Value.Value}");
                    }
                });
            }
        }
    }

    public void RemoveLegacySystem()
    {
        if (AvatarRoot is null || _legacySystem is null)
            return;

        Log.Info("=== Removing legacy system");

        var legacyDisableOnStatueSlot = _legacySystem.FindChild("Disable on Statue", false, false, -1);
        if (legacyDisableOnStatueSlot is not null)
        {
            var existingMultiDriver = legacyDisableOnStatueSlot.GetComponent<ValueMultiDriver<bool>>();
            if (existingMultiDriver is not null)
            {
                foreach (var drive in existingMultiDriver.Drives.Select(x => x.Target))
                {
                    var worker = drive.FindNearestParent<Worker>();
                    if (worker is DynamicBoneChain dbc)
                    {
                        Log.Debug($"Moving drive for {dbc.ToShortString()} to slot {dbc.Slot.ToShortString()}");
                        _existingDrivesForDisableOnFreeze.Add(dbc.Slot.ActiveSelf_Field);
                    }
                    else if (worker is EyeManager em)
                    {
                        Log.Debug($"Moving drive for {em.ToShortString()} to slot {em.Slot.ToShortString()}");
                        _existingDrivesForDisableOnFreeze.Add(em.Slot.ActiveSelf_Field);
                    }
                    else
                    {
                        _existingDrivesForDisableOnFreeze.Add(drive);
                    }
                }
            }
        }
        Log.Info($"Collected {_existingDrivesForDisableOnFreeze.Count} drivers for Disable on Statue");

        var blinder = AvatarRoot.GetComponent<VRIK>()?.Solver.BoneReferences.head.Slot?.FindChild("Blinder");
        if (blinder?.ActiveSelf_Field.IsDriven == true)
        {
            Log.Info($"Removing {blinder.ToShortString()}");
            blinder.Destroy();
        }

        var smoothTransforms = AvatarRoot.GetComponentsInChildren<SmoothTransform>(slotFilter: x => x.Name == "Target" && x.Parent.Name.EndsWith("Proxy"));
        foreach (var component in smoothTransforms)
        {
            Log.Info($"Removing {component.ToLongString()}");
            component.Slot.RemoveComponent(component);
        }

        _legacySystem.Destroy();
        _legacyAddons?.Destroy();
    }

    public bool VerifyInstallRequirements()
    {
        foreach (var map in MeshRenderers)
        {
            if (map.NormalMeshRenderer is not null)
            {
                // Check for any nested MeshRenderers
                var smrsInChildren = map.NormalMeshRenderer.Slot.GetComponentsInChildren<MeshRenderer>();
                if (smrsInChildren.Exists(x => x != map.NormalMeshRenderer))
                {
                    Log.Error($"Slot {map.NormalMeshRenderer.Slot.ToShortString()} has nested MeshRenderers, aborting");
                    return false;
                }
            }

            for (int set = 0; set < map.MaterialSets.Count; set++)
            {
                for (int i = 0; i < map.MaterialSets[set].Count; i++)
                {
                    var material = map.MaterialSets[set][i];

                    if (material.Normal is not null)
                    {
                        // Check for incompatible transition types for materials
                        if ((material.TransitionType == StatueType.PlaneSlicer || material.TransitionType == StatueType.RadialSlicer)
                            && material.Normal is not IPBS_Metallic && material.Normal is not IPBS_Specular && material.Normal is not PBS_DistanceLerpMaterial)
                        {
                            Log.Error($"{material.Normal.GetType().Name} does not support {material.TransitionType}, aborting");
                            return false;
                        }
                        else if ((material.TransitionType == StatueType.AlphaFade || material.TransitionType == StatueType.AlphaCutout)
                            && material.Normal is not PBS_DualSidedMetallic && material.Normal is not PBS_DualSidedSpecular
                            && material.Normal is not IPBS_Metallic && material.Normal is not IPBS_Specular
                            && material.Normal is not XiexeToonMaterial && material.Normal is not PBS_DistanceLerpMaterial
                            && material.Normal is not UnlitMaterial)
                        {
                            Log.Error($"{material.Normal.GetType().Name} does not support {material.TransitionType}, aborting");
                            return false;
                        }
                    }

                    // Check for missing statue materials
                    if (material.Statue is null || material.Statue.ReferenceID == RefID.Null)
                    {
                        Log.Error("Missing default statue material for some material slots, aborting");
                        return false;
                    }
                }
            }
        }

        return true;
    }

    #endregion
}
