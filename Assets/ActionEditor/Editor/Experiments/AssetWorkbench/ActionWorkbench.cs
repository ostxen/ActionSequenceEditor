using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Extensibility;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Workbench
{
    public sealed class ActionWorkbench : EditorWindow
    {
        public DocumentSession Session { get; private set; }
        private VisualElement inspector;
        private VisualElement timelineHost;
        private Label status;
        private ListView library;
        private readonly List<string> paths = new();
        private bool refreshing;
        private TimelineCanvas canvas;
        private TimelineInteraction interaction;
        private TwoPaneSplitView librarySplit;
        private Label documentHeading;
        private Label trackHeading;
        private DoubleField timeField;
        private MinMaxSlider timeRange;
        private Scroller verticalScroll;
        private VisualElement documentControls;
        private VisualElement emptyState;
        private bool syncingView;
        private bool libraryVisible;
        private VisualElement transport;
        private WorkbenchInspectorTarget inspectorTarget;
        private readonly List<VisualElement> propertyViews = new();
        public VisualElement Properties => propertyViews.LastOrDefault() ?? inspector;
        public TimelineInteraction Interaction => interaction;

        // Retained experimental .asset implementation. Main menu now uses the shared JSON model.
        public static void Open() => GetWindow<ActionWorkbench>().Show();

        private void OnEnable()
        {
            titleContent = new GUIContent("ActionEditor");
            minSize = new Vector2(850, 400);
            Session = new DocumentSession();
            inspectorTarget = CreateInstance<WorkbenchInspectorTarget>();
            inspectorTarget.hideFlags = HideFlags.HideAndDontSave;
            inspectorTarget.Owner = this;
            Session.Changed += Refresh;
            EditorApplication.projectChanged += RefreshLibrary;
        }

        private void OnDisable()
        {
            interaction?.Dispose(); interaction = null;
            EditorApplication.projectChanged -= RefreshLibrary;
            if (inspectorTarget != null) DestroyImmediate(inspectorTarget);
            if (Session != null) { Session.Changed -= Refresh; Session.Dispose(); Session = null; }
        }

        private void OnLostFocus() => interaction?.Cancel();

        public void CreateGUI()
        {
            interaction?.Dispose(); interaction = null;
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            var header = new VisualElement { name = "timeline-header" };
            header.style.flexDirection = FlexDirection.Row; header.style.height = 20; header.style.flexShrink = 0;
            transport = new VisualElement { name = "document-controls" };
            transport.style.flexDirection = FlexDirection.Row; transport.style.flexShrink = 0;
            void Control(string text, Action action, string hint, bool available = true)
            {
                var button = new ToolbarButton(action) { text = text, tooltip = hint };
                button.style.flexGrow = 1; button.style.width = 26; button.style.marginLeft = 0; button.style.marginRight = 0;
                button.SetEnabled(available); transport.Add(button);
            }
            Control("|<", () => SetPlayhead(0), "定位开始，不执行事件");
            Control("<", () => SetPlayhead(Session.Workspace.Playhead - 1.0 / 60), "后退 1/60 秒");
            Control("▶", () => { }, "播放预览尚未接入", false);
            Control("Ⅱ", () => { }, "暂停预览尚未接入", false);
            Control(">", () => SetPlayhead(Session.Workspace.Playhead + 1.0 / 60), "前进 1/60 秒");
            Control(">|", () => SetPlayhead(Session.Document?.Duration ?? 0), "定位结束，不执行事件");
            var fit = new ToolbarButton(FitTimeline) { text = "↔", name = "fit-timeline", tooltip = "显示完整时间范围" };
            transport.Add(fit); header.Add(transport);
            var toolbar = new Toolbar(); toolbar.style.flexGrow = 1; toolbar.style.minWidth = 0;
            documentHeading = new Label { name = "document-heading" }; documentHeading.style.display = DisplayStyle.None;
            toolbar.Add(documentHeading);
            var documents = new ToolbarMenu { text = "[文档]", name = "document-menu" };
            documents.menu.AppendAction("New", _ => Run(NewDocument));
            documents.menu.AppendAction("Open…", _ => Run(OpenDialog));
            documents.menu.AppendAction("文档库", _ => ToggleLibrary());
            toolbar.Add(documents);
            toolbar.Add(new ToolbarButton(() => Run(NewDocument)) { text = "New", name = "new-document" });
            toolbar.Add(new ToolbarButton(() => Run(OpenDialog)) { text = "Open" });
            timeField = new DoubleField { name = "playhead-time", isDelayed = true, tooltip = "当前时间（秒），仅定位" };
            timeField.style.width = 65; timeField.RegisterValueChangedCallback(evt => Run(() => SetPlayhead(evt.newValue)));
            toolbar.Add(timeField);
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; toolbar.Add(spacer);
            toolbar.Add(new ToolbarButton(() => Run(() => SaveDocument())) { text = "Save", name = "save-document" });
            var snap = new ToolbarToggle { text = "磁吸", name = "snap-toggle", value = Session.Workspace.SnapSeconds > 0 };
            snap.RegisterValueChangedCallback(evt => { Session.Workspace.SnapSeconds = evt.newValue ? 0.1 : 0; }); toolbar.Add(snap);
            toolbar.Add(new ToolbarButton(ShowProperties) { text = "属性", tooltip = "在 Unity Inspector 编辑文档及当前选择" });
            header.Add(toolbar); rootVisualElement.Add(header);

            var outer = librarySplit = new TwoPaneSplitView(0, 180, TwoPaneSplitViewOrientation.Horizontal) { name = "library-split" };
            outer.style.flexGrow = 1; outer.style.minHeight = 0; outer.style.marginRight = 2;
            var left = new VisualElement(); left.style.minWidth = 130;
            left.Add(SectionTitle("文档库"));
            library = new ListView(paths, 24, () => new Label(), (element, index) => ((Label)element).text = Path.GetFileNameWithoutExtension(paths[index]));
            library.style.flexGrow = 1;
            library.selectionChanged += selected => { if (!refreshing) foreach (var item in selected) if (item is string path) Run(() => OpenDocument(path)); };
            left.Add(library);
            var center = new VisualElement(); center.style.flexGrow = 1; center.style.minWidth = 400; center.style.minHeight = 0;
            var body = new VisualElement { name = "timeline-body" }; body.style.flexDirection = FlexDirection.Row; body.style.flexGrow = 1; body.style.minHeight = 0;
            timelineHost = new VisualElement { name = "timeline-host" }; timelineHost.style.flexGrow = 1; timelineHost.style.minWidth = 0;
            canvas = new TimelineCanvas(Session); interaction = new TimelineInteraction(canvas, message => { if (status != null) status.text = message; });
            canvas.ViewChanged += SyncView;
            canvas.RegisterCallback<GeometryChangedEvent>(_ => SyncView());
            canvas.RegisterCallback<PointerUpEvent>(_ => SyncView());
            canvas.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0 && Session.Document != null) ShowProperties(); });
            canvas.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
            timelineHost.Add(canvas);
            emptyState = new VisualElement { name = "timeline-empty" };
            emptyState.style.position = Position.Absolute; emptyState.style.left = 24; emptyState.style.top = 90;
            emptyState.Add(new Button(() => Run(NewDocument)) { text = "新建文档 / New" });
            emptyState.Add(new Button(() => Run(OpenDialog)) { text = "打开文档 / Open" }); timelineHost.Add(emptyState);
            body.Add(timelineHost);
            verticalScroll = new Scroller(0, 0, value =>
            {
                if (syncingView) return;
                interaction.Cancel(); Session.Workspace.ScrollY = value; canvas.MarkDirtyRepaint();
            }, SliderDirection.Vertical) { name = "track-scroll" };
            verticalScroll.style.width = 14; verticalScroll.style.flexShrink = 0; body.Add(verticalScroll); center.Add(body);
            var footer = new VisualElement { name = "timeline-range-bar" };
            footer.style.flexDirection = FlexDirection.Row; footer.style.height = 16; footer.style.flexShrink = 0;
            documentControls = new VisualElement { name = "group-controls" }; documentControls.style.flexDirection = FlexDirection.Row; documentControls.style.flexShrink = 0;
            documentControls.Add(new ToolbarButton(() => Run(AddGroup)) { text = "Add Group", name = "add-group" });
            documentControls.Add(new ToolbarButton(() => Run(AddTrack)) { text = "+ Track", name = "add-track", tooltip = "在当前组新增轨道，也可右键组名" });
            trackHeading = new Label { name = "selected-track" }; trackHeading.style.display = DisplayStyle.None; documentControls.Add(trackHeading);
            footer.Add(documentControls);
            timeRange = new MinMaxSlider(0, 5, 0, 5) { name = "time-range", tooltip = "拖动范围平移，两端缩放；双击还原完整范围" };
            timeRange.style.flexGrow = 1; timeRange.style.marginRight = 14;
            timeRange.RegisterValueChangedCallback(evt =>
            {
                if (syncingView || Session.Document == null || evt.newValue.y - evt.newValue.x < 0.001f) return;
                interaction.Cancel(); Session.Workspace.ScrollTime = evt.newValue.x;
                Session.Workspace.PixelsPerSecond = Math.Max(1, canvas.contentRect.width - canvas.LeftWidth) / (evt.newValue.y - evt.newValue.x);
                canvas.NotifyViewChanged();
            });
            timeRange.RegisterCallback<PointerDownEvent>(evt => { if (evt.clickCount == 2) { FitTimeline(); evt.StopPropagation(); } });
            footer.Add(timeRange); center.Add(footer);
            outer.Add(left); outer.Add(center); rootVisualElement.Add(outer); if (!libraryVisible) outer.CollapseChild(0);
            inspector = new VisualElement { name = "properties" };
            status = new Label { name = "document-status" }; status.style.height = 18; status.style.flexShrink = 0;
            status.style.paddingLeft = 6; rootVisualElement.Add(status);
            RefreshLibrary(); Refresh();
        }

        private void ToggleLibrary()
        {
            libraryVisible = !libraryVisible;
            if (libraryVisible) librarySplit.UnCollapse(); else librarySplit.CollapseChild(0);
        }

        public void ShowProperties()
        {
            Refresh();
            Selection.activeObject = inspectorTarget;
        }

        public VisualElement CreatePropertyView()
        {
            var view = new ScrollView { name = "properties" };
            propertyViews.Add(view);
            view.RegisterCallback<AttachToPanelEvent>(_ => { if (!propertyViews.Contains(view)) propertyViews.Add(view); Refresh(); });
            view.RegisterCallback<DetachFromPanelEvent>(_ => propertyViews.Remove(view));
            Refresh();
            return view;
        }

        private void BuildContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (Session.Document == null) return;
            var local = canvas.WorldToLocal(evt.mousePosition);
            var index = canvas.RowAt(local);
            if (index < 0) { evt.menu.AppendAction("Add Group", _ => Run(AddGroup)); return; }
            var row = canvas.Rows()[index];
            var hit = canvas.Hit(local);
            if (hit != null)
            {
                if (!Session.Selection.Contains(hit.Id)) { Session.Selection.Clear(); Session.Selection.Add(hit.Id); }
                Session.NotifyChanged(); ShowProperties();
                evt.menu.AppendAction("Duplicate", _ => Run(() => EditSelection(false)));
                evt.menu.AppendAction("Delete", _ => Run(() => EditSelection(true)));
                return;
            }
            Session.Workspace.SelectedGroupId = row.Group.Id; Session.Selection.Clear();
            if (row.Track == null)
            {
                Session.Workspace.SelectedTrackId = null;
                evt.menu.AppendAction("Add Track", _ => Run(AddTrack));
                evt.menu.AppendAction("折叠/展开", _ => canvas.ToggleGroup(row.Group.Id));
            }
            else canvas.SelectTrack(row.Track.Id);
            if (local.x < canvas.LeftWidth || row.Track == null)
            {
                evt.menu.AppendAction(row.Group.Locked || row.Track?.Locked == true ? "Unlock" : "Lock", _ => Run(() => ChangeRow(row, false)));
                evt.menu.AppendAction("Delete", _ => Run(() => ChangeRow(row, true)));
            }
            else
            {
                var snap = Session.Workspace.SnapSeconds;
                var time = canvas.Transform.ToTime(local.x);
                time = Math.Clamp(snap > 0 ? Math.Round(time / snap, MidpointRounding.AwayFromZero) * snap : time, 0, Session.Document.Duration);
                foreach (var descriptor in Session.Registry.Descriptors)
                    evt.menu.AppendAction("Add Event/" + descriptor.TypeId, _ => Run(() => { canvas.SelectTrack(row.Track.Id); SetPlayhead(time); AddEvent(descriptor.TypeId); }));
            }
            Session.NotifyChanged(); ShowProperties();
        }

        private void ChangeRow(TimelineRow row, bool delete)
        {
            if (delete && !EditorUtility.DisplayDialog("Delete", "删除当前" + (row.Track == null ? "组及全部轨道？" : "轨道及全部事件？"), "Delete", "Cancel")) return;
            using var edit = new DocumentTransaction(Session, delete ? "Delete row" : "Toggle row lock");
            var group = edit.Candidate.Groups.Single(g => g.Id == row.Group.Id);
            if (row.Track == null) { if (delete) edit.Candidate.Groups.Remove(group); else group.Locked = !group.Locked; }
            else { var track = group.Tracks.Single(t => t.Id == row.Track.Id); if (delete) group.Tracks.Remove(track); else track.Locked = !track.Locked; }
            edit.Commit();
        }

        private void AddGroup()
        {
            using var edit = new DocumentTransaction(Session, "Add group");
            var group = new DocumentGroup { Name = "Group " + (edit.Candidate.Groups.Count + 1) };
            edit.Candidate.Groups.Add(group); edit.Commit();
            Session.Workspace.SelectedGroupId = group.Id; Session.Workspace.SelectedTrackId = null;
            Session.Selection.Clear(); Session.NotifyChanged(); ShowProperties();
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.style.height = 28; label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.paddingLeft = 8; label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.backgroundColor = new Color(0.24f, 0.25f, 0.27f);
            return label;
        }

        private void SetPlayhead(double time)
        {
            if (!TimelineTime.IsFinite(time)) throw new ArgumentException("Time must be finite.");
            interaction?.Cancel();
            Session.Workspace.Playhead = Math.Clamp(time, 0, Session.Document?.Duration ?? 0);
            canvas.NotifyViewChanged();
        }

        private void FitTimeline()
        {
            if (Session.Document == null) return;
            interaction?.Cancel();
            Session.Workspace.ScrollTime = 0;
            Session.Workspace.PixelsPerSecond = Math.Max(1, canvas.contentRect.width - canvas.LeftWidth) / Session.Document.Duration;
            canvas.NotifyViewChanged();
        }

        private void SyncView()
        {
            if (canvas == null || timeRange == null || syncingView) return;
            syncingView = true;
            try
            {
                var rows = canvas.Rows();
                var track = rows.FirstOrDefault(r => r.Track?.Id == Session.Workspace.SelectedTrackId && r.Track != null).Track;
                trackHeading.text = track == null ? "未选择轨道" : track.Name;
                transport.style.width = canvas.LeftWidth;
                documentControls.style.width = canvas.LeftWidth;
                timeField.SetValueWithoutNotify(Session.Workspace.Playhead);
                verticalScroll.highValue = Math.Max(0, rows.Count * TimelineTransform.RowHeight - Math.Max(0, canvas.contentRect.height - TimelineTransform.RulerHeight));
                Session.Workspace.ScrollY = Math.Clamp(Session.Workspace.ScrollY, 0, verticalScroll.highValue);
                verticalScroll.value = Session.Workspace.ScrollY;
                var end = (float)(Session.Workspace.ScrollTime + Math.Max(1, canvas.contentRect.width - canvas.LeftWidth) / Session.Workspace.PixelsPerSecond);
                timeRange.highLimit = Math.Max((float)(Session.Document?.Duration ?? 5), end);
                timeRange.lowLimit = Math.Min(0, (float)Session.Workspace.ScrollTime);
                timeRange.SetValueWithoutNotify(new Vector2((float)Session.Workspace.ScrollTime, end));
            }
            finally { syncingView = false; }
        }

        public void NewDocument()
        {
            if (!ConfirmSwitch()) return;
            Session.New();
            AddTrack();
        }

        public void OpenDocument(string path)
        {
            if (path == Session.Path) return;
            if (!ConfirmSwitch()) return;
            Session.Open(path);
        }

        private bool ConfirmSwitch()
        {
            if (!Session.IsDirty) return true;
            var choice = EditorUtility.DisplayDialogComplex("Unsaved document", "Save the current document before switching?", "Save", "Cancel", "Discard");
            if (choice == 1) return false;
            if (choice == 0) { SaveDocument(); return !Session.IsDirty; }
            Session.DiscardAndClose(); return true;
        }

        public void SaveDocument(string target = null)
        {
            var path = target ?? Session.Path;
            if (string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanelInProject("Save ActionEditor document", "ActionDocument", "asset", "Choose a new document path.");
            if (!string.IsNullOrEmpty(path)) Session.Save(path);
        }

        public override void SaveChanges() { SaveDocument(); if (!Session.IsDirty) base.SaveChanges(); }
        public override void DiscardChanges() { Session.DiscardAndClose(); base.DiscardChanges(); }

        private void OpenDialog()
        {
            var path = EditorUtility.OpenFilePanel("Open ActionEditor document", UnityEngine.Application.dataPath, "asset");
            if (string.IsNullOrEmpty(path)) return;
            var relative = FileUtil.GetProjectRelativePath(path);
            OpenDocument(relative);
        }

        private void AddTrack()
        {
            if (Session.Document == null) throw new InvalidOperationException("Create or open a document first.");
            using var edit = new DocumentTransaction(Session, "Add track");
            if (edit.Candidate.Groups.Count == 0) edit.Candidate.Groups.Add(new DocumentGroup());
            var group = edit.Candidate.Groups.FirstOrDefault(g => g.Id == Session.Workspace.SelectedGroupId) ?? edit.Candidate.Groups[0];
            var track = new DocumentTrack { Name = "Track " + (group.Tracks.Count + 1) };
            group.Tracks.Add(track);
            edit.Commit();
            Session.Workspace.CollapsedGroups.Remove(Session.Document.Groups.First(g => g.Tracks.Any(t => t.Id == track.Id)).Id);
            canvas.SelectTrack(track.Id, true);
            Session.Selection.Clear(); Session.NotifyChanged();
        }

        public void AddEvent(string typeId)
        {
            using var edit = new DocumentTransaction(Session, "Add event");
            var tracks = edit.Candidate.Groups.SelectMany(g => g.Tracks).ToArray();
            var track = tracks.FirstOrDefault(t => t.Id == Session.Workspace.SelectedTrackId) ?? tracks.FirstOrDefault()
                ?? throw new InvalidOperationException("Add a track first.");
            var item = Session.Registry.Create(typeId);
            item.Start = Session.Workspace.Playhead;
            DocumentCommands.Add(edit, track.Id, item);
            edit.Commit();
        }

        private void EditSelection(bool delete)
        {
            using var edit = new DocumentTransaction(Session, delete ? "Delete selection" : "Duplicate selection");
            if (delete) DocumentCommands.Delete(edit, Session.Selection);
            else DocumentCommands.Duplicate(edit, Session.Selection);
            edit.Commit();
        }

        private void RefreshLibrary()
        {
            paths.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:ActionDocument", new[] { "Assets" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            if (library != null) { refreshing = true; library.Rebuild(); refreshing = false; }
        }

        public void Refresh()
        {
            if (Session == null || timelineHost == null) return;
            hasUnsavedChanges = Session.IsDirty;
            saveChangesMessage = "Save ActionEditor changes before closing?";
            inspector.Clear();
            var document = Session.Document;
            documentHeading.text = document == null ? "ActionEditor" : document.Title + (Session.IsDirty ? " *" : "");
            documentControls.SetEnabled(document != null);
            transport.SetEnabled(document != null);
            emptyState.style.display = document == null ? DisplayStyle.Flex : DisplayStyle.None;
            timeRange.SetEnabled(document != null);
            canvas?.MarkDirtyRepaint();
            BuildProperties(document);
            foreach (var view in propertyViews.ToArray())
            {
                var previous = inspector;
                inspector = view;
                try { inspector.Clear(); BuildProperties(document); }
                finally { inspector = previous; }
            }
            var menu = rootVisualElement.Q<ToolbarMenu>("document-menu");
            if (menu != null) menu.text = "[" + (document?.Title ?? "文档") + (Session.IsDirty ? " *" : "") + "]";
            SyncView();
        }

        private void BuildProperties(ActionDocument document)
        {
            if (document != null)
            {
                var tracks = document.Groups.SelectMany(g => g.Tracks).ToArray();
                if (Session.Workspace.SelectedTrackId != null && !tracks.Any(t => t.Id == Session.Workspace.SelectedTrackId)) Session.Workspace.SelectedTrackId = null;
                inspector.Add(SectionTitle("文档 / DOCUMENT"));
                var title = new TextField("Title") { value = document.Title, isDelayed = true, name = "document-title" };
                title.RegisterValueChangedCallback(evt => Run(() =>
                {
                    using var edit = new DocumentTransaction(Session, "Rename document");
                    edit.Candidate.Title = evt.newValue; edit.Commit();
                }));
                inspector.Add(title);
                var duration = new DoubleField("Duration") { value = document.Duration, isDelayed = true, name = "document-duration" };
                duration.RegisterValueChangedCallback(evt => Run(() =>
                {
                    using var edit = new DocumentTransaction(Session, "Document duration");
                    edit.Candidate.Duration = evt.newValue; edit.Commit();
                }));
                inspector.Add(duration);
                var snap = new DoubleField("Snap (s)") { value = Session.Workspace.SnapSeconds, isDelayed = true, name = "timeline-snap", tooltip = "吸附间隔，秒；0 关闭吸附" };
                snap.RegisterValueChangedCallback(evt => Run(() =>
                {
                    if (!TimelineTime.IsFinite(evt.newValue) || evt.newValue < 0) throw new ArgumentException("Snap must be finite and nonnegative.");
                    Session.Workspace.SnapSeconds = evt.newValue;
                }));
                inspector.Add(snap);
                var currentGroup = document.Groups.FirstOrDefault(g => g.Id == Session.Workspace.SelectedGroupId);
                if (currentGroup != null)
                {
                    inspector.Add(SectionTitle("组 / GROUP"));
                    var groupName = new TextField("Name") { value = currentGroup.Name, isDelayed = true, name = "group-name" };
                    groupName.RegisterValueChangedCallback(evt => Run(() =>
                    {
                        using var edit = new DocumentTransaction(Session, "Rename group");
                        edit.Candidate.Groups.Single(g => g.Id == currentGroup.Id).Name = evt.newValue; edit.Commit();
                    }));
                    inspector.Add(groupName);
                }
                var currentTrack = tracks.FirstOrDefault(t => t.Id == Session.Workspace.SelectedTrackId);
                if (currentTrack != null)
                {
                    inspector.Add(SectionTitle("轨道 / TRACK"));
                    var trackName = new TextField("Name") { value = currentTrack.Name, isDelayed = true, name = "track-name" };
                    trackName.RegisterValueChangedCallback(evt => Run(() =>
                    {
                        using var edit = new DocumentTransaction(Session, "Rename track");
                        DocumentCommands.Track(edit.Candidate, currentTrack.Id).Name = evt.newValue; edit.Commit();
                    }));
                    inspector.Add(trackName);
                    inspector.Add(new Label(currentTrack.Events.Count + " 个事件"));
                }
                var selected = Session.Selection.Select(id => document.Groups.SelectMany(g => g.Tracks).SelectMany(t => t.Events).FirstOrDefault(e => e.Id == id))
                    .Where(e => e != null).ToArray();
                if (selected.Length > 0)
                {
                    inspector.Add(SectionTitle("事件 / EVENT"));
                    inspector.Add(new Label(selected.Length + " selected"));
                    var context = new EventPropertyContext(Session, inspector, message => status.text = message);
                    context.Text("Name", e => e.Name, (e, v) => e.Name = v);
                    context.Number("Start", e => e.Start, (e, v) => e.Start = v);
                    if (selected.All(e => e.Kind == EventKind.Interval)) context.Number("Length", e => e.Duration, (e, v) => e.Duration = v);
                    context.Integer("Order", e => e.Order, (e, v) => e.Order = v);
                    // Locked definitions remain selectable but cannot be changed by this editor.
                    inspector.Add(new Label(selected.Any(e => e.Locked) ? "Selection includes locked events" : "Selection unlocked"));
                    if (selected.All(e => e.GetType() == selected[0].GetType()))
                    {
                        var descriptor = Session.Registry.Find(selected[0]);
                        if (descriptor == null) inspector.Add(new Label("Unknown type retained; editing and saving disabled."));
                        else descriptor.Properties(context);
                    }
                    else inspector.Add(new Label("Mixed types: common properties only."));
                }
                var issues = Session.Registry.Inspect(document);
                foreach (var issue in issues)
                    inspector.Add(new Button(() =>
                    {
                        Session.Selection.Clear();
                        var item = document.Groups.SelectMany(g => g.Tracks).SelectMany(t => t.Events).FirstOrDefault(e => e.Id == issue.TargetId);
                        if (item != null)
                        {
                            Session.Selection.Add(item.Id); Session.Workspace.ScrollTime = Math.Max(0, item.Start - 0.5);
                            var rows = canvas.Rows();
                            var row = rows.FindIndex(r => r.Track != null && r.Track.Events.Any(e => e.Id == item.Id));
                            if (row >= 0) Session.Workspace.ScrollY = row * TimelineTransform.RowHeight;
                        }
                        Session.NotifyChanged();
                    }) { text = issue.Message, name = "issue-" + issue.TargetId });
                var hint = new Label("选择组、轨道头或事件进行编辑。\n时间区滚轮缩放 · 中键横向平移 · 轨道头滚轮纵向滚动 · Esc 取消");
                hint.style.whiteSpace = WhiteSpace.Normal; hint.style.marginTop = 10;
                inspector.Add(hint);
                status.text = issues.Count > 0 ? issues[0].Message : (Session.IsDirty ? "未保存 / Unsaved changes" : "已保存 / Saved") + "  |  " + document.Groups.Sum(g => g.Tracks.Count) + " 条轨道";
            }
            else status.text = "尚未打开文档：点击 New 或 Open 开始";
            inspector.Query<Label>(className: "unity-base-field__label").ForEach(label =>
            {
                // Keep Vector3's nested x/y/z labels compact, not the full inspector label width.
                if (label.GetFirstAncestorOfType<Vector3Field>() != null && !(label.parent is Vector3Field)) return;
                label.style.minWidth = 75; label.style.width = 85;
            });
        }

        private void Run(Action operation)
        {
            try { operation(); }
            catch (Exception exception) { Refresh(); if (status != null) status.text = exception.Message; }
        }
    }
}
