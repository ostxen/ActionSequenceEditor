using NBC.ActionEditor.Domain;
using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Samples.LegacyExperience;
using NBC.ActionEditor.Storage;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Validation
{
    public static class SharedJsonProbeCommands
    {
        private const string Root = "Assets/ActionEditorValidationR10";
        private const string Path = Root + "/Shared.json";
        [CliGroupCommand("actioneditor.validation", "Fixed R10 shared JSON persistence lifecycle. Creates only its new fixture folder; restores current session; deletes only its owned fixtures.")]
        public static object SharedJsonProbe(
            [CliArg("confirm", "Apply reviewed isolated fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Preview only, without changing session or files.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Requires ready EditMode.");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture exists; refusing overwrite.");
            if (dryRun) return new
            {
                schema = 1, root = Root, targets = new[] { Path, Root + "/Alternate.json" },
                operations = new[] { "capture and finally restore current in-memory session including unsaved content",
                    "create and open isolated Shared.json through DocumentCreationService", "edit through common JSON session",
                    "save-as to new Alternate.json and verify current source switches, then reopen Shared.json",
                    "atomic save, Undo/Redo saved-state tracking and reload", "save public graph time edits with stale derived duration and reload",
                    "discard committed and pending edits to the last saved baseline without writing bytes or losing disk revision",
                    "eight fractional timeline edit/save/import/reload cycles with exact float preservation",
                    "reject save-as onto existing fixture, preserve current graph on failed open and restore recovery baseline",
                    "reject locked fixture replacement without changing bytes or baseline, then save after unlocking",
                    "verify automatic save entry and suppression during an owned edit",
                    "reject externally modified target without overwriting it",
                    "delete only this newly created fixture folder" },
                touchesExistingAssets = false, changesPackagesOrSettings = false
            };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var recovery = App.Session.CaptureRecovery();
            var previous = App.TextAsset;
            var ownsFolder = false;
            TextAsset memory = null;
            try
            {
                ownsFolder = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorValidationR10"));
                if (!ownsFolder) throw new IOException("Could not create fixture folder.");
                var created = DocumentCreationService.Create(typeof(LegacyExperienceAsset), Path);
                App.TextAsset = created;
                Require(App.Session.Path == Path && App.TextAsset == created, "Create/open did not bind source.");
                memory = new TextAsset(JsonDocumentSession.Encode(new LegacyExperienceAsset()));
                Require(!App.Session.IsDirty, "New save is dirty.");
                App.Session.Edit("R10 payload", () =>
                {
                    ((LegacyExperienceAsset)App.AssetData).Version = 42;
                    ((LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[1]).Count = 17;
                });
                App.SaveAsset();
                var committed = File.ReadAllBytes(Path);
                Require(!App.Session.IsDirty, "Saved payload is dirty.");
                Undo.PerformUndo(); Require(App.Session.IsDirty && ((LegacyExperienceAsset)App.AssetData).Version == 1, "Undo after save failed.");
                Undo.PerformRedo(); Require(!App.Session.IsDirty && ((LegacyExperienceAsset)App.AssetData).Version == 42, "Redo to saved state failed.");
                App.Session.Edit("R10 discarded payload", () => ((LegacyExperienceAsset)App.AssetData).Version = 99);
                App.Session.BeginEdit(); ((LegacyExperienceAsset)App.AssetData).Version = 100;
                App.Session.DiscardChanges(); App.TryAutoSave();
                Require(!App.Session.IsDirty && !App.Session.IsEditing && ((LegacyExperienceAsset)App.AssetData).Version == 42
                    && File.ReadAllBytes(Path).SequenceEqual(committed), "Discard did not restore saved baseline without writes.");
                Undo.PerformUndo(); Undo.PerformRedo();
                Require(((LegacyExperienceAsset)App.AssetData).Version == 42, "Discarded edits returned through Undo/Redo.");
                using (var other = new JsonDocumentSession())
                {
                    other.Open(memory);
                    var refused = false;
                    try { other.Save(Path); } catch (IOException) { refused = true; }
                    Require(refused && string.IsNullOrEmpty(other.Path) && File.ReadAllBytes(Path).SequenceEqual(committed), "Save-as overwrote an existing fixture.");
                }
                var beforeOpen = App.AssetData;
                var invalidText = new TextAsset("{\"format\":\"actioneditor.json\",\"version\":1,\"document\":null}");
                try
                {
                    var refused = false;
                    try { App.Session.Open(invalidText); } catch (InvalidDataException) { refused = true; }
                    Require(refused && ReferenceEquals(App.AssetData, beforeOpen), "Failed open replaced the current graph.");
                }
                finally { UnityEngine.Object.DestroyImmediate(invalidText); }
                App.Session.Edit("R10 recovery payload", () => ((LegacyExperienceAsset)App.AssetData).Version = 88);
                using (var restored = new JsonDocumentSession())
                {
                    restored.RestoreRecovery(App.Session.CaptureRecovery());
                    Require(restored.IsDirty && ((LegacyExperienceAsset)restored.Document).Version == 88, "Recovery lost unsaved content.");
                    restored.DiscardChanges();
                    Require(!restored.IsDirty && restored.Path == Path && ((LegacyExperienceAsset)restored.Document).Version == 42, "Recovery lost saved baseline.");
                }
                App.Session.DiscardChanges();
                var alternate = Root + "/Alternate.json";
                App.Session.Save(alternate);
                Require(App.Session.Path == alternate && AssetDatabase.GetAssetPath(App.TextAsset) == alternate && !App.Session.IsDirty, "Save-as did not switch source and baseline.");
                App.TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                App.Session.Edit("R10 locked payload", () => ((LegacyExperienceAsset)App.AssetData).Version = 43);
                using (var locked = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var refused = false;
                    try { App.SaveAsset(); } catch (IOException) { refused = true; }
                    Require(refused && App.Session.IsDirty && App.Session.Path == Path && File.ReadAllBytes(Path).SequenceEqual(committed), "Locked save changed bytes or baseline.");
                }
                App.SaveAsset(); Require(!App.Session.IsDirty, "Save after unlocking failed.");
                App.Session.DiscardChanges(); Require(((LegacyExperienceAsset)App.AssetData).Version == 43, "Successful save did not advance baseline.");
                var active = App.Session.BeginEdit(new object());
                ((LegacyExperienceAsset)App.AssetData).Version = 44;
                var beforeAuto = File.ReadAllBytes(Path);
                App.TryAutoSave();
                Require(active.IsActive && File.ReadAllBytes(Path).SequenceEqual(beforeAuto), "Automatic save wrote an active gesture.");
                active.Cancel();
                App.Session.Edit("R10 automatic payload", () => ((LegacyExperienceAsset)App.AssetData).Version = 42);
                App.AutoSave();
                Require(!App.Session.IsDirty && File.ReadAllBytes(Path).SequenceEqual(committed), "Automatic save entry did not persist the completed edit.");
                App.TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                Require(((LegacyExperienceAsset)App.AssetData).Version == 42 && ((LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[1]).Count == 17, "Persistent JSON reload lost fields.");
                Require(App.AssetData.RootItems.OfType<TimelineGroup>().Sum(g => g.Tracks.Count) == 7 && App.AssetData.Length == 6, "Persistent graph changed.");
                var edited = (LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                edited.StartTime = 8; edited.Length = 3; edited.Count = 27;
                Require(App.AssetData.Length == 6, "Stale duration scenario was not reproduced.");
                App.SaveAsset();
                App.TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                Require(App.AssetData.Length == 11 && ((LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count == 27, "Public graph edit save/reload failed.");
                var random = new System.Random(1937);
                for (var iteration = 0; iteration < 8; iteration++)
                {
                    App.Session.Edit("R10 fractional timeline", () =>
                    {
                        foreach (var clip in App.AssetData.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips))
                        {
                            clip.StartTime = (float)(random.NextDouble() * 17);
                            if (clip.CanScale()) clip.Length = (float)(random.NextDouble() * 4 + .01);
                        }
                    });
                    var times = App.AssetData.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips)
                        .SelectMany(c => new[] { c.StartTime, c.Length }).ToArray();
                    var duration = App.AssetData.Length;
                    App.SaveAsset();
                    Require(!App.Session.IsDirty, "Fractional save is dirty.");
                    App.TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                    Require(App.AssetData.Length == duration && times.SequenceEqual(App.AssetData.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks)
                        .SelectMany(t => t.Clips).SelectMany(c => new[] { c.StartTime, c.Length })), "Fractional save/reload changed time values.");
                }
                File.AppendAllText(Path, "\n ");
                var external = File.ReadAllBytes(Path);
                var rejected = false;
                try { App.SaveAsset(); } catch (IOException) { rejected = true; }
                Require(rejected && File.ReadAllBytes(Path).SequenceEqual(external), "External edits were overwritten.");
                return new { status = "passed", checks = new[] { "atomic create", "shared edit/save", "saved-state Undo/Redo", "discard restores saved baseline without writing", "existing-target save-as protection", "failed open preserves graph", "recovery and saved baseline", "locked target bytes and baseline preserved", "save after unlocking", "automatic save entry and active-edit suppression", "polymorphic graph reload", "stale duration save/reload", "eight exact fractional save/reload cycles", "external edit protection" }, bytes = committed.Length, cleanup = "owned folder deleted and prior session restored before response" };
            }
            finally
            {
                try
                {
                    App.TextAsset = previous; App.Session.RestoreRecovery(recovery);
                    if (memory != null) UnityEngine.Object.DestroyImmediate(memory);
                }
                finally { if (ownsFolder && !AssetDatabase.DeleteAsset(Root)) throw new IOException("Fixture cleanup failed: " + Root); }
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
