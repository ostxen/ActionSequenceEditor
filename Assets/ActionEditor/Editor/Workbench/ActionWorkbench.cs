using System;
using System.Collections.Generic;
using System.IO;
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
            EditorApplication.projectChanged -= RefreshLibrary;
            if (Session != null) { Session.Changed -= Refresh; Session.Dispose(); Session = null; }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => Run(NewDocument)) { text = "New", name = "new-document" });
            toolbar.Add(new ToolbarButton(() => Run(OpenDialog)) { text = "Open" });
            toolbar.Add(new ToolbarButton(() => Run(() => SaveDocument())) { text = "Save", name = "save-document" });
            toolbar.Add(new ToolbarButton(() => Undo.PerformUndo()) { text = "Undo" });
            toolbar.Add(new ToolbarButton(() => Undo.PerformRedo()) { text = "Redo" });
            toolbar.Add(new ToolbarButton(() => Run(AddTrack)) { text = "+ Track" });
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
            center.Add(timelineHost);
            status = new Label("No document"); status.style.whiteSpace = WhiteSpace.Normal; status.style.maxHeight = 100;
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
            timelineHost.Clear();
            inspector.Clear();
            var document = Session.Document;
            timelineHost.Add(new Label(document == null ? "Create or open an ActionEditor document." : document.Title));
            if (document != null)
            {
                var title = new TextField("Title") { value = document.Title };
                title.RegisterValueChangedCallback(evt => Run(() =>
                {
                    using var edit = new DocumentTransaction(Session, "Rename document");
                    edit.Candidate.Title = evt.newValue; edit.Commit();
                }));
                inspector.Add(title);
                status.text = string.Join("\n", DocumentValidation.Inspect(document));
                if (string.IsNullOrEmpty(status.text)) status.text = Session.IsDirty ? "Unsaved changes" : "Saved";
            }
            else status.text = "No document";
        }

        private void Run(Action operation)
        {
            try { operation(); }
            catch (Exception exception) { if (status != null) status.text = exception.Message; }
        }
    }
}
