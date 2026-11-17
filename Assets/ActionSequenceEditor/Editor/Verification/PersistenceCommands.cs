using ActionSequenceEditor.Infrastructure.Sequences;
using ActionSequenceEditor.Model;
using System;
using System.IO;
using System.Linq;
using ActionSequenceEditor.Samples.FeatureSample;
using ActionSequenceEditor.Infrastructure.Serialization;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Verification
{
    // 验证磁盘持久化、撤销基线和外部冲突，不更改既有文件。
    public static class PersistenceCommands
    {
        private const string Root = "Assets/ActionSequenceEditorPersistenceVerification";
        private const string Path = Root + "/Shared.json";
        [CliGroupCommand("actionsequenceeditor.verification", "Fixed document persistence lifecycle. Creates only its new fixture folder; restores current session; deletes only its owned fixtures.")]
        public static object PersistenceProbe(
            [CliArg("confirm", "Apply reviewed isolated fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Preview only, without changing session or files.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Requires ready EditMode.");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture exists; refusing overwrite.");
            if (VerificationSession.Shared.Session.IsEditing || VerificationSession.Shared.Playback.IsPlaying)
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
            var recovery = VerificationSession.Shared.Session.CaptureRecovery();
            var previous = VerificationSession.Shared.Source;
            var ownsFolder = false;
            TextAsset memory = null;
            try
            {
                ownsFolder = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionSequenceEditorPersistenceVerification"));
                if (!ownsFolder) throw new IOException("Could not create fixture folder.");
                VerificationSession.Shared.CreateSequencePresenter().Create(typeof(FeatureSampleSequence), Path);
                var created = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                Require(VerificationSession.Shared.Session.Path == Path && VerificationSession.Shared.Source == created, "Create/open did not bind source.");
                memory = new TextAsset(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(new FeatureSampleSequence()));
                Require(!VerificationSession.Shared.Session.IsDirty, "New save is dirty.");
                VerificationSession.Shared.Session.Edit("Persistence payload", () =>
                {
                    ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 42;
                    ((FeatureSampleInterval)VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[1]).Count = 17;
                });
                VerificationSession.Shared.Session.Save(VerificationSession.Shared.Session.Path);
                var committed = File.ReadAllBytes(Path);
                Require(!VerificationSession.Shared.Session.IsDirty, "Saved payload is dirty.");
                Undo.PerformUndo(); Require(VerificationSession.Shared.Session.IsDirty && ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version == 1, "Undo after save failed.");
                Undo.PerformRedo(); Require(!VerificationSession.Shared.Session.IsDirty && ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version == 42, "Redo to saved state failed.");
                VerificationSession.Shared.Session.Edit("Persistence discarded payload", () => ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 99);
                VerificationSession.Shared.Session.BeginEdit(); ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 100;
                VerificationSession.Shared.Session.DiscardChanges(); VerificationSession.Shared.AutoSave.Tick(VerificationSession.SharedSettings.Current.AutoSaveIntervalSeconds);
                Require(!VerificationSession.Shared.Session.IsDirty && !VerificationSession.Shared.Session.IsEditing && ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version == 42
                    && File.ReadAllBytes(Path).SequenceEqual(committed), "Discard did not restore saved baseline without writes.");
                Undo.PerformUndo(); Undo.PerformRedo();
                Require(((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version == 42, "Discarded edits returned through Undo/Redo.");
                using (var other = ActionSequenceEditor.Composition.SequenceComposition.Create())
                {
                    other.Open(new SequenceFileRepository().ReadSource(memory));
                    var refused = false;
                    try { other.Save(Path); } catch (IOException) { refused = true; }
                    Require(refused && string.IsNullOrEmpty(other.Path) && File.ReadAllBytes(Path).SequenceEqual(committed), "Save-as overwrote an existing fixture.");
                }
                var beforeOpen = VerificationSession.Shared.Sequence;
                var invalidText = new TextAsset("{\"format\":\"actioneditor.json\",\"version\":1,\"document\":null}");
                try
                {
                    var refused = false;
                    try { VerificationSession.Shared.Session.Open(new SequenceFileRepository().ReadSource(invalidText)); } catch (InvalidDataException) { refused = true; }
                    Require(refused && ReferenceEquals(VerificationSession.Shared.Sequence, beforeOpen), "Failed open replaced the current graph.");
                }
                finally { UnityEngine.Object.DestroyImmediate(invalidText); }
                VerificationSession.Shared.Session.Edit("Persistence recovery payload", () => ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 88);
                using (var restored = ActionSequenceEditor.Composition.SequenceComposition.Create())
                {
                    restored.RestoreRecovery(VerificationSession.Shared.Session.CaptureRecovery());
                    Require(restored.IsDirty && ((FeatureSampleSequence)restored.Sequence).Version == 88, "Recovery lost unsaved content.");
                    restored.DiscardChanges();
                    Require(!restored.IsDirty && restored.Path == Path && ((FeatureSampleSequence)restored.Sequence).Version == 42, "Recovery lost saved baseline.");
                }
                VerificationSession.Shared.Session.DiscardChanges();
                var alternate = Root + "/Alternate.json";
                VerificationSession.Shared.Session.Save(alternate);
                Require(VerificationSession.Shared.Session.Path == alternate && AssetDatabase.GetAssetPath(VerificationSession.Shared.Source) == alternate && !VerificationSession.Shared.Session.IsDirty, "Save-as did not switch source and baseline.");
                VerificationSession.Shared.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                VerificationSession.Shared.Session.Edit("Persistence locked payload", () => ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 43);
                using (var locked = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var refused = false;
                    try { VerificationSession.Shared.Session.Save(VerificationSession.Shared.Session.Path); } catch (IOException) { refused = true; }
                    Require(refused && VerificationSession.Shared.Session.IsDirty && VerificationSession.Shared.Session.Path == Path && File.ReadAllBytes(Path).SequenceEqual(committed), "Locked save changed bytes or baseline.");
                }
                VerificationSession.Shared.Session.Save(VerificationSession.Shared.Session.Path); Require(!VerificationSession.Shared.Session.IsDirty, "Save after unlocking failed.");
                VerificationSession.Shared.Session.DiscardChanges(); Require(((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version == 43, "Successful save did not advance baseline.");
                var active = VerificationSession.Shared.Session.BeginEdit(new object());
                ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 44;
                var beforeAuto = File.ReadAllBytes(Path);
                VerificationSession.Shared.AutoSave.Tick(VerificationSession.SharedSettings.Current.AutoSaveIntervalSeconds);
                Require(active.IsActive && File.ReadAllBytes(Path).SequenceEqual(beforeAuto), "Automatic save wrote an active gesture.");
                active.Cancel();
                VerificationSession.Shared.Session.Edit("Persistence automatic payload", () => ((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 42);
                VerificationSession.Shared.Session.Save(VerificationSession.Shared.Session.Path);
                Require(!VerificationSession.Shared.Session.IsDirty && File.ReadAllBytes(Path).SequenceEqual(committed), "Explicit save did not persist the completed edit.");
                VerificationSession.Shared.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                Require(((FeatureSampleSequence)VerificationSession.Shared.Sequence).Version == 42 && ((FeatureSampleInterval)VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[1]).Count == 17, "Persistent JSON reload lost fields.");
                Require(VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().Sum(g => g.Tracks.Count) == 7 && VerificationSession.Shared.Sequence.Length == 6, "Persistent graph changed.");
                var edited = (FeatureSampleInterval)VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                edited.StartTime = 8; edited.Length = 3; edited.Count = 27;
                Require(VerificationSession.Shared.Sequence.Length == 6, "Stale duration scenario was not reproduced.");
                VerificationSession.Shared.Session.Save(VerificationSession.Shared.Session.Path);
                VerificationSession.Shared.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                Require(VerificationSession.Shared.Sequence.Length == 11 && ((FeatureSampleInterval)VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count == 27, "Public graph edit save/reload failed.");
                var random = new System.Random(1937);
                for (var iteration = 0; iteration < 8; iteration++)
                {
                    VerificationSession.Shared.Session.Edit("Persistence fractional timeline", () =>
                    {
                        foreach (var clip in VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips))
                        {
                            clip.StartTime = (float)(random.NextDouble() * 17);
                            if (clip.CanScale()) clip.Length = (float)(random.NextDouble() * 4 + .01);
                        }
                    });
                    var times = VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips)
                        .SelectMany(c => new[] { c.StartTime, c.Length }).ToArray();
                    var duration = VerificationSession.Shared.Sequence.Length;
                    VerificationSession.Shared.Session.Save(VerificationSession.Shared.Session.Path);
                    Require(!VerificationSession.Shared.Session.IsDirty, "Fractional save is dirty.");
                    VerificationSession.Shared.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(Path));
                    Require(VerificationSession.Shared.Sequence.Length == duration && times.SequenceEqual(VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().SelectMany(g => g.Tracks)
                        .SelectMany(t => t.Clips).SelectMany(c => new[] { c.StartTime, c.Length })), "Fractional save/reload changed time values.");
                }
                File.AppendAllText(Path, "\n ");
                var external = File.ReadAllBytes(Path);
                var rejected = false;
                try { VerificationSession.Shared.Session.Save(VerificationSession.Shared.Session.Path); } catch (IOException) { rejected = true; }
                Require(rejected && File.ReadAllBytes(Path).SequenceEqual(external), "External edits were overwritten.");
                return new { status = "passed", checks = new[] { "atomic create", "shared edit/save", "saved-state Undo/Redo", "discard restores saved baseline without writing", "existing-target save-as protection", "failed open preserves graph", "recovery and saved baseline", "locked target bytes and baseline preserved", "save after unlocking", "active-edit autosave suppression and explicit save", "polymorphic graph reload", "stale duration save/reload", "eight exact fractional save/reload cycles", "external edit protection" }, bytes = committed.Length, cleanup = "owned folder deleted and prior session restored before response" };
            }
            finally
            {
                try
                {
                    VerificationSession.Shared.Open(previous); VerificationSession.Shared.Session.RestoreRecovery(recovery);
                    if (memory != null) UnityEngine.Object.DestroyImmediate(memory);
                }
                finally { if (ownsFolder && !AssetDatabase.DeleteAsset(Root)) throw new IOException("Fixture cleanup failed: " + Root); }
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
