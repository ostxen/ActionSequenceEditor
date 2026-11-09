using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Workbench;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Validation
{
    public static class WorkbenchProbeCommands
    {
        private const string Root = "Assets/ActionEditorValidationB06";
        private const string DocumentPath = Root + "/Sample.asset";

        [CliGroupCommand("actioneditor.validation", "B06 fixed isolated real workbench lifecycle. Preview and human approval required.")]
        public static object WorkbenchProbe(
            [CliArg("confirm", "Apply reviewed fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Preview without opening windows or creating assets.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Requires ready EditMode.");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture exists; refusing overwrite.");
            if (dryRun) return new
            {
                schema = 1, root = Root,
                targets = new[] { DocumentPath, Root + "/Resource.asset" },
                operations = new[] { "create fixed new fixture folder and persistent resource", "open dedicated new native workbench window",
                    "create point and interval through registered workbench entry", "edit native properties and nullable collection",
                    "cancel via Escape and check restoration", "Undo/Redo native property edit", "save through workbench",
                    "reject invalid edit without changing saved bytes", "close dedicated window and reopen document in a new window",
                    "compare IDs, times, parameters, nulls and resource references", "save then Undo/Redo dirty-state check",
                    "close owned windows and delete only newly created fixture folder" },
                touchesExistingAssets = false, changesPackagesOrSettings = false
            };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var ownsFolder = false;
            ActionWorkbench window = null;
            try
            {
                ownsFolder = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorValidationB06"));
                if (!ownsFolder) throw new IOException("Could not create fixture folder.");
                var resource = new Texture2D(1, 1);
                AssetDatabase.CreateAsset(resource, Root + "/Resource.asset");
                window = OpenOwnedWindow();
                window.NewDocument();
                window.AddEvent("actioneditor.signal");
                var signalId = window.Session.Selection.Single();
                Field<TextField>(window, "event-Message").value = "B06 point";
                Field<DoubleField>(window, "event-Start").value = 0.25;
                Field<ObjectField>(window, "event-Resource").value = resource;
                Submit(Field<Button>(window, "add-Tags"));
                Field<TextField>(window, "Tags-0").value = "roundtrip";
                Submit(Field<Button>(window, "null-Tags"));
                Require(((SignalEvent)DocumentCommands.Event(window.Session.Document, signalId)).Tags == null, "Native null list edit failed.");
                Submit(Field<Button>(window, "null-Name"));
                window.AddEvent("actioneditor.interval");
                var intervalId = window.Session.Selection.Single();
                Field<DoubleField>(window, "event-Start").value = 1.25;
                Field<DoubleField>(window, "event-Length").value = 0.5;
                Field<Vector3Field>(window, "event-Offset").value = new Vector3(1, 2, 3);
                Field<ObjectField>(window, "event-Resource").value = resource;
                Field<DoubleField>(window, "event-Value").value = 7;
                Undo.PerformUndo();
                Require(((IntervalEvent)DocumentCommands.Event(window.Session.Document, intervalId)).Value == 1, "Undo native value failed.");
                Undo.PerformRedo();
                Require(((IntervalEvent)DocumentCommands.Event(window.Session.Document, intervalId)).Value == 7, "Redo native value failed.");
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                var beforeCancel = EditorJsonUtility.ToJson(window.Session.Document);
                var selection = window.Session.Selection.ToArray();
                var head = window.Session.Workspace.Playhead;
                canvas.Focus();
                window.Interaction.Begin(new Vector2(250, 10), 0, false);
                Require(window.Interaction.IsActive, "Seek gesture did not start.");
                using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                { escape.target = canvas; canvas.SendEvent(escape); }
                Require(!window.Interaction.IsActive && window.Session.Workspace.Playhead == head
                    && window.Session.Selection.SequenceEqual(selection) && EditorJsonUtility.ToJson(window.Session.Document) == beforeCancel, "Escape did not restore state.");
                var documentId = window.Session.Document.Id;
                window.SaveDocument(DocumentPath);
                Require(!window.Session.IsDirty, "Saved workbench is dirty.");
                var saved = File.ReadAllBytes(DocumentPath);
                Field<DoubleField>(window, "event-Length").value = -1;
                Require(!window.Session.IsDirty && File.ReadAllBytes(DocumentPath).SequenceEqual(saved), "Rejected native edit changed saved state.");
                window.Close(); window = null;
                var loaded = AssetDatabase.LoadAssetAtPath<ActionDocument>(DocumentPath);
                Resources.UnloadAsset(loaded);
                window = OpenOwnedWindow();
                window.OpenDocument(DocumentPath);
                Check(window, documentId, signalId, intervalId, resource);
                Require(!window.Session.IsDirty, "Reopened workbench is dirty.");
                Field<TextField>(window, "document-title").value = "B06 saved title";
                window.SaveDocument();
                Undo.PerformUndo(); Require(window.Session.IsDirty, "Undo after save must become dirty.");
                Undo.PerformRedo(); Require(!window.Session.IsDirty, "Redo back to saved state must become clean.");
                Check(window, documentId, signalId, intervalId, resource);
                return new { status = "passed", root = Root, checks = new[] { "registered point/interval creation", "native fields and null collection",
                    "Escape cancellation", "Undo/Redo", "workbench save and close/reopen", "IDs/times/parameters/references roundtrip",
                    "invalid edit protects saved bytes", "saved-state Undo/Redo dirty tracking" }, cleanup = "owned windows closed and new fixture folder deleted before response" };
            }
            finally
            {
                try
                {
                    if (window != null) { window.Session.DiscardAndClose(); window.Close(); }
                }
                finally { if (ownsFolder && !AssetDatabase.DeleteAsset(Root)) throw new IOException("Fixture cleanup failed: " + Root); }
            }
        }

        private static ActionWorkbench OpenOwnedWindow()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(80, 80, 1200, 700);
                window.Show(); window.CreateGUI();
                return window;
            }
            catch { window.Close(); throw; }
        }
        private static T Field<T>(ActionWorkbench window, string name) where T : VisualElement
            => window.rootVisualElement.Q<T>(name) ?? throw new InvalidOperationException("Missing native field: " + name);
        private static void Submit(Button button)
        {
            using var evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Check(ActionWorkbench window, string documentId, string signalId, string intervalId, Texture2D resource)
        {
            var signal = (SignalEvent)DocumentCommands.Event(window.Session.Document, signalId);
            var interval = (IntervalEvent)DocumentCommands.Event(window.Session.Document, intervalId);
            Require(window.Session.Document.Id == documentId && signal.Start == 0.25 && signal.Message == "B06 point" && signal.Name == null && signal.Tags == null
                && signal.Resource == resource && interval.Start == 1.25 && interval.Duration == 0.5 && interval.Value == 7
                && interval.Offset == new Vector3(1, 2, 3) && interval.Resource == resource, "Persisted workbench content changed.");
        }
    }
}
