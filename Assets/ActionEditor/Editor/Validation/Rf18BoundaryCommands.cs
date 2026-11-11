using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Storage;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Validation
{
    public static class Rf18BoundaryCommands
    {
        private const string Source = "Assets/ActionEditor/Runtime/Utility/FullSerializerExtensions.cs";
        private const string Destination = "Assets/ActionEditor/Runtime/Serialization/FullSerializerExtensions.cs";
        private const string Guid = "107863859ba94b9580581de9746adeef";
        [CliGroupCommand("actioneditor.validation", "Fixed RF18 serializer source move, retaining GUID and bytes. No overwrite or deletion. Regenerate Unity solution.")]
        public static object Rf18Move([CliArg("confirm", "Apply fixed move.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            Ready();
            if (!File.Exists(Source) || AssetDatabase.AssetPathToGUID(Source) != Guid || File.Exists(Destination)
                || File.Exists(Destination + ".meta") || !AssetDatabase.IsValidFolder("Assets/ActionEditor/Runtime/Serialization"))
                throw new IOException("Fixed RF18 move state changed.");
            var bytes = Hash(Source); var meta = Hash(Source + ".meta");
            if (dryRun) return new { source = Source, destination = Destination, guid = Guid, sha256 = bytes, metaSha256 = meta, regenerateProjects = true };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var error = AssetDatabase.MoveAsset(Source, Destination);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            if (AssetDatabase.AssetPathToGUID(Destination) != Guid || Hash(Destination) != bytes || Hash(Destination + ".meta") != meta)
                throw new IOException("RF18 move identity mismatch; inspect, do not replay.");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            return new { status = "passed", guid = Guid, bytesPreserved = true, projectsRegenerated = true };
        }
        private const string Root = "Assets/ActionEditorValidationRF18";
        [Serializable] private sealed class ReferenceAsset : Asset { public Texture2D Main, Sub; }
        [CliGroupCommand("actioneditor.validation", "RF18 isolated main/subasset reference persistence. Only creates a new fixed fixture folder; always cleans owned fixtures. No current-session changes.")]
        public static object Rf18ReferenceProbe([CliArg("confirm", "Apply fixed isolated fixture.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            Ready();
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (dryRun) return new { root = Root, targets = new[] { Root + "/References.asset", Root + "/Document.json" },
                operations = new[] { "create main and subasset Texture2D", "write unique-format JSON", "unload and resolve GUID/localFileId", "reject missing/type-mismatched reference", "delete only owned new fixture folder" }, changesCurrentSession = false };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var owns = false;
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorValidationRF18"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                var main = new Texture2D(2, 2) { name = "Main" }; var sub = new Texture2D(1, 1) { name = "Sub" };
                AssetDatabase.CreateAsset(main, Root + "/References.asset"); AssetDatabase.AddObjectToAsset(sub, main);
                EditorUtility.SetDirty(main); EditorUtility.SetDirty(sub); AssetDatabase.SaveAssetIfDirty(main);
                AssetDatabase.ImportAsset(Root + "/References.asset", ImportAssetOptions.ForceSynchronousImport);
                var context = DocumentFormats.CreateContext(typeof(ReferenceAsset)); var format = new ActionDocumentJsonFormat();
                var bytes = format.Serialize(new ReferenceAsset { Main = main, Sub = sub }, default, context);
                File.WriteAllBytes(Root + "/Document.json", bytes);
                var resolver = new AssetReferenceResolver(); var mainId = resolver.Identify(main, typeof(Texture2D)); var subId = resolver.Identify(sub, typeof(Texture2D));
                if (mainId.LocalFileId == subId.LocalFileId || mainId.Guid != subId.Guid) throw new InvalidOperationException("Subasset identity not distinct.");
                Resources.UnloadAsset(sub); Resources.UnloadAsset(main);
                var restored = (ReferenceAsset)format.Deserialize(File.ReadAllBytes(Root + "/Document.json"), context);
                if (resolver.Identify(restored.Main, typeof(Texture2D)).LocalFileId != mainId.LocalFileId
                    || resolver.Identify(restored.Sub, typeof(Texture2D)).LocalFileId != subId.LocalFileId) throw new InvalidOperationException("Reference reload lost identity.");
                var refused = false;
                try { resolver.Resolve(mainId, typeof(AudioClip)); } catch (InvalidDataException) { refused = true; }
                if (!refused) throw new InvalidOperationException("Type mismatch accepted.");
                if (!AssetDatabase.DeleteAsset(Root + "/References.asset")) throw new IOException("Owned fixture deletion failed.");
                refused = false;
                try { format.Deserialize(bytes, context); } catch (InvalidDataException) { refused = true; }
                if (!refused) throw new InvalidOperationException("Missing reference accepted.");
                return new { status = "passed", checks = new[] { "main/subasset GUID+localFileId", "disk bytes reload after unload", "wrong type rejected", "missing reference rejected" } };
            }
            finally { if (owns && !AssetDatabase.DeleteAsset(Root)) throw new IOException("Owned fixture cleanup failed."); }
        }
        [CliGroupCommand("actioneditor.validation", "Regenerate current Unity IDE solution only; no source or settings changes.")]
        public static object SyncProjects([CliArg("confirm", "Regenerate generated project files.")] bool confirm = false,
            [CliArg("dry-run", "Preview only.")] bool dryRun = false)
        {
            Ready();
            if (dryRun) return new { operation = "Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll", scope = "generated solution and project files only" };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll(); return new { status = "passed" };
        }
        private static void Ready()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Requires ready EditMode.");
        }
        [CliGroupCommand("actioneditor.validation", "RF20 fixed export lifecycle, both UI shared save-menu callbacks and independent new JSON targets. Restores prior session and cleans only owned fixtures.")]
        public static object Rf20ExportProbe([CliArg("confirm", "Apply isolated export lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            Ready();
            const string root = "Assets/ActionEditorValidationRF20";
            if (Directory.Exists(root) || File.Exists(root) || File.Exists(root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (App.Session.IsEditing || App.IsPlay) throw new InvalidOperationException("Requires no active edit/playback in previous session.");
            if (dryRun) return new { root, targets = new[] { root + "/Source.json", root + "/Clean.json", root + "/Dirty.json" },
                operations = new[] { "capture/restore prior session", "create isolated source", "open shared export window from IMGUI/Toolkit save menu", "export clean/dirty new JSON and read back", "verify source identity, content, baseline, Undo, selection and paused playback unchanged", "reject source alias, existing target and active gesture", "delete only owned fixture folder" } };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var recovery = App.Session.CaptureRecovery(); var previous = App.TextAsset; var owns = false;
            DocumentExportWindow window = null;
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorValidationRF20"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                App.TextAsset = DocumentCreationService.Create(typeof(Samples.LegacyExperience.LegacyExperienceAsset), root + "/Source.json");
                var sourceBytes = File.ReadAllBytes(App.Session.Path);
                for (var dirty = 0; dirty < 2; dirty++)
                {
                    if (dirty != 0) App.Session.Edit("RF20 business", () => ((Samples.LegacyExperience.LegacyExperienceAsset)App.AssetData).Version = 9);
                    App.SelectWithoutInspector(App.AssetData.groups[0].Tracks[0].Clips[0]);
                    App.Context.Player.Sample(.5f); App.Play(); App.Pause();
                    var source = App.AssetData; var selection = App.FistSelect; var before = App.Session.CaptureRecovery(); var path = App.Session.Path;
                    Action open = null;
                    ActionEditorHost.PopulateSaveMenu((label, action) => { if (label == "导出…") open = action; });
                    if (open == null) throw new InvalidOperationException("Both UI shared menu missing export.");
                    open(); window = Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Single();
                    var target = root + (dirty == 0 ? "/Clean.json" : "/Dirty.json");
                    if (DocumentExportWindow.ExportTo("actioneditor.json", null)) throw new InvalidOperationException("Cancel exported a file.");
                    DocumentExportWindow.ExportTo("actioneditor.json", target);
                    window.Close(); window = null;
                    var loaded = JsonDocumentCodec.Decode(File.ReadAllText(target));
                    if (((Samples.LegacyExperience.LegacyExperienceAsset)loaded).Version != (dirty == 0 ? 1 : 9)) throw new InvalidOperationException("Export lost business content.");
                    foreach (var rejected in new[] { target, root.ToLowerInvariant() + "/./Source.JSON" })
                    {
                        var refused = false;
                        try { DocumentExportWindow.ExportTo("actioneditor.json", rejected); } catch (Exception error) when (error is IOException || error is ArgumentException) { refused = true; }
                        if (!refused) throw new InvalidOperationException("Unsafe export target accepted.");
                    }
                    if (!ReferenceEquals(source, App.AssetData) || App.Session.CaptureRecovery() != before || path != App.Session.Path
                        || !ReferenceEquals(selection, App.FistSelect) || !App.IsPlay || !App.IsPause || App.Context.Player.CurrentTime != .5f
                        || !File.ReadAllBytes(path).SequenceEqual(sourceBytes)) throw new InvalidOperationException("Export changed source/session/selection/playback.");
                    App.Stop();
                }
                Undo.PerformUndo();
                if (((Samples.LegacyExperience.LegacyExperienceAsset)App.AssetData).Version != 1) throw new InvalidOperationException("Export changed Undo.");
                Undo.PerformRedo();
                if (((Samples.LegacyExperience.LegacyExperienceAsset)App.AssetData).Version != 9) throw new InvalidOperationException("Export changed Redo.");
                var edit = App.Session.BeginEdit(new object());
                var refusedEdit = false;
                try { DocumentExportWindow.ExportTo("actioneditor.json", root + "/Active.json"); } catch (InvalidOperationException) { refusedEdit = true; }
                if (!refusedEdit || !edit.IsActive || File.Exists(root + "/Active.json")) throw new InvalidOperationException("Export ended active gesture.");
                edit.Cancel();
                return new { status = "passed", checks = new[] { "clean/dirty exact readback", "export window/menu entry", "cancel no writes", "existing/source alias rejected", "active edit remains owned", "source/baseline/selection/paused playback/Undo unchanged" } };
            }
            finally
            {
                if (window != null) window.Close();
                try { App.Stop(); App.TextAsset = previous; App.Session.RestoreRecovery(recovery); }
                finally { if (owns && !AssetDatabase.DeleteAsset(root)) throw new IOException("Owned fixture cleanup failed."); }
            }
        }
        private static string Hash(string path)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    }
}
