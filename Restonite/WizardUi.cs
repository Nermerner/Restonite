using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Linq;

namespace Restonite
{
    internal class WizardUi
    {
        #region Public Constructors

        public WizardUi(Slot slot, string title, Avatar avatarReader, Func<Slot, SyncRef<Slot>, bool> onInstall)
        {
            try
            {
                _wizardSlot = slot;
                _avatar = avatarReader;
                _installSystemOnAvatar = onInstall;

                Slot Data = _wizardSlot.AddSlot("Data");

                _avatarRoot = Data.AddSlot("avatarRoot").AttachComponent<ReferenceField<Slot>>();
                _statueSystemFallback = Data.AddSlot("statueSystemFallback").AttachComponent<ReferenceField<Slot>>();
                _baseStatueMaterial = Data.AddSlot("baseMaterial").AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                _statueType = Data.AddSlot("statueType").AttachComponent<ValueField<int>>();
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

                _advancedMode = UI.HorizontalElementWithLabel("Advanced mode", 0.925f, () => UI.Checkbox(false));
                _advancedMode.State.OnValueChange += (state) =>
                {
                    if (state.Value)
                    {
                        _advancedModeSlot.ActiveSelf = true;
                        _simpleModeSlot.ActiveSelf = false;
                        UI.Canvas.Size.Value = new float2(1600f, 1000f);
                        left.AnchorMax.Value = new float2(0.25f, left.AnchorMax.Value.y);
                        right.AnchorMin.Value = new float2(0.25f, right.AnchorMin.Value.y);

                        RefreshUI();
                    }
                    else
                    {
                        _advancedModeSlot.ActiveSelf = false;
                        _simpleModeSlot.ActiveSelf = true;
                        UI.Canvas.Size.Value = new float2(800f, 1000f);
                        left.AnchorMax.Value = new float2(0.5f, left.AnchorMax.Value.y);
                        right.AnchorMin.Value = new float2(0.5f, right.AnchorMin.Value.y);
                    }
                };

                _skinnedMeshRenderersOnly = UI.HorizontalElementWithLabel("Skinned Meshes only", 0.925f, () => UI.Checkbox(true));

                UI.Text("Avatar root slot:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Avatar root slot");
                var avatarField = UI.Current.AttachComponent<RefEditor>();
                avatarField.Setup(_avatarRoot.Reference);
                _avatarRoot.Reference.OnValueChange += _ => OnValuesChanged();

                UI.Text("Default statue material:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Default statue material");
                UI.Current.AttachComponent<RefEditor>().Setup(_baseStatueMaterial.Reference);
                _baseStatueMaterial.Reference.OnValueChange += _ => OnValuesChanged();

                _defaultMaterialAsIs = UI.HorizontalElementWithLabel("Use default material as-is", 0.925f, () => UI.Checkbox(false));
                _defaultMaterialAsIs.State.OnValueChange += _ => OnValuesChanged();

                UI.Text("Default transition type:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Default transition type");
                var editor = new StatueTypeEditor();
                editor.Setup(UI.Current, _statueType.Value);
                _statueType.Value.OnValueChange += _ => OnValuesChanged();

                UI.Spacer(24f);
                _confirmButton = UI.Button("Install");
                _confirmButton.LocalPressed += OnInstallButtonPressed;

                UI.Text("(Optional, Advanced) Override system:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Override system");
                UI.Current.AttachComponent<RefEditor>().Setup(_statueSystemFallback.Reference);

                UI.Spacer(24f);

                UI.NestInto(bottom);

                UI.SplitVertically(0.06f, out RectTransform logHeader, out RectTransform logContent);

                UI.NestInto(logHeader);
                var logTitle = UI.Text("Log");
                logTitle.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                logTitle.VerticalAlign.Value = TextVerticalAlignment.Top;

                UI.NestInto(logContent);
                UI.ScrollArea();
                UI.FitContent(SizeFit.Disabled, SizeFit.MinSize);
                UI.VerticalLayout();

                _debugText = UI.Text("", false);
                _debugText.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                _debugText.VerticalAlign.Value = TextVerticalAlignment.Top;
                _debugText.Size.Value = 10f;
                _debugText.VerticalAutoSize.Value = false;
                _debugText.HorizontalAutoSize.Value = false;
                _debugText.Slot.RemoveComponent(_debugText.Slot.GetComponent<LayoutElement>());

                UI.NestInto(right);

                _advancedModeSlot = UI.Next("Advanced Mode");
                UI.Nest();

                UI.SplitVertically(0.03f, out RectTransform listHeader, out RectTransform listContent);

                UI.NestInto(listHeader);
                var listTitle = UI.Text("Found Mesh Renderers", true, new Alignment?(), true, null);
                listTitle.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                listTitle.VerticalAlign.Value = TextVerticalAlignment.Top;
                UI.NestOut();

                UI.NestInto(listContent);
                UI.ScrollArea();
                UI.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                VerticalLayout verticalLayout2 = UI.VerticalLayout(4f, childAlignment: Alignment.TopLeft);
                verticalLayout2.ForceExpandHeight.Value = true;
                UI.NestOut();

                _listPanel = UI.Current;
                UI.NestOut();

                UI.NestOut();

                _simpleModeSlot = UI.Next("Simple Mode");
                UI.Nest();
                UI.ScrollArea();
                UI.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                SyncMemberEditorBuilder.Build(_foundMeshRenderers.References, "Mesh Renderers found", null, UI);
                UI.Current.Children.Last().Destroy();   // Remove the Add button
                _foundMeshRenderers.References.ElementsRemoving += (list, startIndex, count) =>
                {
                    if (count == 1 && !_refreshingList)
                        _avatar.RemoveMeshRenderer(list[startIndex].Value);
                };
                UI.NestOut();

                _advancedModeSlot.ActiveSelf = false;

                _wizardSlot.PositionInFrontOfUser(float3.Backward, distance: 3f);
            }
            catch (Exception ex)
            {
                Log.Error($"Exception while generating UI: {ex.ToString().ToUixLineEndings()}");
            }
        }

        #endregion

        #region Public Methods

        public void LogDebug(string logMessage)
        {
            if (_debugText != null)
                _debugText.Content.Value = $"<color=gray>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogError(string logMessage)
        {
            if (_debugText != null)
                _debugText.Content.Value = $"<color=red>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogInfo(string logMessage)
        {
            if (_debugText != null)
                _debugText.Content.Value = $"<color=#e0e0e0>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogSuccess(string logMessage)
        {
            if (_debugText != null)
                _debugText.Content.Value = $"<color=green>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        public void LogWarn(string logMessage)
        {
            if (_debugText != null)
                _debugText.Content.Value = $"<color=yellow>{DateTime.Now.ToString("HH:mm:ss.fffffff")}: {logMessage}<br>{_debugText?.Content?.Value ?? ""}";
        }

        #endregion

        #region Private Fields

        private readonly Checkbox _advancedMode;
        private readonly Slot _advancedModeSlot;
        private readonly Avatar _avatar;
        private readonly ReferenceField<Slot> _avatarRoot;
        private readonly ReferenceField<IAssetProvider<Material>> _baseStatueMaterial;
        private readonly Button _confirmButton;
        private readonly Text _debugText;
        private readonly Checkbox _defaultMaterialAsIs;
        private readonly ReferenceMultiplexer<MeshRenderer> _foundMeshRenderers;
        private readonly Func<Slot, SyncRef<Slot>, bool> _installSystemOnAvatar;
        private readonly Slot _listPanel;
        private readonly Slot _simpleModeSlot;
        private readonly Checkbox _skinnedMeshRenderersOnly;
        private readonly ReferenceField<Slot> _statueSystemFallback;
        private readonly ValueField<int> _statueType;
        private readonly Slot _wizardSlot;
        private bool _refreshingList;

        #endregion

        #region Private Methods

        private void OnInstallButtonPressed(IButton button, ButtonEventData eventData)
        {
            var scratchSpace = _wizardSlot.AddSlot("Scratch space");
            try
            {
                var result = _installSystemOnAvatar(scratchSpace, _statueSystemFallback.Reference);
                HighlightHelper.FlashHighlight(_avatarRoot.Reference.Target, (_a) => true, result ? new colorX(0.5f, 0.5f, 0.5f, 1.0f) : new colorX(1.0f, 0.0f, 0.0f, 1.0f));
            }
            catch (Exception ex)
            {
                var errorString = $"Exception while installing: {ex.ToString().ToUixLineEndings()}";
                LogError(errorString);
                LogError("Sorry! We ran into an error installing the statue system.<br>Debugging information has been copied to your clipboard; please send it to the Statue devs!<br>(Arion, Azavit, Nermerner, Uruloke)");
                Engine.Current.InputInterface.Clipboard.SetText(_debugText.Content.Value.ToNormalLineEndings());
            }
            finally
            {
                scratchSpace.Destroy();
            }
        }

        private void OnValuesChanged()
        {
            try
            {
                _avatar.ReadAvatarRoot(_avatarRoot.Reference.Target, _baseStatueMaterial.Reference.Target, _skinnedMeshRenderersOnly.State.Value, _defaultMaterialAsIs.State.Value, (StatueType)_statueType.Value.Value);

                RefreshUI();
            }
            catch (Exception ex)
            {
                Log.Error($"Exception encountered reading avatar root: {ex.ToString().ToUixLineEndings()}");
            }
        }

        private void RefreshUI()
        {
            try
            {
                _refreshingList = true;
                _foundMeshRenderers.References.Clear();
                _foundMeshRenderers.References.AddRange(_avatar.MeshRenderers.Where(x => x.NormalMeshRenderer != null).Select(x => x.NormalMeshRenderer));
                _refreshingList = false;

                _listPanel.DestroyChildren();

                var UI = new UIBuilder(_listPanel);
                RadiantUI_Constants.SetupEditorStyle(UI);

                var first = true;
                for (int i = 0; i < _avatar.MeshRenderers.Count; i++)
                {
                    MeshRendererMap meshRendererMap = _avatar.MeshRenderers[i];
                    if (meshRendererMap.NormalMeshRenderer == null)
                        continue;

                    UI.Spacer(32f);
                    if (!first)
                    {
                        UI.Nest();
                        var separator = UI.Image(new colorX(0.5f));
                        separator.FillRect.Value = new Rect(0.1f, 0, 0.8f, 1);
                        UI.CurrentRect.OffsetMin.Value = new float2(0, 15);
                        UI.CurrentRect.OffsetMax.Value = new float2(0, -15);
                        UI.NestOut();
                    }
                    first = false;

                    UI.PushStyle();
                    UI.Style.MinHeight = 24f;
                    UI.Next($"Mesh Renderer {meshRendererMap.NormalMeshRenderer.ReferenceID}");
                    UI.Nest();
                    UI.PopStyle();

                    UI.SplitHorizontally(0.20f, out var left, out var right);

                    UI.NestInto(left);
                    var title = UI.Text("Mesh Renderer");
                    title.Size.Value = 20;
                    title.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                    UI.NestOut();

                    UI.NestInto(right);
                    UI.Next("Mesh Renderer");
                    var meshRenderer = UI.Current.AttachComponent<ReferenceField<MeshRenderer>>();
                    meshRenderer.Reference.Value = meshRendererMap.NormalMeshRenderer.ReferenceID;
                    UI.Current.AttachComponent<RefEditor>().Setup(meshRenderer.Reference);
                    meshRenderer.Reference.OnValueChange += _ => meshRenderer.Reference.Value = meshRendererMap.NormalMeshRenderer.ReferenceID;

                    var slot = UI.Current.Children.First();     // Get the Horizontal Layout slot
                    slot.Children.Last().Destroy();                 // Destroy the clear button for the RefEditor

                    UI.NestInto(slot);
                    UI.PushStyle();
                    UI.Style.FlexibleWidth = 0f;
                    UI.Style.MinWidth = 24f;
                    var removeButton = UI.Button("X");
                    UI.PopStyle();
                    removeButton.LocalPressed += (x, y) =>
                    {
                        _avatar.RemoveMeshRenderer(meshRendererMap);
                        RefreshUI();
                    };
                    UI.NestOut();
                    UI.NestOut();

                    UI.NestOut();

                    for (int set = 0; set < meshRendererMap.MaterialSets.Count; set++)
                    {
                        if (meshRendererMap.MaterialSets.Count > 1)
                        {
                            UI.PushStyle();
                            UI.Style.MinHeight = 24f;
                            UI.Next($"Material Set {set}");
                            UI.Nest();
                            var materialSetTitle = UI.Text($"Material Set {set}");
                            materialSetTitle.Size.Value = 20;
                            materialSetTitle.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                            UI.NestOut();
                            UI.PopStyle();
                        }

                        UI.PushStyle();
                        UI.Style.MinHeight = 24f;
                        UI.Next("Materials Header");
                        UI.Nest();
                        UI.PopStyle();

                        var headerCols = UI.SplitHorizontally(0.03f, 0.35f, 0.35f, 0.20f, 0.07f);

                        UI.NestInto(headerCols[1]);
                        headerCols[1].OffsetMax.Value = new float2(-10, 0);
                        var text = UI.Text("Normal material");
                        text.Size.Value = 20;
                        text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                        UI.NestOut();

                        UI.NestInto(headerCols[2]);
                        headerCols[2].OffsetMin.Value = new float2(10, 0);
                        headerCols[2].OffsetMax.Value = new float2(-10, 0);
                        text = UI.Text("Statue material");
                        text.Size.Value = 20;
                        text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                        UI.NestOut();

                        UI.NestInto(headerCols[3]);
                        headerCols[3].OffsetMin.Value = new float2(10, 0);
                        headerCols[3].OffsetMax.Value = new float2(-10, 0);
                        text = UI.Text("Transition type");
                        text.Size.Value = 20;
                        text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                        UI.NestOut();

                        UI.NestInto(headerCols[4]);
                        headerCols[3].OffsetMin.Value = new float2(10, 0);
                        text = UI.Text("Use as-is");
                        text.Size.Value = 20;
                        text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                        UI.NestOut();

                        UI.NestOut();

                        for (int materialSlot = 0; materialSlot < meshRendererMap.MaterialSets[set].Count; materialSlot++)
                        {
                            MaterialMap materialMap = meshRendererMap.MaterialSets[set][materialSlot];

                            UI.PushStyle();
                            UI.Style.MinHeight = 24f;
                            UI.Next($"Material slot {materialSlot}: {materialMap.Normal.ReferenceID}");
                            UI.Nest();
                            UI.PopStyle();

                            var materialCols = UI.SplitHorizontally(0.03f, 0.35f, 0.35f, 0.20f, 0.07f);

                            UI.NestInto(materialCols[0]);
                            var indexLabel = UI.Text($"{materialSlot}:");
                            indexLabel.Size.Value = 20;
                            indexLabel.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                            UI.NestOut();

                            UI.NestInto(materialCols[1]);
                            UI.Next("Normal material");
                            UI.CurrentRect.OffsetMax.Value = new float2(-10, 0);
                            var normalMaterial = UI.Current.AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                            normalMaterial.Reference.Value = materialMap.Normal.ReferenceID;
                            UI.Current.AttachComponent<RefEditor>().Setup(normalMaterial.Reference);
                            normalMaterial.Reference.OnValueChange += _ => normalMaterial.Reference.Value = materialMap.Normal.ReferenceID;

                            slot = UI.Current.Children.First();     // Get the Horizontal Layout slot
                            slot.Children.Last().Destroy();         // Destroy the clear button for the RefEditor

                            UI.NestOut();

                            UI.NestInto(materialCols[2]);
                            UI.Next("Statue material");
                            UI.CurrentRect.OffsetMin.Value = new float2(10, 0);
                            UI.CurrentRect.OffsetMax.Value = new float2(-10, 0);
                            var statueMaterial = UI.Current.AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                            statueMaterial.Reference.Value = materialMap.Statue.ReferenceID;
                            UI.Current.AttachComponent<RefEditor>().Setup(statueMaterial.Reference);
                            statueMaterial.Reference.OnValueChange += _ => materialMap.Statue = statueMaterial.Reference.Target;
                            UI.NestOut();

                            UI.NestInto(materialCols[3]);
                            UI.Next("Transition type");
                            UI.CurrentRect.OffsetMin.Value = new float2(10, 0);
                            UI.CurrentRect.OffsetMax.Value = new float2(-10, 0);
                            var transitionType = UI.Current.AttachComponent<ValueField<int>>();
                            transitionType.Value.Value = (int)materialMap.TransitionType;
                            var editor = new StatueTypeEditor();
                            editor.Setup(UI.Current, transitionType.Value);
                            transitionType.Value.OnValueChange += _ => materialMap.TransitionType = (StatueType)transitionType.Value.Value;
                            UI.NestOut();

                            UI.NestInto(materialCols[4]);
                            UI.Next("Use as-is");
                            UI.CurrentRect.OffsetMin.Value = new float2(10, 0);
                            UI.Nest();
                            var asIs = UI.Checkbox();
                            asIs.State.Value = materialMap.UseAsIs;
                            asIs.State.OnValueChange += _ => materialMap.UseAsIs = asIs.State;
                            UI.NestOut();
                            UI.NestOut();

                            UI.NestOut();
                        }
                    }
                }

                if (_avatar.HasExistingSystem)
                    _confirmButton.LabelText = "Update";
                else if (_avatar.HasLegacySystem)
                    _confirmButton.LabelText = "Update from legacy";
                else
                    _confirmButton.LabelText = "Install";
            }
            catch (Exception ex)
            {
                Log.Error($"Exception while generating UI: {ex.ToString().ToUixLineEndings()}");
            }
        }

        #endregion
    }
}
