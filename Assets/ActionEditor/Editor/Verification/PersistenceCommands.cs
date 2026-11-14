using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Domain;
using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Storage;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Verification
{
    // 验证磁盘持久化、撤销基线和外部冲突，不更改既有文件。
    public static class PersistenceCommands
    {
        private const string Root = "Assets/ActionEditorPersistenceVerification";
        private const string Path = Root + "/Shared.json";
        [CliGroupCommand("actioneditor.verification", "Fixed document persistence lifecycle. Creates only its new fixture folder; restores current session; deletes only its owned fixtures.")]
        public static object PersistenceProbe(
            [CliArg("confirm", "Apply reviewed isolated fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Preview only, without changing session or files.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Requires ready EditMode.");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture exists; refusing overwrite.");
            if (ActionEditorHost.Context.Session.IsEditing || ActionEditorHost.Context.Playback.IsPlaying)
                throw new InvalidOperationException("需要当前会话没有活动编辑或播放。");
            if (dryRun) return new
            {
                schema = 1, root = Root, targets = new[] { Path, Root + "/Alternate.json" },
                operations = new[] { "capture and finally restore current in-memory session including unsaved content",
                    "create and open isolated Shared.json through document lifecycle", "edit through shared document session",
                    "save-as to new Alternate.json and verify current source switches, then reopen Shared.json",
                    "atomic save, Undo/Redo saved-state tracking and reload", "save public graph time edits with stale derived duration and reload",
                    "discard committed and pending edits to the last saved baseline without writing bytes or losing disk revision",
                    "eight fractional timeline edit/save/import/reload cycles with exact float preservation",
                    "reject save-as onto existing fixture, preserve current graph on failed open and restore recovery baseline",
                    "reject locked fixture replacement without changing bytes or baseline, then save after unlocking",
                    "verify active-edit autosave suppression and explicit save after completed edit",
                    "reject externally modified target without overwriting it",
                    "delete only this newly created fixture folder" },
                touchesExistingAssets = false, changesPackagesOrSettings = false
            };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var recovery = ActionEditorHost.Context.Session.CaptureRecovery();
            var previous = ActionEditorHost.Context.Source;
            var ownsFolder = false;
            TextAsset memory = null;
            try
            {
                ownsFolder = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorPersistenceVerification"));
                if (!ownsFolder) throw new IOException("Could not create fixture folder.");
                new Composition.DocumentDialogComposition(ActionEditorHost.Context).CreateAndOpen(typeof(FeatureSampleDocument), Path);
                var created = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                Require(ActionEditorHost.Context.Session.Path == Path && ActionEditorHost.Context.Source == created, "Create/open did not bind source.");
                memory = new TextAsset(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(new FeatureSampleDocument()));
                Require(!ActionEditorHost.Context.Session.IsDirty, "New save is dirty.");
                ActionEditorHost.Context.Session.Edit("Persistence payload", () =>
                {
                    ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 42;
                    ((FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[1]).Count = 17;
                });
                ActionEditorHost.Context.Session.Save(ActionEditorHost.Context.Session.Path);
                var committed = File.ReadAllBytes(Path);
                Require(!ActionEditorHost.Context.Session.IsDirty, "Saved payload is dirty.");
                Undo.PerformUndo(); Require(ActionEditorHost.Context.Session.IsDirty && ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version == 1, "Undo after save failed.");
                Undo.PerformRedo(); Require(!ActionEditorHost.Context.Session.IsDirty && ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version == 42, "Redo to saved state failed.");
                ActionEditorHost.Context.Session.Edit("Persistence discarded payload", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 99);
                ActionEditorHost.Context.Session.BeginEdit(); ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 100;
                ActionEditorHost.Context.Session.DiscardChanges(); ActionEditorHost.Context.AutoSave.Tick(Prefs.autoSaveSeconds);
                Require(!ActionEditorHost.Context.Session.IsDirty && !ActionEditorHost.Context.Session.IsEditing && ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version == 42
                    && File.ReadAllBytes(Path).SequenceEqual(committed), "Discard did not restore saved baseline without writes.");
                Undo.PerformUndo(); Undo.PerformRedo();
                Require(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version == 42, "Discarded edits returned through Undo/Redo.");
                using (var other = NBC.ActionEditor.Composition.DocumentComposition.Create())
                {
                    other.Open(new JsonDocumentRepository().ReadSource(memory));
                    var refused = false;
                    try { other.Save(Path); } catch (IOException) { refused = true; }
                    Require(refused && string.IsNullOrEmpty(other.Path) && File.ReadAllBytes(Path).SequenceEqual(committed), "Save-as overwrote an existing fixture.");
                }
                var beforeOpen = ActionEditorHost.Context.Document;
                var invalidText = new TextAsset("{\"format\":\"actioneditor.json\",\"version\":1,\"document\":null}");
                try
                {
                    var refused = false;
                    try { ActionEditorHost.Context.Session.Open(new JsonDocumentRepository().ReadSource(invalidText)); } catch (InvalidDataException) { refused = true; }
                    Require(refused && ReferenceEquals(ActionEditorHost.Context.Document, beforeOpen), "Failed open replaced the current graph.");
                }
                finally { UnityEngine.Object.DestroyImmediate(invalidText); }
                ActionEditorHost.Context.Session.Edit("Persistence recovery payload", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 88);
                using (var restored = NBC.ActionEditor.Composition.DocumentComposition.Create())
                {
                    restored.RestoreRecovery(ActionEditorHost.Context.Session.CaptureRecovery());
                    Require(restored.IsDirty && ((FeatureSampleDocument)restored.Document).Version == 88, "Recovery lost unsaved content.");
                    restored.DiscardChanges();
                    Require(!restored.IsDirty && restored.Path == Path && ((FeatureSampleDocument)restored.Document).Version == 42, "Recovery lost saved baseline.");
                }
                ActionEditorHost.Context.Session.DiscardChanges();
                var alternate = Root + "/Alternate.json";
                ActionEditorHost.Context.Session.Save(alternate);
                Require(ActionEditorHost.Context.Session.Path == alternate && AssetDatabase.GetAssetPath(ActionEditorHost.Context.Source) == alternate && !ActionEditorHost.Context.Session.IsDirty, "Save-as did not switch source and baseline.");
                ActionEditorHost.Context.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                ActionEditorHost.Context.Session.Edit("Persistence locked payload", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 43);
                using (var locked = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var refused = false;
                    try { ActionEditorHost.Context.Session.Save(ActionEditorHost.Context.Session.Path); } catch (IOException) { refused = true; }
                    Require(refused && ActionEditorHost.Context.Session.IsDirty && ActionEditorHost.Context.Session.Path == Path && File.ReadAllBytes(Path).SequenceEqual(committed), "Locked save changed bytes or baseline.");
                }
                ActionEditorHost.Context.Session.Save(ActionEditorHost.Context.Session.Path); Require(!ActionEditorHost.Context.Session.IsDirty, "Save after unlocking failed.");
                ActionEditorHost.Context.Session.DiscardChanges(); Require(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version == 43, "Successful save did not advance baseline.");
                var active = ActionEditorHost.Context.Session.BeginEdit(new object());
                ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 44;
                var beforeAuto = File.ReadAllBytes(Path);
                ActionEditorHost.Context.AutoSave.Tick(Prefs.autoSaveSeconds);
                Require(active.IsActive && File.ReadAllBytes(Path).SequenceEqual(beforeAuto), "Automatic save wrote an active gesture.");
                active.Cancel();
                ActionEditorHost.Context.Session.Edit("Persistence automatic payload", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 42);
                ActionEditorHost.Context.Session.Save(ActionEditorHost.Context.Session.Path);
                Require(!ActionEditorHost.Context.Session.IsDirty && File.ReadAllBytes(Path).SequenceEqual(committed), "Explicit save did not persist the completed edit.");
                ActionEditorHost.Context.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                Require(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version == 42 && ((FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[1]).Count == 17, "Persistent JSON reload lost fields.");
                Require(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().Sum(g => g.Tracks.Count) == 7 && ActionEditorHost.Context.Document.Length == 6, "Persistent graph changed.");
                var edited = (FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                edited.StartTime = 8; edited.Length = 3; edited.Count = 27;
                Require(ActionEditorHost.Context.Document.Length == 6, "Stale duration scenario was not reproduced.");
                ActionEditorHost.Context.Session.Save(ActionEditorHost.Context.Session.Path);
                ActionEditorHost.Context.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                Require(ActionEditorHost.Context.Document.Length == 11 && ((FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count == 27, "Public graph edit save/reload failed.");
                var random = new System.Random(1937);
                for (var iteration = 0; iteration < 8; iteration++)
                {
                    ActionEditorHost.Context.Session.Edit("Persistence fractional timeline", () =>
                    {
                        foreach (var clip in ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips))
                        {
                            clip.StartTime = (float)(random.NextDouble() * 17);
                            if (clip.CanScale()) clip.Length = (float)(random.NextDouble() * 4 + .01);
                        }
                    });
                    var times = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips)
                        .SelectMany(c => new[] { c.StartTime, c.Length }).ToArray();
                    var duration = ActionEditorHost.Context.Document.Length;
                    ActionEditorHost.Context.Session.Save(ActionEditorHost.Context.Session.Path);
                    Require(!ActionEditorHost.Context.Session.IsDirty, "Fractional save is dirty.");
                    ActionEditorHost.Context.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                    Require(ActionEditorHost.Context.Document.Length == duration && times.SequenceEqual(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks)
                        .SelectMany(t => t.Clips).SelectMany(c => new[] { c.StartTime, c.Length })), "Fractional save/reload changed time values.");
                }
                File.AppendAllText(Path, "\n ");
                var external = File.ReadAllBytes(Path);
                var rejected = false;
                try { ActionEditorHost.Context.Session.Save(ActionEditorHost.Context.Session.Path); } catch (IOException) { rejected = true; }
                Require(rejected && File.ReadAllBytes(Path).SequenceEqual(external), "External edits were overwritten.");
                return new { status = "passed", checks = new[] { "atomic create", "shared edit/save", "saved-state Undo/Redo", "discard restores saved baseline without writing", "existing-target save-as protection", "failed open preserves graph", "recovery and saved baseline", "locked target bytes and baseline preserved", "save after unlocking", "active-edit autosave suppression and explicit save", "polymorphic graph reload", "stale duration save/reload", "eight exact fractional save/reload cycles", "external edit protection" }, bytes = committed.Length, cleanup = "owned folder deleted and prior session restored before response" };
            }
            finally
            {
                try
                {
                    ActionEditorHost.Context.Open(previous); ActionEditorHost.Context.Session.RestoreRecovery(recovery);
                    if (memory != null) UnityEngine.Object.DestroyImmediate(memory);
                }
                finally { if (ownsFolder && !AssetDatabase.DeleteAsset(Root)) throw new IOException("Fixture cleanup failed: " + Root); }
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
