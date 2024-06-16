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

        public WizardUi(Slot slot, string title, Avatar avatarReader, Func<Slot, Slot, SyncRef<Slot>, SyncRef<Slot>, bool> onInstall)
        {
            try
            {
                _wizardSlot = slot;
                _avatar = avatarReader;
                _installSystemOnAvatar = onInstall;

                // Initialize cloud spawn
                var statueSystemLoadSlot = slot.AddSlot("Statue System Loader");
                var statueSystemCloudURIVariable = statueSystemLoadSlot.AttachComponent<CloudValueVariable<string>>();
                statueSystemCloudURIVariable.Path.Value = "U-Azavit.Statue.Stable.AssetURI";
                statueSystemCloudURIVariable.VariableOwnerId.Value = "U-Azavit";
                statueSystemCloudURIVariable.ChangeHandling.Value = CloudVariableChangeMode.Ignore;
                statueSystemCloudURIVariable.IsLinkedToCloud.Value = true;
                _uriVariable = statueSystemCloudURIVariable;

                var Data = _wizardSlot.AddSlot("Data");

                _avatarRoot = Data.AddSlot("avatarRoot").AttachComponent<ReferenceField<Slot>>();
                _statueSystemFallback = Data.AddSlot("statueSystemFallback").AttachComponent<ReferenceField<Slot>>();
                _baseStatueMaterial = Data.AddSlot("baseMaterial").AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                _statueType = Data.AddSlot("statueType").AttachComponent<ValueField<int>>();
                _foundMeshRenderers = Data.AddSlot("foundMRs").AttachComponent<ReferenceMultiplexer<MeshRenderer>>();
                _contextMenuSlot = Data.AddSlot("contextMenuSlot").AttachComponent<ReferenceField<Slot>>();
                _installSlot = Data.AddSlot("installSlot").AttachComponent<ReferenceField<Slot>>();

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

                var UI = RadiantUI_Panel.SetupPanel(slot, title, new float2(1000f, 1000f));
                RadiantUI_Constants.SetupEditorStyle(UI);

                UI.Canvas.MarkDeveloper();
                UI.Canvas.AcceptPhysicalTouch.Value = false;

                var columns = UI.SplitHorizontally(0.2f, 0.4f, 0.4f);

                var help = columns[0];
                var left = columns[1];
                var right = columns[2];

                help.OffsetMax.Value = new float2(-10f);
                left.OffsetMin.Value = new float2(10f);
                left.OffsetMax.Value = new float2(-10f);
                right.OffsetMin.Value = new float2(10f);

                UI.NestInto(help);

                UI.SplitVertically(0.03f, out RectTransform helpHeader, out RectTransform helpContent);

                UI.NestInto(helpHeader);
                var helpTitle = UI.Text("Help");
                helpTitle.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                helpTitle.VerticalAlign.Value = TextVerticalAlignment.Top;

                UI.NestInto(helpContent);
                UI.ScrollArea();
                UI.FitContent(SizeFit.Disabled, SizeFit.MinSize);
                UI.VerticalLayout();

                var helpText = UI.Text("", false);
                helpText.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                helpText.VerticalAlign.Value = TextVerticalAlignment.Top;
                helpText.Size.Value = 10f;
                helpText.VerticalAutoSize.Value = false;
                helpText.HorizontalAutoSize.Value = false;
                helpText.Slot.RemoveComponent(helpText.Slot.GetComponent<LayoutElement>());
                helpText.Content.Value = _helpText;

                UI.NestOut();

                UI.NestInto(left);

                UI.SplitVertically(0.65f, out RectTransform top, out RectTransform bottom);

                UI.NestInto(top);

                UI.Style.MinHeight = 24f;
                UI.Style.PreferredHeight = 24f;
                UI.Style.PreferredWidth = 400f;
                UI.Style.MinWidth = 400f;

                VerticalLayout verticalLayout = UI.VerticalLayout(4f, childAlignment: Alignment.TopLeft);
                verticalLayout.ForceExpandHeight.Value = true;

                var advancedMode = UI.HorizontalElementWithLabel("Advanced mode", 0.925f, () => UI.Checkbox(false));
                advancedMode.State.OnValueChange += (state) =>
                {
                    if (state.Value)
                    {
                        _advancedModeSlot!.ActiveSelf = true;
                        _simpleModeSlot!.ActiveSelf = false;
                        UI.Canvas.Size.Value = new float2(1900f, 1000f);
                        help.AnchorMax.Value = new float2(0.11f, help.AnchorMax.Value.y);
                        left.AnchorMin.Value = new float2(0.11f, left.AnchorMin.Value.y);
                        left.AnchorMax.Value = new float2(0.31f, left.AnchorMax.Value.y);
                        right.AnchorMin.Value = new float2(0.31f, right.AnchorMin.Value.y);

                        RefreshUI();
                    }
                    else
                    {
                        _advancedModeSlot!.ActiveSelf = false;
                        _simpleModeSlot!.ActiveSelf = true;
                        UI.Canvas.Size.Value = new float2(1000f, 1000f);
                        help.AnchorMax.Value = new float2(0.2f, help.AnchorMax.Value.y);
                        left.AnchorMin.Value = new float2(0.2f, left.AnchorMin.Value.y);
                        left.AnchorMax.Value = new float2(0.6f, left.AnchorMax.Value.y);
                        right.AnchorMin.Value = new float2(0.6f, right.AnchorMin.Value.y);
                    }
                };

                _skinnedMeshRenderersOnly = UI.HorizontalElementWithLabel("Skinned Meshes only", 0.925f, () => UI.Checkbox(true));
                _skinnedMeshRenderersOnly.State.OnValueChange += _ => OnValuesChanged(true);

                UI.Text("Avatar root slot:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Avatar root slot");
                var avatarField = UI.Current.AttachComponent<RefEditor>();
                avatarField.Setup(_avatarRoot.Reference);
                _avatarRoot.Reference.OnValueChange += _ => OnValuesChanged(true);

                UI.Text("Default statue material:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Default statue material");
                UI.Current.AttachComponent<RefEditor>().Setup(_baseStatueMaterial.Reference);
                _baseStatueMaterial.Reference.OnValueChange += _ => OnValuesChanged(false);

                _defaultMaterialAsIs = UI.HorizontalElementWithLabel("Use default material as-is", 0.925f, () => UI.Checkbox(false));
                _defaultMaterialAsIs.State.OnValueChange += _ => OnValuesChanged(false);

                UI.Text("Default transition type:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Default transition type");
                var editor = new StatueTypeEditor();
                editor.Setup(UI.Current, _statueType.Value);
                _statueType.Value.OnValueChange += _ => OnValuesChanged(false);

                UI.Spacer(12f);

                _removeNewButton = UI.Button("Remove new MeshRenderers");
                _removeNewButton.LocalPressed += (_, __) => OnValuesChanged(false, true);
                _removeNewButton.Enabled = false;

                _refreshButton = UI.Button("Refresh");
                _refreshButton.LocalPressed += (_, __) => OnValuesChanged(true);
                _refreshButton.Enabled = false;

                _confirmButton = UI.Button("Install");
                _confirmButton.LocalPressed += OnInstallButtonPressed;
                _confirmButton.Enabled = false;

                UI.Spacer(12f);

                UI.Text("Advanced (Optional)").HorizontalAlign.Value = TextHorizontalAlignment.Left;

                UI.Spacer(6f);

                UI.Text("Override system:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Override system");
                UI.Current.AttachComponent<RefEditor>().Setup(_statueSystemFallback.Reference);

                UI.Text("Installation slot:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Installation slot");
                UI.Current.AttachComponent<RefEditor>().Setup(_installSlot.Reference);

                UI.Text("Context menu slot:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
                UI.Next("Context menu slot");
                UI.Current.AttachComponent<RefEditor>().Setup(_contextMenuSlot.Reference);

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

                _wizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
            }
            catch (Exception ex)
            {
                Log.Error($"Exception while generating UI: {ex.ToString().ToUixLineEndings()}");
            }
        }

        #endregion

        #region Public Methods

        public void ClearLog()
        {
            if (_debugText is not null)
                _debugText.Content.Value = string.Empty;
        }

        public void LogDebug(string logMessage)
        {
            if (_debugText is not null)
                _debugText.Content.Value = $"<color=gray>{DateTime.Now:HH:mm:ss.fffffff}: {logMessage}<br>{_debugText.Content?.Value ?? ""}";
        }

        public void LogError(string logMessage)
        {
            if (_debugText is not null)
                _debugText.Content.Value = $"<color=red>{DateTime.Now:HH:mm:ss.fffffff}: {logMessage}<br>{_debugText.Content?.Value ?? ""}";
        }

        public void LogInfo(string logMessage)
        {
            if (_debugText is not null)
                _debugText.Content.Value = $"<color=#e0e0e0>{DateTime.Now:HH:mm:ss.fffffff}: {logMessage}<br>{_debugText.Content?.Value ?? ""}";
        }

        public void LogSuccess(string logMessage)
        {
            if (_debugText is not null)
                _debugText.Content.Value = $"<color=green>{DateTime.Now:HH:mm:ss.fffffff}: {logMessage}<br>{_debugText.Content?.Value ?? ""}";
        }

        public void LogWarn(string logMessage)
        {
            if (_debugText is not null)
                _debugText.Content.Value = $"<color=yellow>{DateTime.Now:HH:mm:ss.fffffff}: {logMessage}<br>{_debugText.Content?.Value ?? ""}";
        }

        #endregion

        #region Private Fields

        private readonly Slot? _advancedModeSlot;
        private readonly Avatar? _avatar;
        private readonly ReferenceField<Slot>? _avatarRoot;
        private readonly ReferenceField<IAssetProvider<Material>>? _baseStatueMaterial;
        private readonly Button? _confirmButton;
        private readonly ReferenceField<Slot>? _contextMenuSlot;
        private readonly Text? _debugText;
        private readonly Checkbox? _defaultMaterialAsIs;
        private readonly ReferenceMultiplexer<MeshRenderer>? _foundMeshRenderers;

        private readonly string _helpText = """
This mod installs and updates the Statue Remaster system on an avatar.

Advanced Mode:
Advanced mode allows more fine grained control of what materials and options to use where.

Skinned Meshes only:
Check this option to only look for SkinnedMeshRenderers. If you want to include regular MeshRenderers typically used for procedural meshes like BoxMesh etc uncheck this option.

Avatar root:
The root slot of the avatar to install to. The mod will try to find any existing legacy or remaster installations. Legacy installations can be updated to remaster. Additionally it will try to match the normal MeshRenderers with any existing statue MeshRenderers. If a MeshRenderer appears to show up twice it's likely it hasn't found a match. You can workaround this by adding "Statue" to the name of the slot.

Default statue material:
The material to use as the default statue material. It can be left null if you want to use the material installed previously. Additional materials can be added later using Advanced Mode.

Use default material as-is:
Normally the mod will merge the statue materials with your normal avatar materials' normal maps. This can preserve avatar details. Use this option to use the statue materials without modifying them. This can be changed per material slot using Advanced Mode.

Default transition type:
The transition type to use for when the avatar is turned into a statue. AlphaCutout requires special alpha textures. PlaneSlicer and RadialSlicer require PBS_Metallic or PBS_Specular materials. This can be changed per material slot using Advanced Mode.

Remove new MeshRenderers:
Removes all MeshRenderers from installation that have not previously been setup by the mod. Useful when tweaking settings on an already set up avatar.

Refresh:
Reads the avatar root again and refreshes the MeshRenderers. Any individual changes made in Advanced Mode will be lost.

Install:
Performs a fresh install of the Remaster system on the avatar.

Update:
Updates the installation of the Remaster system on the avatar with new materials, meshes and installation options.

Update from legacy:
Updates the legacy installation to a Remaster installation on the avatar.
""";

        private readonly ReferenceField<Slot>? _installSlot;
        private readonly Func<Slot, Slot, SyncRef<Slot>, SyncRef<Slot>, bool>? _installSystemOnAvatar;
        private readonly Slot? _listPanel;
        private readonly Button? _refreshButton;
        private readonly Button? _removeNewButton;
        private readonly Slot? _simpleModeSlot;
        private readonly Checkbox? _skinnedMeshRenderersOnly;
        private readonly ReferenceField<Slot>? _statueSystemFallback;
        private readonly ValueField<int>? _statueType;
        private readonly CloudValueVariable<string>? _uriVariable;
        private readonly Slot? _wizardSlot;
        private bool _refreshingList;

        #endregion

        #region Private Methods

        private void OnInstallButtonPressed(IButton button, ButtonEventData eventData)
        {
            var scratchSpace = _wizardSlot!.AddSlot("Scratch space");
            try
            {
                var systemSlot = CloudSpawn.GetStatueSystem(scratchSpace, _uriVariable!, _statueSystemFallback!.Reference);
                if (systemSlot is null)
                    return;

                var result = _installSystemOnAvatar!(scratchSpace, systemSlot, _installSlot!.Reference, _contextMenuSlot!.Reference);

                HighlightHelper.FlashHighlight(_avatarRoot!.Reference.Target, (_) => true, result ? new colorX(0.5f, 0.5f, 0.5f, 1.0f) : new colorX(1.0f, 0.0f, 0.0f, 1.0f));
                _confirmButton!.Enabled = false;
            }
            catch (Exception ex)
            {
                var errorString = $"Exception while installing: {ex.ToString().ToUixLineEndings()}";
                LogError(errorString);
                LogError("Sorry! We ran into an error installing the statue system.<br>Debugging information has been copied to your clipboard; please send it to the Statue devs!<br>(Arion, Azavit, Nermerner, Uruloke)");
                Engine.Current.InputInterface.Clipboard.SetText(_debugText!.Content.Value.ToNormalLineEndings());
            }
            finally
            {
                scratchSpace.Destroy();
            }
        }

        private void OnValuesChanged(bool readAvatar, bool removeNew = false)
        {
            try
            {
                if (readAvatar)
                    _avatar!.ReadAvatarRoot(_avatarRoot!.Reference.Target, _baseStatueMaterial!.Reference.Target, _skinnedMeshRenderersOnly!.State.Value, _defaultMaterialAsIs!.State.Value, (StatueType)_statueType!.Value.Value);
                else
                    _avatar!.UpdateParameters(_baseStatueMaterial!.Reference.Target, _defaultMaterialAsIs!.State.Value, (StatueType)_statueType!.Value.Value);

                if (removeNew)
                    _avatar.RemoveUnmatchedMeshRenderers();

                RefreshUI();
            }
            catch (Exception ex)
            {
                Log.Error($"Exception encountered reading avatar root: {ex.ToString().ToUixLineEndings()}");
            }
        }

        private void RefreshUI()
        {
            if (_avatar is null)
                return;

            try
            {
                _refreshingList = true;
                _foundMeshRenderers!.References.Clear();
                _foundMeshRenderers.References.AddRange(_avatar.MeshRenderers.Where(x => x.NormalMeshRenderer is not null).Select(x => x.NormalMeshRenderer!));
                _refreshingList = false;

                _listPanel!.DestroyChildren();

                var UI = new UIBuilder(_listPanel);
                RadiantUI_Constants.SetupEditorStyle(UI);

                var first = true;
                for (int i = 0; i < _avatar.MeshRenderers.Count; i++)
                {
                    MeshRendererMap meshRendererMap = _avatar.MeshRenderers[i];
                    if (meshRendererMap.NormalMeshRenderer is null)
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
                    removeButton.LocalPressed += (_, __) =>
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

                        var headerCols = UI.SplitHorizontally(0.03f, 0.35f, 0.35f, 0.20f, 0.07f, 0.07f);

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
                        headerCols[4].OffsetMin.Value = new float2(10, 0);
                        headerCols[4].OffsetMax.Value = new float2(-10, 0);
                        text = UI.Text("Use as-is");
                        text.Size.Value = 20;
                        text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                        UI.NestOut();

                        UI.NestInto(headerCols[5]);
                        headerCols[5].OffsetMin.Value = new float2(10, 0);
                        text = UI.Text("Clothes");
                        text.Size.Value = 20;
                        text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                        UI.NestOut();

                        UI.NestOut();

                        for (int materialSlot = 0; materialSlot < meshRendererMap.MaterialSets[set].Count; materialSlot++)
                        {
                            MaterialMap materialMap = meshRendererMap.MaterialSets[set][materialSlot];

                            UI.PushStyle();
                            UI.Style.MinHeight = 24f;
                            UI.Next($"Material slot {materialSlot}: {materialMap.Normal?.ReferenceID ?? RefID.Null}");
                            UI.Nest();
                            UI.PopStyle();

                            var materialCols = UI.SplitHorizontally(0.03f, 0.35f, 0.35f, 0.20f, 0.07f, 0.07f);

                            UI.NestInto(materialCols[0]);
                            var indexLabel = UI.Text($"{materialSlot}:");
                            indexLabel.Size.Value = 20;
                            indexLabel.HorizontalAlign.Value = TextHorizontalAlignment.Left;
                            UI.NestOut();

                            UI.NestInto(materialCols[1]);
                            UI.Next("Normal material");
                            UI.CurrentRect.OffsetMax.Value = new float2(-10, 0);
                            var normalMaterial = UI.Current.AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                            normalMaterial.Reference.Value = materialMap.Normal?.ReferenceID ?? RefID.Null;
                            UI.Current.AttachComponent<RefEditor>().Setup(normalMaterial.Reference);
                            normalMaterial.Reference.OnValueChange += _ => normalMaterial.Reference.Value = materialMap.Normal?.ReferenceID ?? RefID.Null;

                            slot = UI.Current.Children.First();     // Get the Horizontal Layout slot
                            slot.Children.Last().Destroy();         // Destroy the clear button for the RefEditor

                            UI.NestOut();

                            UI.NestInto(materialCols[2]);
                            UI.Next("Statue material");
                            UI.CurrentRect.OffsetMin.Value = new float2(10, 0);
                            UI.CurrentRect.OffsetMax.Value = new float2(-10, 0);
                            var statueMaterial = UI.Current.AttachComponent<ReferenceField<IAssetProvider<Material>>>();
                            statueMaterial.Reference.Value = materialMap.Statue?.ReferenceID ?? RefID.Null;
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
                            UI.CurrentRect.OffsetMax.Value = new float2(-10, 0);
                            UI.Nest();
                            var asIs = UI.Checkbox();
                            asIs.State.Value = materialMap.UseAsIs;
                            asIs.State.OnValueChange += _ => materialMap.UseAsIs = asIs.State;
                            UI.NestOut();
                            UI.NestOut();

                            UI.NestInto(materialCols[5]);
                            UI.Next("Clothes");
                            UI.CurrentRect.OffsetMin.Value = new float2(10, 0);
                            UI.Nest();
                            var clothes = UI.Checkbox();
                            clothes.State.Value = materialMap.Clothes;
                            clothes.State.OnValueChange += _ => materialMap.Clothes = clothes.State;
                            UI.NestOut();
                            UI.NestOut();

                            UI.NestOut();
                        }
                    }
                }

                if (_avatar.HasExistingSystem)
                {
                    _confirmButton!.LabelText = "Update";
                    _removeNewButton!.Enabled = true;
                }
                else if (_avatar.HasLegacySystem)
                {
                    _confirmButton!.LabelText = "Update from legacy";
                    _removeNewButton!.Enabled = true;
                }
                else
                {
                    _confirmButton!.LabelText = "Install";
                    _removeNewButton!.Enabled = false;
                }

                _refreshButton!.Enabled = true;
                _confirmButton.Enabled = true;
            }
            catch (Exception ex)
            {
                Log.Error($"Exception while generating UI: {ex.ToString().ToUixLineEndings()}");
            }
        }

        #endregion
    }
}
