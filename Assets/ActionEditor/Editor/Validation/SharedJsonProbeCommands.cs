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
                schema = 1, root = Root, targets = new[] { Path },
                operations = new[] { "capture and finally restore current in-memory session including unsaved content",
                    "create isolated Shared.json from the existing sample type", "edit through common JSON session",
                    "atomic save, Undo/Redo saved-state tracking and reload", "save public graph time edits with stale derived duration and reload",
                    "eight fractional timeline edit/save/import/reload cycles with exact float preservation",
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
                memory = new TextAsset(JsonDocumentSession.Encode(new LegacyExperienceAsset()));
                App.TextAsset = memory;
                App.Session.Save(Path);
                Require(!App.Session.IsDirty, "New save is dirty.");
                App.Session.Edit("R10 payload", () =>
                {
                    ((LegacyExperienceAsset)App.AssetData).Version = 42;
                    ((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[1]).Count = 17;
                });
                App.SaveAsset();
                var committed = File.ReadAllBytes(Path);
                Require(!App.Session.IsDirty, "Saved payload is dirty.");
                Undo.PerformUndo(); Require(App.Session.IsDirty && ((LegacyExperienceAsset)App.AssetData).Version == 1, "Undo after save failed.");
                Undo.PerformRedo(); Require(!App.Session.IsDirty && ((LegacyExperienceAsset)App.AssetData).Version == 42, "Redo to saved state failed.");
                App.TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                Require(((LegacyExperienceAsset)App.AssetData).Version == 42 && ((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[1]).Count == 17, "Persistent JSON reload lost fields.");
                Require(App.AssetData.groups.Sum(g => g.Tracks.Count) == 7 && App.AssetData.Length == 6, "Persistent graph changed.");
                var edited = (LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0];
                edited.StartTime = 8; edited.Length = 3; edited.Count = 27;
                Require(App.AssetData.Length == 6, "Stale duration scenario was not reproduced.");
                App.SaveAsset();
                App.TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                Require(App.AssetData.Length == 11 && ((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0]).Count == 27, "Public graph edit save/reload failed.");
                var random = new System.Random(1937);
                for (var iteration = 0; iteration < 8; iteration++)
                {
                    App.Session.Edit("R10 fractional timeline", () =>
                    {
                        foreach (var clip in App.AssetData.groups.SelectMany(g => g.Tracks).SelectMany(t => t.Clips))
                        {
                            clip.StartTime = (float)(random.NextDouble() * 17);
                            if (clip.CanScale()) clip.Length = (float)(random.NextDouble() * 4 + .01);
                        }
                    });
                    var times = App.AssetData.groups.SelectMany(g => g.Tracks).SelectMany(t => t.Clips)
                        .SelectMany(c => new[] { c.StartTime, c.Length }).ToArray();
                    var duration = App.AssetData.Length;
                    App.SaveAsset();
                    Require(!App.Session.IsDirty, "Fractional save is dirty.");
                    App.TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
                    Require(App.AssetData.Length == duration && times.SequenceEqual(App.AssetData.groups.SelectMany(g => g.Tracks)
                        .SelectMany(t => t.Clips).SelectMany(c => new[] { c.StartTime, c.Length })), "Fractional save/reload changed time values.");
                }
                File.AppendAllText(Path, "\n ");
                var external = File.ReadAllBytes(Path);
                var rejected = false;
                try { App.SaveAsset(); } catch (IOException) { rejected = true; }
                Require(rejected && File.ReadAllBytes(Path).SequenceEqual(external), "External edits were overwritten.");
                return new { status = "passed", checks = new[] { "atomic create", "shared edit/save", "saved-state Undo/Redo", "polymorphic graph reload", "stale duration save/reload", "eight exact fractional save/reload cycles", "external edit protection" }, bytes = committed.Length, cleanup = "owned folder deleted and prior session restored before response" };
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
