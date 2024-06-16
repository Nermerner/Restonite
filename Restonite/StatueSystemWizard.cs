using FrooxEngine;
using System;

namespace Restonite
{
    internal class StatueSystemWizard
    {
        #region Public Methods

        public static StatueSystemWizard GetOrCreateWizard(Slot x, Action<string> debug, Action<string> info, Action<string> warn, Action<string> error)
        {
            return new StatueSystemWizard(x, debug, info, warn, error);
        }

        public bool InstallSystemOnAvatar(Slot scratchSpace, Slot statueSystem, SyncRef<Slot> installSlot, SyncRef<Slot> contextMenuSlot)
        {
            Log.Info($"=== Starting install for avatar {_avatar.AvatarRoot.ToShortString()}");

            // Start
            if (!_avatar.VerifyInstallRequirements())
                return false;

            _avatar.SetScratchSpace(scratchSpace);

            _avatar.CreateOrUpdateSlots(installSlot);
            _avatar.SetupRootDynVar();

            // Add statue system objects
            _avatar.RemoveLegacySystem();
            _avatar.InstallRemasterSystem(statueSystem, contextMenuSlot);

            if (!_avatar.DuplicateMeshes())
                return false;

            // Materials:
            // 1. For each material that needs to be created, create a driver and default material
            // 2. For each old material, give it an appropriate blend mode

            // Create a map of normal materials -> statue materials
            _avatar.CollectMaterials();
            _avatar.GenerateNormalMaterials();
            _avatar.GenerateStatueMaterials();

            // Set up drivers
            _avatar.CopyBlendshapes();
            _avatar.CreateOrUpdateEnableDrivers();
            _avatar.CreateOrUpdateDisableOnFreeze();
            _avatar.CreateOrUpdateVoiceDrivers();

            _avatar.CreateOrUpdateDefaults();

            Log.Success("Setup completed successfully!");
            Log.Success("Re-equip your avatar before testing the system.");

            _avatar.OpenUserConfigInspector();

            return true;
        }

        #endregion

        #region Private Fields

        private readonly Avatar _avatar;
        private readonly WizardUi _ui;
        private readonly Slot _wizardSlot;

        #endregion

        #region Private Constructors

        private StatueSystemWizard(Slot x, Action<string> debug, Action<string> info, Action<string> warn, Action<string> error)
        {
            // Init editor
            _wizardSlot = x;
            _wizardSlot.Tag = "Developer";
            _wizardSlot.PersistentSelf = false;
            _wizardSlot.LocalScale *= 0.0008f;

            _avatar = new Avatar();

            _ui = new WizardUi(_wizardSlot, RestoniteMod.WIZARD_TITLE, _avatar, InstallSystemOnAvatar);

            Log.Setup(_ui, debug, info, warn, error);
        }

        #endregion
    }
}
