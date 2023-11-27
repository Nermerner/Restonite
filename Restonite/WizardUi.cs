using FrooxEngine.UIX;
using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.CommonAvatar;

namespace Restonite
{
    internal class WizardUi
    {
        private readonly Text _debugText;
        private readonly Checkbox _skinnedMeshRenderersOnly;
        private readonly Checkbox _defaultMaterialAsIs;
        private readonly ReferenceField<Slot> _avatarRoot;
        private readonly ReferenceField<Slot> _statueSystemFallback;
        private readonly ValueField<StatueType> _statueType;
        private readonly Button _confirmButton;
        private readonly ReferenceField<IAssetProvider<Material>> _baseStatueMaterial;
        private readonly ReferenceMultiplexer<MeshRenderer> _foundMeshRenderers;
        private readonly Slot _wizardSlot;
        private readonly Avatar _avatar;
        private readonly Action<Slot, StatueType, SyncRef<Slot>, bool> _installSystemOnAvatar;

        public void LogDebug(string logMessage)
        {
            _debugText.Content.Value = $"<color=gray>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogInfo(string logMessage)
        {
            _debugText.Content.Value = $"<color=#e0e0e0>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogWarn(string logMessage)
        {
            _debugText.Content.Value = $"<color=yellow>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogError(string logMessage)
        {
            _debugText.Content.Value = $"<color=red>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogSuccess(string logMessage)
        {
            _debugText.Content.Value = $"<color=green>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public WizardUi(Slot slot, string title, Avatar avatarReader, Action<Slot, StatueType, SyncRef<Slot>, bool> onInstall)
        {
            _wizardSlot = slot;
            _avatar = avatarReader;
            _installSystemOnAvatar = onInstall;

            Slot Data = _wizardSlot.AddSlot("Data");

            _avatarRoot = Data.AddSlot("avatarRoot").AttachComponent<ReferenceField<Slot>>();
            _statueSystemFallback = Data.AddSlot("statueSystemFallback").AttachComponent<ReferenceField<Slot>>();
            _baseStatueMaterial = Data.AddSlot("baseMaterial").AttachComponent<ReferenceField<IAssetProvider<Material>>>();
            _statueType = Data.AddSlot("statueType").AttachComponent<ValueField<StatueType>>();
            _foundMeshRenderers = Data.AddSlot("foundMRs").AttachComponent<ReferenceMultiplexer<MeshRenderer>>();

            /*
            - Add avatar space
            - Add statue system objects
            - Enumerate SkinnedMeshRenderers
            - Do the algorithm thing to create statue object(s)
            - Add dynamic bool driver for Statue.BodyNormal and Statue.BodyStatue
            - Make Statue.DisableOnFreeze disable:
                 -> VRIK
                 -> Dynamic Bone Slot's Enabled State
                 -> Viseme Drivers
                 -> Expression Drivers
                 -> Animetion Systems (Wigglers, Panners, etc.)
                 -> Hand Posers
                 -> Custom Grabable Systems installed on avatar
                 -> Custom Nameplaces/Badges
                    * If you don't have a custom Nameplate/Badge you will need one to have it hidden.
            - Drives in general
            - Materials: generate default materials (Material 0 is used for vision overlay)
            - Configure material system specifically
            */

            var UI = RadiantUI_Panel.SetupPanel(slot, title, new float2(800f, 1000f));
            RadiantUI_Constants.SetupEditorStyle(UI);

            UI.Canvas.MarkDeveloper();
            UI.Canvas.AcceptPhysicalTouch.Value = false;

            UI.SplitHorizontally(0.5f, out RectTransform left, out RectTransform right);

            left.OffsetMax.Value = new float2(-10f);
            right.OffsetMin.Value = new float2(10f);

            UI.NestInto(left);

            UI.SplitVertically(0.5f, out RectTransform top, out RectTransform bottom);

            UI.NestInto(top);

            UI.Style.MinHeight = 24f;
            UI.Style.PreferredHeight = 24f;
            UI.Style.PreferredWidth = 400f;
            UI.Style.MinWidth = 400f;

            VerticalLayout verticalLayout = UI.VerticalLayout(4f, childAlignment: Alignment.TopLeft);
            verticalLayout.ForceExpandHeight.Value = true;

            _skinnedMeshRenderersOnly = UI.HorizontalElementWithLabel("Skinned Meshes only", 0.925f, () => UI.Checkbox(true));

            UI.Text("Avatar Root Slot:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
            UI.Next("Avatar Root Slot");
            var avatarField = UI.Current.AttachComponent<RefEditor>();
            avatarField.Setup(_avatarRoot.Reference);
            _avatarRoot.Reference.OnValueChange += OnReferencesChanged;

            UI.Text("Default statue material:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
            UI.Next("Base Texture");
            UI.Current.AttachComponent<RefEditor>().Setup(_baseStatueMaterial.Reference);
            _baseStatueMaterial.Reference.OnValueChange += OnReferencesChanged;

            _defaultMaterialAsIs = UI.HorizontalElementWithLabel("Use default material as-is", 0.925f, () => UI.Checkbox(false));

            UI.Text("Default transition type:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
            UI.Next("Default transition type");
            UI.Current.AttachComponent<EnumMemberEditor>().Setup(_statueType.Value);

            UI.Spacer(24f);
            _confirmButton = UI.Button("Install");
            _confirmButton.LocalPressed += OnInstallButtonPressed;

            UI.Text("(Optional, Advanced) Override system:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
            UI.Next("(Optional, Advanced) Override system:");
            UI.Current.AttachComponent<RefEditor>().Setup(_statueSystemFallback.Reference);

            UI.Spacer(24f);


            UI.NestInto(bottom);

            UI.SplitVertically(0.06f, out RectTransform logTop, out RectTransform logBottom);

            UI.NestInto(logTop);
            var logTitle = UI.Text((LocaleString)"Log", true, new Alignment?(), true, null);
            logTitle.HorizontalAlign.Value = TextHorizontalAlignment.Left;
            logTitle.VerticalAlign.Value = TextVerticalAlignment.Top;

            UI.NestInto(logBottom);
            UI.ScrollArea();
            UI.FitContent(SizeFit.Disabled, SizeFit.MinSize);
            UI.VerticalLayout();

            _debugText = UI.Text((LocaleString)"", false, new Alignment?(), true, null);
            _debugText.HorizontalAlign.Value = TextHorizontalAlignment.Left;
            _debugText.VerticalAlign.Value = TextVerticalAlignment.Top;
            _debugText.Size.Value = 10f;
            _debugText.VerticalAutoSize.Value = false;
            _debugText.HorizontalAutoSize.Value = false;
            _debugText.Slot.RemoveComponent(_debugText.Slot.GetComponent<LayoutElement>());

            UI.NestInto(right);
            UI.ScrollArea();
            UI.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

            SyncMemberEditorBuilder.Build(_foundMeshRenderers.References, "Skinned Mesh Renderers found", null, UI);

            _wizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
        }

        private void OnReferencesChanged(SyncField<RefID> syncField)
        {
            _avatar.ReadAvatarRoot(_avatarRoot.Reference.Target, _baseStatueMaterial.Reference.Target, _skinnedMeshRenderersOnly.State.Value, _defaultMaterialAsIs.State.Value, _statueType.Value.Value);

            _foundMeshRenderers.References.Clear();
            _foundMeshRenderers.References.AddRange(_avatar.MeshRenderers.Select(x => x.NormalMeshRenderer));

            if (_avatar.HasExistingSystem)
                _confirmButton.LabelText = "Update";
            else if (_avatar.HasLegacySystem)
                _confirmButton.LabelText = "Update from legacy";
            else
                _confirmButton.LabelText = "Install";
        }

        private void OnInstallButtonPressed(IButton button, ButtonEventData eventData)
        {
            var scratchSpace = _wizardSlot.AddSlot("Scratch space");
            try
            {
                _installSystemOnAvatar(scratchSpace, _statueType.Value.Value, _statueSystemFallback.Reference, _defaultMaterialAsIs.State.Value);
                HighlightHelper.FlashHighlight(_avatarRoot.Reference.Target, (_a) => true, new colorX(0.5f, 0.5f, 0.5f, 1.0f));
            }
            catch (Exception ex)
            {
                var errorString = $"ERROR: Encountered exception during install: {ex.Message} / {ex}";
                LogError(errorString);
                LogError("ERROR: Sorry! We ran into an error installing the statue system.<br>Debugging information has been copied to your clipboard; please send it to the Statue devs!<br>(Arion, Azavit, Nermerner, Uruloke)");
                Engine.Current.InputInterface.Clipboard.SetText(_debugText.Content.Value);
            }
        }
    }
}
