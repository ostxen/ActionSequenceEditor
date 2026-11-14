using NBC.ActionEditor.Views.IMGUI;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Views.UIToolkit
{
    // 原生窗口装配逐窗控件与Presenter，只持有自身工作区和捕获。
    public sealed class ActionEditorWindow : EditorWindow
    {
        public const float HeaderHeight = 40, RowHeight = 28, BottomHeight = 16;
        public float LeftWidth { get => Workspace.LeftWidth; set => Workspace.LeftWidth = value; }
        public float ScrollY { get => Workspace.ScrollY; set => Workspace.ScrollY = value; }
        public TimelineCanvas Canvas { get; private set; }
        private VisualElement transport, toolbar, footer;
        private ToolbarButton documents;
        private VisualElement customHeader;
        private Type headerType;
        private Label lastSave;
        private ToolbarToggle magnet;
        private MinMaxSlider range;
        private Scroller scroll;
        private Label error;
        private UIToolkitInspectorTarget target;
        private readonly List<VisualElement> inspectors = new List<VisualElement>();
        private bool syncing;
        private bool scheduled;
        private double lastTick;
        public string LastError { get; private set; }
        private Composition.WindowBinding binding;
        public ActionEditorContext Context => binding.Context;
        public PlaybackPresenter Playback => binding.Playback;
        public DocumentPresenter Documents => binding.Documents;
        public ActionDocument Document => Context.Document;
        public TimelineWorkspace Workspace { get; } = new TimelineWorkspace();

        // 显式独立宿主与主入口共用控件和SaveChanges，不通过全局会话中转。
        public void BindContext(ActionEditorContext context, ActionEditorViews views, NBC.ActionEditor.Application.Settings.SettingsSession settings,
            Func<bool, string> chooseSavePath = null)
        {
            Canvas?.Dispose(); Canvas = null; binding?.Dispose();
            binding = new Composition.WindowBinding(this, Repaint, Refresh, context, views, settings, () => context.AutoSave.LastSaved, chooseSavePath);
            try { InitializeWindow(); CreateGUI(); }
            catch { OnDisable(); throw; }
        }

        [MenuItem("NBC/Action Editor/UI Toolkit", false, -10)]
        public static void Open() => GetWindow<ActionEditorWindow>().Show();
        private void OnEnable()
        {
            binding?.Dispose(); binding = new Composition.WindowBinding(this, Repaint, Refresh);
            try { InitializeWindow(); }
            catch { OnDisable(); throw; }
        }
        // 创建Inspector代理失败时仍释放本窗口已经取得的绑定。
        private void InitializeWindow()
        {
            Lan.Load();
            Styles.Load();
            LeftWidth = Styles.TimelineLeftWidth;
            titleContent = new GUIContent(Lan.Title + " · UI Toolkit"); minSize = new Vector2(500, 250);
            if (target != null) DestroyImmediate(target);
            target = CreateInstance<UIToolkitInspectorTarget>(); target.hideFlags = HideFlags.HideAndDontSave; target.Owner = this;
            binding.SetTick(Tick);
            lastTick = EditorApplication.timeSinceStartup;
        }
        private void OnDisable()
        {
            var canvas = Canvas; Canvas = null;
            try { canvas?.Dispose(); }
            finally
            {
                try { binding?.Dispose(); }
                finally { if (target != null) DestroyImmediate(target); target = null; }
            }
        }
        private void OnLostFocus() => Canvas?.Cancel();
        private void Tick()
        {
            if (EditorApplication.timeSinceStartup - lastTick < 0.03) return;
            var delta = EditorApplication.timeSinceStartup - lastTick;
            lastTick = EditorApplication.timeSinceStartup;
            Canvas?.Tick(delta);
            Canvas?.Presenter.PlaybackState(Playback.Playing, Playback.Range);
            if (Document != null) SyncView();
        }
        public void CreateGUI()
        {
            titleContent = new GUIContent(Lan.Title + " · UI Toolkit");
            Canvas?.Dispose();
            rootVisualElement.Clear();
            headerType = null;
            rootVisualElement.style.backgroundColor = new Color(.2f, .2f, .2f);
            var head = new VisualElement { name = "timeline-header" }; head.style.height = HeaderHeight; head.style.flexShrink = 0; head.style.flexDirection = FlexDirection.Row;
            transport = new VisualElement { name = "timeline-transport" }; transport.style.flexDirection = FlexDirection.Row; transport.style.flexShrink = 0;
            void Control(string text, string hint, Action click, Texture2D icon)
            {
                var button = new ToolbarButton(() => Run(click)) { text = text, tooltip = hint };
                button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.marginLeft = 0; button.style.marginRight = 0;
                button.style.height = 40;
                if (icon != null)
                {
                    button.text = ""; var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    image.style.width = Length.Percent(100); image.style.height = Length.Percent(100); button.Add(image);
                }
                transport.Add(button);
            }
            Control("‹", "返回资源选择", () => Documents.Invoke(DocumentIntent.Close), Styles.BackIcon);
            Control("|◀", "开始", () => Playback.Invoke(PlaybackIntent.First), Styles.FirstFrameIcon); Control("◀", "上一帧", () => Playback.Invoke(PlaybackIntent.Previous), Styles.PrevFrameIcon);
            Control("▶/■", "播放/停止", () => Playback.Invoke(PlaybackIntent.TogglePlay), Styles.PlayIcon);
            Control("Ⅱ", "暂停/继续", () => Playback.Invoke(PlaybackIntent.TogglePause), Styles.PauseIcon); Control("▶|", "下一帧", () => Playback.Invoke(PlaybackIntent.Next), Styles.NextFrameIcon);
            Control("▶|", "结束", () => Playback.Invoke(PlaybackIntent.Last), Styles.LastFrameIcon);
            Control("↔", "范围循环", () => Playback.Invoke(PlaybackIntent.ToggleRange), Styles.RangeIcon);
            head.Add(transport);
            var right = new VisualElement(); right.style.flexGrow = 1; right.style.minWidth = 0; right.style.marginLeft = Styles.SplitterWidth;
            toolbar = new Toolbar { name = "timeline-document-toolbar" }; toolbar.style.height = 20;
            documents = new ToolbarButton(() => Run(() => Documents.Invoke(DocumentIntent.Open)))
                { text = "[]", name = "timeline-document-picker", tooltip = Lan.SelectAssetTips };
            documents.AddToClassList("unity-toolbar-menu"); toolbar.Add(documents);
            var dropdownArrow = new Label("▼") { pickingMode = PickingMode.Ignore }; dropdownArrow.style.position = Position.Absolute;
            dropdownArrow.style.right = 4; dropdownArrow.style.top = 3; dropdownArrow.style.fontSize = 8; documents.Add(dropdownArrow);
            customHeader = new VisualElement { name = "timeline-custom-header" }; customHeader.style.flexDirection = FlexDirection.Row; toolbar.Add(customHeader);
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; toolbar.Add(spacer);
            lastSave = new Label { name = "timeline-last-save" }; lastSave.style.fontSize = 11; lastSave.style.color = new Color(1, 1, 1, .3f); lastSave.style.unityTextAlign = TextAnchor.MiddleLeft; toolbar.Add(lastSave);
            ToolbarButton IconButton(string name, Texture2D texture, string hint, Action action)
            {
                var button = new ToolbarButton(() => Run(action)) { name = name, tooltip = hint }; button.style.width = 26;
                button.style.flexShrink = 0; button.Add(new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore }); toolbar.Add(button); return button;
            }
            var saveButton = IconButton("timeline-save", Styles.SaveIcon, Lan.SaveMenuHint, Save);
            saveButton.AddManipulator(new ContextualMenuManipulator(e => binding.PopulateSaveMenu((label, action) => e.menu.AppendAction(label, _ => Run(action)))));
            magnet = new ToolbarToggle { name = "timeline-magnet", value = binding.Preferences.MagnetSnapping, tooltip = Lan.OpenMagnetSnappingTips };
            magnet.style.width = 26; magnet.style.flexShrink = 0; magnet.Add(new Image { image = Styles.MagnetIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
            magnet.RegisterValueChangedCallback(e =>
            {
                binding.Settings.Change(magnet: e.newValue);
                ShowTimelineError(binding.SettingsError);
            }); toolbar.Add(magnet);
            IconButton("timeline-settings", Styles.SettingsIcon, Lan.OpenPreferencesTips, () => PreferencesWindow.ShowFor(this));
            right.Add(toolbar); var rulerSpace = new VisualElement(); rulerSpace.style.height = 20; right.Add(rulerSpace); head.Add(right); rootVisualElement.Add(head);
            var body = new VisualElement(); body.style.flexGrow = 1; body.style.minHeight = 0; body.style.flexDirection = FlexDirection.Row;
            Canvas = new TimelineCanvas(this); body.Add(Canvas);
            Canvas.Bind(binding.CreateTimeline(Workspace, Canvas));
            scroll = new Scroller(0, 0, value => { if (!syncing) Canvas.Presenter.Scroll(value); }, SliderDirection.Vertical);
            scroll.style.width = 14; scroll.style.flexShrink = 0; body.Add(scroll); rootVisualElement.Add(body);
            footer = new VisualElement { name = "timeline-footer" }; footer.style.height = BottomHeight; footer.style.flexShrink = 0; footer.style.flexDirection = FlexDirection.Row;
            var blank = new VisualElement { name = "timeline-footer-blank" }; blank.style.width = LeftWidth + 9; blank.style.flexShrink = 0; footer.Add(blank);
            range = new MinMaxSlider(0, 7, 0, 7) { name = "timeline-view-range" }; range.style.flexGrow = 1;
            range.RegisterValueChangedCallback(e => { if (syncing || Document == null) return; Canvas.Presenter.ViewRange(e.newValue.x, e.newValue.y); });
            range.style.marginLeft = 0; range.style.marginRight = 5; range.style.marginTop = 0; range.style.height = 18;
            range.RegisterCallback<PointerDownEvent>(e => { if (e.clickCount == 2 && Document != null) { Canvas.Presenter.Fit(); SyncView(); } });
            footer.Add(range); rootVisualElement.Add(footer);
            error = new Label { name = "timeline-error" }; error.style.display = DisplayStyle.None; rootVisualElement.Add(error);
            Canvas.AttachRuler(); Canvas.RegisterCallback<GeometryChangedEvent>(_ => SyncView()); Refresh();
        }
        public void Seek(float time) { Playback.Seek(time); Canvas?.MarkDirtyRepaint(); }
        public void CreateDocument() => Documents.Invoke(DocumentIntent.Create);
        public void OpenDocument() => Documents.Invoke(DocumentIntent.Open);
        public void ShowTimelineError(string message)
        {
            LastError = message;
            if (error == null) return;
            error.text = message ?? ""; error.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public void Run(Action action)
        {
            try { action(); LastError = null; if (error != null) error.style.display = DisplayStyle.None; }
            catch (ExitGUIException) { throw; }
            catch (Exception e) { LastError = e.Message; if (error != null) { error.text = e.Message; error.style.display = DisplayStyle.Flex; } else Debug.LogException(e); }
        }
        public void Refresh()
        {
            Workspace.Bind(Document);
            if (Canvas == null || scheduled) return;
            scheduled = true;
            rootVisualElement.schedule.Execute(() =>
            {
                scheduled = false; if (Canvas == null) return;
                hasUnsavedChanges = Documents.Dirty;
                var assetName = binding.DocumentName;
                documents.text = "[" + assetName + "]";
                documents.style.minWidth = 80;
                if (headerType != Document?.GetType())
                {
                    headerType = Document?.GetType(); customHeader.Clear();
                    var adapter = Document == null ? null : EditorExtensionFactory.GetToolkitHeader(Document);
                    var header = adapter?.CreateGUI(binding.Extensions);
                    if (header != null) customHeader.Add(header);
                }
                Canvas.RefreshVisualState(); SyncView();
                foreach (var view in inspectors.ToArray()) BuildProperties(view);
            });
        }
        public void SyncView()
        {
            if (transport == null || Canvas?.State == null || scroll == null || footer == null || range == null || lastSave == null || magnet == null) return;
            transport.parent.style.display = Document == null ? DisplayStyle.None : DisplayStyle.Flex;
            footer.style.display = Document == null ? DisplayStyle.None : DisplayStyle.Flex;
            transport.style.width = LeftWidth;
            Canvas.SyncRuler();
            var playImage = transport.Children().ElementAt(3).Q<Image>(); if (playImage != null) playImage.image = Playback.Playing ? Styles.StopIcon : Styles.PlayIcon;
            foreach (var index in new[] { 3, 4, 7 })
                transport.Children().ElementAt(index).style.backgroundColor = (index == 3 ? Playback.Playing : index == 4 ? Playback.Paused : Playback.Range) ? new Color(.22f, .38f, .55f) : StyleKeyword.Null;
            lastSave.text = string.Format(Lan.HeaderLastSaveTime, binding.LastSave.ToString("HH:mm:ss"));
            magnet.SetValueWithoutNotify(binding.Preferences.MagnetSnapping);
            footer.Q("timeline-footer-blank").style.width = LeftWidth + 9;
            syncing = true;
            try
            {
                scroll.highValue = Mathf.Max(0, Canvas.State.Rows.Count * RowHeight + 32 - Canvas.contentRect.height);
                scroll.style.display = Document != null && scroll.highValue > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                scroll.Adjust(Canvas.contentRect.height / Mathf.Max(1, Canvas.State.Rows.Count * RowHeight + 32));
                ScrollY = Mathf.Clamp(ScrollY, 0, scroll.highValue); scroll.value = ScrollY;
                Canvas.SyncAddGroup();
                if (Document != null)
                {
                    range.highLimit = Mathf.Max(.001f, Document.Length);
                    range.SetValueWithoutNotify(new Vector2(Workspace.ViewTimeMin, Workspace.ViewTimeMax));
                }
            }
            finally { syncing = false; }
            Canvas.MarkDirtyRepaint();
        }
        public void Select(params IDirectable[] items) { binding.Select(items); ShowProperties(); }
        public void ShowProperties() { Selection.activeObject = target; Refresh(); }
        public VisualElement CreatePropertyView()
        {
            var view = new ScrollView(); inspectors.Add(view);
            view.RegisterCallback<DetachFromPanelEvent>(_ => inspectors.Remove(view));
            view.RegisterCallback<AttachToPanelEvent>(_ => { if (!inspectors.Contains(view)) inspectors.Add(view); BuildProperties(view); });
            BuildProperties(view); return view;
        }
        private void BuildProperties(VisualElement root)
        {
            root.Clear();
            foreach (var issue in binding.Properties.CaptureIssues())
                root.Add(new Button(() => { binding.Properties.SelectIssue(issue); Refresh(); })
                    { name = "property-issue-" + issue.Key, text = issue.Message });
            if (binding.Properties.Selected == null) { root.Add(new HelpBox(Lan.NotSelectAsset, HelpBoxMessageType.Info)); return; }
            var basic = new Foldout { text = string.Format(Lan.InsBaseInfo, binding.Properties.Title(Document)), value = true, viewDataKey = "timeline-basic-info" };
            basic.Q<Toggle>().style.fontSize = 18; basic.Q<Toggle>().style.unityFontStyleAndWeight = FontStyle.Bold; root.Add(basic);
            var properties = new PropertyView(binding.Properties);
            basic.Add(new Label("Length: " + binding.Properties.DocumentLength));
            properties.DrawFields(basic, Document, Refresh, 0);
            basic.contentContainer.style.marginLeft = 0;
            SetPropertyLabelWidths(basic);
            var panel = new VisualElement(); panel.style.marginTop = 4; root.Add(panel); properties.Populate(panel, binding.Properties.Selected, Refresh);
            SetPropertyLabelWidths(panel);
            basic.SetEnabled(!binding.Properties.Locked(binding.Properties.Selected));
            panel.SetEnabled(!binding.Properties.Locked(binding.Properties.Selected));
        }
        // 只对顶层字段分配标题宽度，向量分量保留原生紧凑标签和输入空间。
        private static void SetPropertyLabelWidths(VisualElement root)
        {
            foreach (var label in root.Query<Label>().ToList().Where(l => l.ClassListContains("unity-base-field__label")))
            {
                if (label.parent.ClassListContains("unity-composite-field__field")) continue;
                label.style.minWidth = label.style.width = 150;
            }
        }
        private void Save()
        {
            Documents.Invoke(DocumentIntent.Save);
        }
        public override void SaveChanges() { Save(); if (!Documents.Dirty) base.SaveChanges(); }
        public override void DiscardChanges()
        {
            Canvas?.Cancel();
            Documents.Discard();
            base.DiscardChanges();
        }
    }
}
