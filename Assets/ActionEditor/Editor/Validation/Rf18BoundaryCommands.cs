using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using NBC.ActionEditor.Workbench;
using NBC.ActionEditor.Samples.LegacyExperience;
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
                    ActionEditorHost.PopulateSaveMenu((label, action) => { if (label == Lan.Export) open = action; });
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
        [CliGroupCommand("actioneditor.validation", "RF22 fixed tutorial replay in both real window entrypoints. New isolated sources and exports only; restore prior session and remove owned fixtures. No native OS file-dialog automation.")]
        public static async Task<object> Rf22TutorialProbe([CliArg("confirm", "Apply isolated tutorial replay.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            Ready();
            const string root = "Assets/ActionEditorValidationRF22";
            if (Directory.Exists(root) || File.Exists(root) || File.Exists(root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (App.Session.IsEditing || App.IsPlay || Resources.FindObjectsOfTypeAll<ActionEditorWindow>().Length != 0
                || Resources.FindObjectsOfTypeAll<JsonActionWorkbench>().Length != 0 || Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Length != 0
                || Resources.FindObjectsOfTypeAll<Samples.LegacyExperiencePreview>().Length != 0)
                throw new InvalidOperationException("Requires no active edit/playback or existing product/preview/export windows; never reuse user windows.");
            if (dryRun) return new { root, targets = new[] { root + "/IMGUI.json", root + "/IMGUI-Export.json", root + "/Toolkit.json", root + "/Toolkit-Export.json" },
                operations = new[] { "open both real menu methods in turn", "create preset via creation popup's shared confirmed action", "select and edit properties, move and resize using each window's gesture adapter", "Undo/Redo", "sample state preview and playback", "save via each window SaveChanges", "close/reopen same source", "open shared export menu and export independent copy", "restore prior session and delete only owned fixture folder" },
                nativeFileDialogs = "not automated; fixed new paths replace OS path input only" };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var recovery = App.Session.CaptureRecovery(); var previous = App.TextAsset; var previousHost = ActionEditorHost.Window;
            var previousWidth = ActionEditorHost.Width; var owns = false;
            ActionEditorWindow original = null; JsonActionWorkbench native = null; Samples.LegacyExperiencePreview preview = null; DocumentExportWindow export = null;
            var results = new System.Collections.Generic.List<object>();
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorValidationRF22"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                foreach (var toolkit in new[] { false, true })
                {
                    App.TextAsset = null;
                    if (toolkit) { JsonActionWorkbench.Open(); native = Resources.FindObjectsOfTypeAll<JsonActionWorkbench>().Single(); native.position = new Rect(60, 60, 1000, 600); }
                    else { ActionEditorWindow.OpenDirectorWindow(); original = Resources.FindObjectsOfTypeAll<ActionEditorWindow>().Single(); original.position = new Rect(60, 60, 1000, 600); }
                    await TutorialFrames();
                    var path = root + (toolkit ? "/Toolkit.json" : "/IMGUI.json");
                    CreateAssetWindow.CreateAndOpen(typeof(LegacyExperienceAsset), path, toolkit);
                    await TutorialFrames();
                    if (App.Session.Path != path || App.AssetData.groups.Count != 3 || App.AssetData.groups.Sum(g => g.Tracks.Count) != 7
                        || App.AssetData.groups.SelectMany(g => g.Tracks).Sum(t => t.Clips.Count) != 13 || App.Session.IsDirty
                        || (toolkit ? Resources.FindObjectsOfTypeAll<ActionEditorWindow>().Length != 0 : Resources.FindObjectsOfTypeAll<JsonActionWorkbench>().Length != 0))
                        throw new InvalidOperationException("Preset graph or originating view differs.");
                    var clip = App.AssetData.groups[0].Tracks[0].Clips[0]; App.SelectWithoutInspector(clip);
                    if (toolkit)
                    {
                        native.Select(clip); var properties = native.CreatePropertyView(); native.rootVisualElement.Add(properties);
                        properties.Query<TextField>().ToList().Single(f => f.label == "标题").value = "Tutorial";
                        properties.RemoveFromHierarchy();
                    }
                    else App.Session.Edit("Tutorial property", () => new JsonPropertyPath(clip).Field(typeof(LegacyExperienceInterval).GetField("Caption")).Write("Tutorial"));
                    if (((LegacyExperienceInterval)clip).Caption != "Tutorial") throw new InvalidOperationException("Property edit failed.");
                    await TutorialFrames();
                    for (var resize = 0; resize < 2; resize++)
                    {
                        clip = App.AssetData.groups[0].Tracks[0].Clips[0]; App.SelectWithoutInspector(clip);
                        if (toolkit)
                        {
                            var rect = native.Canvas.ClipRect(clip, 1);
                            var down = resize == 0 ? rect.center : new Vector2(rect.xMax - 2, rect.center.y);
                            var up = down + new Vector2(native.Canvas.Scale * (resize == 0 ? .25f : -.25f), 0);
                            TutorialPointer(native.Canvas, EventType.MouseDown, down); TutorialPointer(native.Canvas, EventType.MouseDrag, up); TutorialPointer(native.Canvas, EventType.MouseUp, up);
                        }
                        else
                        {
                            var down = new Vector2(Styles.TimelineLeftTotalWidth + App.AssetData.TimeToPos(resize == 0 ? clip.StartTime + .5f : clip.EndTime, ActionEditorHost.Width) - (resize == 0 ? 0 : 2), 30);
                            var up = down + new Vector2(ActionEditorHost.Width * (resize == 0 ? .25f : -.25f) / App.AssetData.ViewTime, 0);
                            TutorialOriginalPointer(original.Gesture, EventType.MouseDown, down);
                            TutorialOriginalPointer(original.Gesture, EventType.MouseDrag, up);
                            TutorialOriginalPointer(original.Gesture, EventType.MouseUp, up);
                        }
                        clip = App.AssetData.groups[0].Tracks[0].Clips[0];
                        if (Mathf.Abs(clip.StartTime - .5f) > .0001f || Mathf.Abs(clip.Length - (resize == 0 ? 1.5f : 1.25f)) > .0001f)
                            throw new InvalidOperationException("Tutorial move/resize differs: " + (toolkit ? "Toolkit" : "IMGUI") + ", step=" + resize + ", start=" + clip.StartTime + ", length=" + clip.Length);
                        await TutorialFrames();
                    }
                    var editedLength = App.AssetData.groups[0].Tracks[0].Clips[0].Length;
                    Undo.PerformUndo(); if (App.AssetData.groups[0].Tracks[0].Clips[0].Length != 1.5f) throw new InvalidOperationException("Undo failed.");
                    Undo.PerformRedo(); if (App.AssetData.groups[0].Tracks[0].Clips[0].Length != editedLength) throw new InvalidOperationException("Redo failed.");
                    preview = ScriptableObject.CreateInstance<Samples.LegacyExperiencePreview>(); preview.Show(); preview.Repaint();
                    var now = (float)EditorApplication.timeSinceStartup; App.Context.Playback.Play(now); App.Context.Playback.Update(now + .1f, 1);
                    if (!App.IsPlay || App.Context.Player.CurrentTime <= 0) throw new InvalidOperationException("Playback failed.");
                    App.Pause(); App.Context.Player.Sample(.75f); await TutorialFrames(); App.Stop(); preview.Close(); preview = null;
                    if (toolkit) native.SaveChanges(); else original.SaveChanges();
                    if (App.Session.IsDirty) throw new InvalidOperationException("Real window save failed.");
                    var expected = JsonDocumentCodec.Content(App.AssetData); var bytes = File.ReadAllBytes(path);
                    if (toolkit) { native.Close(); native = null; } else { original.Close(); original = null; }
                    App.TextAsset = null;
                    if (toolkit) { JsonActionWorkbench.Open(); native = Resources.FindObjectsOfTypeAll<JsonActionWorkbench>().Single(); }
                    else { ActionEditorWindow.OpenDirectorWindow(); original = Resources.FindObjectsOfTypeAll<ActionEditorWindow>().Single(); }
                    ActionEditorHost.OnObjectPickerConfig(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); await TutorialFrames();
                    if (App.Session.Path != path || App.Session.IsDirty || JsonDocumentCodec.Content(App.AssetData) != expected) throw new InvalidOperationException("Reopened source differs.");
                    var before = App.Session.CaptureRecovery(); var graph = App.AssetData;
                    Action openExport = null; ActionEditorHost.PopulateSaveMenu((label, action) => { if (label == Lan.Export) openExport = action; });
                    openExport(); export = Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Single(); await TutorialFrames();
                    var target = path.Replace(".json", "-Export.json"); DocumentExportWindow.ExportTo("actioneditor.json", target); export.Close(); export = null;
                    if (!ReferenceEquals(graph, App.AssetData) || App.Session.CaptureRecovery() != before || !File.ReadAllBytes(path).SequenceEqual(bytes)
                        || JsonDocumentCodec.Content(JsonDocumentCodec.Decode(File.ReadAllText(target))) != expected) throw new InvalidOperationException("Export not independent or readback differs.");
                    results.Add(new { ui = toolkit ? "UI Toolkit" : "IMGUI", source = path, exported = target, groups = 3, tracks = 7, clips = 13, start = .5f, length = 1.25f,
                        checks = "create origin/property/move/resize/Undo/Redo/playback/save/close/reopen/export readback", sourceBytes = bytes.Length });
                    if (toolkit) { native.Close(); native = null; } else { original.Close(); original = null; }
                }
                return new { status = "passed", results, limitations = "OS path/confirmation input replaced by fixed approved paths; IMGUI property uses shared path and gesture uses actual window adapter; not human acceptance." };
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("RF22 fixed replay failed: " + error.Message + "\n" + error.StackTrace, error);
            }
            finally
            {
                App.Session.CancelEdit(); App.Stop();
                if (preview != null) preview.Close(); if (export != null) export.Close();
                if (original != null) { original.DiscardChanges(); original.Close(); } if (native != null) { native.DiscardChanges(); native.Close(); }
                try { App.TextAsset = previous; App.Session.RestoreRecovery(recovery); ActionEditorHost.Window = previousHost; ActionEditorHost.Width = previousWidth; }
                finally { if (owns && !AssetDatabase.DeleteAsset(root)) throw new IOException("Owned tutorial fixture cleanup failed."); }
            }
        }
        private static async Task TutorialFrames()
        {
            for (var i = 0; i < 3; i++)
            {
                var done = new TaskCompletionSource<bool>(); EditorApplication.CallbackFunction tick = null;
                tick = () => { EditorApplication.update -= tick; done.TrySetResult(true); };
                EditorApplication.update += tick;
                try { await done.Task; } finally { EditorApplication.update -= tick; }
            }
        }
        private static void TutorialOriginalPointer(OriginalTimelineGesture gesture, EventType type, Vector2 point)
        {
            var pointer = new Events.PointerEventData(new Event { type = type, button = 0, shift = true, mousePosition = point });
            if (type == EventType.MouseDown) gesture.OnBeginDrag(pointer);
            else if (type == EventType.MouseDrag) gesture.OnDrag(pointer);
            else gesture.OnEndDrag(pointer);
        }
        private static void TutorialPointer(VisualElement target, EventType type, Vector2 point)
        {
            var source = new Event { type = type, button = 0, shift = true, mousePosition = target.LocalToWorld(point) };
            if (type == EventType.MouseDown) { using var e = PointerDownEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else if (type == EventType.MouseDrag) { using var e = PointerMoveEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else { using var e = PointerUpEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
        }
        private static string Hash(string path)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    }
}
