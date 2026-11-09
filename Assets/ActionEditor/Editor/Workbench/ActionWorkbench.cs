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
        public TimelineInteraction Interaction => interaction;

        [MenuItem("NBC/Action Editor/Open Workbench", false, -10)]
        public static void Open() => GetWindow<ActionWorkbench>().Show();

        private void OnEnable()
        {
            titleContent = new GUIContent("ActionEditor");
            minSize = new Vector2(850, 400);
            Session = new DocumentSession();
            Session.Changed += Refresh;
            EditorApplication.projectChanged += RefreshLibrary;
        }

        private void OnDisable()
        {
            interaction?.Dispose(); interaction = null;
            EditorApplication.projectChanged -= RefreshLibrary;
            if (Session != null) { Session.Changed -= Refresh; Session.Dispose(); Session = null; }
        }

        private void OnLostFocus() => interaction?.Cancel();

        public void CreateGUI()
        {
            interaction?.Dispose(); interaction = null;
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => Run(NewDocument)) { text = "New", name = "new-document" });
            toolbar.Add(new ToolbarButton(() => Run(OpenDialog)) { text = "Open" });
            toolbar.Add(new ToolbarButton(() => Run(() => SaveDocument())) { text = "Save", name = "save-document" });
            toolbar.Add(new ToolbarButton(() => Undo.PerformUndo()) { text = "Undo" });
            toolbar.Add(new ToolbarButton(() => Undo.PerformRedo()) { text = "Redo" });
            toolbar.Add(new ToolbarButton(() => Run(AddTrack)) { text = "+ Track" });
            var eventMenu = new ToolbarMenu { text = "+ Event", name = "add-event" };
            foreach (var descriptor in Session.Registry.Descriptors)
                eventMenu.menu.AppendAction(descriptor.TypeId, _ => Run(() => AddEvent(descriptor.TypeId)));
            toolbar.Add(eventMenu);
            toolbar.Add(new ToolbarButton(() => Run(() => EditSelection(false))) { text = "Duplicate" });
            toolbar.Add(new ToolbarButton(() => Run(() => EditSelection(true))) { text = "Delete" });
            rootVisualElement.Add(toolbar);

            var outer = new TwoPaneSplitView(0, 210, TwoPaneSplitViewOrientation.Horizontal) { name = "library-split" };
            outer.style.flexGrow = 1;
            var left = new VisualElement();
            left.style.minWidth = 130;
            left.Add(new Label("DOCUMENT LIBRARY"));
            library = new ListView(paths, 24, () => new Label(), (element, index) => ((Label)element).text = System.IO.Path.GetFileNameWithoutExtension(paths[index]));
            library.style.flexGrow = 1;
            library.selectionChanged += selected =>
            {
                if (refreshing) return;
                foreach (var item in selected) if (item is string path) Run(() => OpenDocument(path));
            };
            left.Add(library);
            var right = new TwoPaneSplitView(1, 280, TwoPaneSplitViewOrientation.Horizontal) { name = "inspector-split" };
            right.style.flexGrow = 1;
            var center = new VisualElement(); center.style.flexGrow = 1; center.style.minWidth = 250;
            timelineHost = new VisualElement { name = "timeline-host" }; timelineHost.style.flexGrow = 1;
            canvas = new TimelineCanvas(Session);
            interaction = new TimelineInteraction(canvas, message => { if (status != null) status.text = message; });
            timelineHost.Add(canvas);
            center.Add(timelineHost);
            status = new Label("No document") { name = "document-status" }; status.style.whiteSpace = WhiteSpace.Normal; status.style.maxHeight = 100;
            center.Add(status);
            inspector = new ScrollView { name = "properties" }; inspector.style.minWidth = 200;
            right.Add(center); right.Add(inspector);
            outer.Add(left); outer.Add(right); rootVisualElement.Add(outer);
            RefreshLibrary(); Refresh();
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

        public void SaveDocument()
        {
            var path = Session.Path;
            if (string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanelInProject("Save ActionEditor document", "ActionDocument", "asset", "Choose a new document path.");
            if (!string.IsNullOrEmpty(path)) Session.Save(path);
        }

        public override void SaveChanges() { SaveDocument(); if (!Session.IsDirty) base.SaveChanges(); }
        public override void DiscardChanges() { Session.DiscardAndClose(); base.DiscardChanges(); }

        private void OpenDialog()
        {
            var path = EditorUtility.OpenFilePanel("Open ActionEditor document", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(path)) return;
            var relative = FileUtil.GetProjectRelativePath(path);
            OpenDocument(relative);
        }

        private void AddTrack()
        {
            if (Session.Document == null) throw new InvalidOperationException("Create or open a document first.");
            using var edit = new DocumentTransaction(Session, "Add track");
            if (edit.Candidate.Groups.Count == 0) edit.Candidate.Groups.Add(new DocumentGroup());
            edit.Candidate.Groups[0].Tracks.Add(new DocumentTrack { Name = "Track " + (edit.Candidate.Groups[0].Tracks.Count + 1) });
            edit.Commit();
        }

        public void AddEvent(string typeId)
        {
            using var edit = new DocumentTransaction(Session, "Add event");
            var track = edit.Candidate.Groups.SelectMany(g => g.Tracks).FirstOrDefault()
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
            canvas?.MarkDirtyRepaint();
            if (document != null)
            {
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
                var snap = new DoubleField("Snap seconds (0 off)") { value = Session.Workspace.SnapSeconds, isDelayed = true, name = "timeline-snap" };
                snap.RegisterValueChangedCallback(evt => Run(() =>
                {
                    if (!TimelineTime.IsFinite(evt.newValue) || evt.newValue < 0) throw new ArgumentException("Snap must be finite and nonnegative.");
                    Session.Workspace.SnapSeconds = evt.newValue;
                }));
                inspector.Add(snap);
                var selected = Session.Selection.Select(id => document.Groups.SelectMany(g => g.Tracks).SelectMany(t => t.Events).FirstOrDefault(e => e.Id == id))
                    .Where(e => e != null).ToArray();
                if (selected.Length > 0)
                {
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
                            var row = rows.FindIndex(r => r.Track.Events.Any(e => e.Id == item.Id));
                            if (row >= 0) Session.Workspace.ScrollY = row * TimelineTransform.RowHeight;
                        }
                        Session.NotifyChanged();
                    }) { text = issue.Message, name = "issue-" + issue.TargetId });
                status.text = string.Join("\n", issues);
                if (string.IsNullOrEmpty(status.text)) status.text = Session.IsDirty ? "Unsaved changes" : "Saved";
            }
            else status.text = "No document";
        }

        private void Run(Action operation)
        {
            try { operation(); }
            catch (Exception exception) { Refresh(); if (status != null) status.text = exception.Message; }
        }
    }
}
