using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using NBC.ActionEditor.Domain;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Verification
{
    // 通过两个真实窗口入口重放当前样例操作链。
    public static class TutorialCommands
    {
        [CliGroupCommand("actioneditor.verification", "Fixed tutorial replay in both real window entrypoints. New isolated sources and exports only; restore prior session and remove owned fixtures. No native OS file-dialog automation.")]
        public static async Task<object> TutorialProbe([CliArg("confirm", "Apply isolated tutorial replay.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            VerificationGuard.Ready();
            const string root = "Assets/ActionEditorTutorialVerification";
            if (Directory.Exists(root) || File.Exists(root) || File.Exists(root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (VerificationSession.Shared.Session.IsEditing || VerificationSession.Shared.Playback.IsPlaying || Resources.FindObjectsOfTypeAll<ImguiWindow>().Length != 0
                || Resources.FindObjectsOfTypeAll<ToolkitWindow>().Length != 0 || Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Length != 0
                || Resources.FindObjectsOfTypeAll<Samples.FeatureSamplePreview>().Length != 0)
                throw new InvalidOperationException("Requires no active edit/playback or existing product/preview/export windows; never reuse user windows.");
            if (dryRun) return new { root, targets = new[] { root + "/IMGUI.json", root + "/IMGUI-Export.json", root + "/Toolkit.json", root + "/Toolkit-Export.json" },
                operations = new[] { "open both real menu methods in turn", "create preset via creation popup's shared confirmed action", "select and edit properties, move and resize using each window's gesture adapter", "Undo/Redo", "sample state preview and playback", "save via each window SaveChanges", "close/reopen same source", "open shared export menu and export independent copy", "restore prior session and delete only owned fixture folder" },
                nativeFileDialogs = "not automated; fixed new paths replace OS path input only" };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var recovery = VerificationSession.Shared.Session.CaptureRecovery(); var previous = VerificationSession.Shared.Source;
            var owns = false;
            ImguiWindow imgui = null; ToolkitWindow toolkitWindow = null; Samples.FeatureSamplePreview preview = null; DocumentExportWindow export = null;
            var results = new System.Collections.Generic.List<object>();
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorTutorialVerification"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                foreach (var toolkit in new[] { false, true })
                {
                    VerificationSession.Shared.Open(null);
                    if (toolkit) { ToolkitWindow.Open(); toolkitWindow = Resources.FindObjectsOfTypeAll<ToolkitWindow>().Single(); toolkitWindow.position = new Rect(60, 60, 1000, 600); }
                    else { ImguiWindow.Open(); imgui = Resources.FindObjectsOfTypeAll<ImguiWindow>().Single(); imgui.position = new Rect(60, 60, 1000, 600); }
                    await TutorialFrames();
                    var path = root + (toolkit ? "/Toolkit.json" : "/IMGUI.json");
                    VerificationSession.Shared.CreateDocuments().Create(typeof(FeatureSampleDocument), path);
                    await TutorialFrames();
                    if (VerificationSession.Shared.Session.Path != path || VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().Count() != 3 || VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().Sum(g => g.Tracks.Count) != 7
                        || VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).Sum(t => t.Clips.Count) != 13 || VerificationSession.Shared.Session.IsDirty
                        || (toolkit ? Resources.FindObjectsOfTypeAll<ImguiWindow>().Length != 0 : Resources.FindObjectsOfTypeAll<ToolkitWindow>().Length != 0))
                        throw new InvalidOperationException("Preset graph or originating view differs.");
                    var clip = VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]; VerificationSession.Shared.Selection.Set(new IDirectable[] { clip }, false);
                    if (toolkit)
                    {
                        toolkitWindow.Select(clip); var properties = toolkitWindow.CreatePropertyView(); toolkitWindow.rootVisualElement.Add(properties);
                        properties.Query<TextField>().ToList().Single(f => f.label == "标题").value = "Tutorial";
                        properties.RemoveFromHierarchy();
                    }
                    else VerificationSession.Shared.Session.Edit("Tutorial property", () => new NBC.ActionEditor.Application.Editing.PropertyPath(VerificationSession.Shared.Properties, clip).Field(typeof(FeatureSampleInterval).GetField("Caption")).Write("Tutorial"));
                    if (((FeatureSampleInterval)clip).Caption != "Tutorial") throw new InvalidOperationException("Property edit failed.");
                    await TutorialFrames();
                    for (var resize = 0; resize < 2; resize++)
                    {
                        clip = VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]; VerificationSession.Shared.Selection.Set(new IDirectable[] { clip }, false);
                        if (toolkit)
                        {
                            var rect = toolkitWindow.Canvas.ClipRect(clip, 1);
                            var down = resize == 0 ? rect.center : new Vector2(rect.xMax - 2, rect.center.y);
                            var up = down + new Vector2(toolkitWindow.Canvas.Scale * (resize == 0 ? .25f : -.25f), 0);
                            TutorialPointer(toolkitWindow.Canvas, EventType.MouseDown, down); TutorialPointer(toolkitWindow.Canvas, EventType.MouseDrag, up); TutorialPointer(toolkitWindow.Canvas, EventType.MouseUp, up);
                        }
                        else
                        {
                            imgui.Focus(); imgui.Repaint(); await TutorialFrames();
                            var state = imgui.Timeline.State;
                            var rect = state.Clips.Single(c => c.Key == imgui.Presenter.KeyFor(clip)).Bounds;
                            var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(imgui);
                            var down = new Vector2(resize == 0 ? rect.X + rect.Width * .5f : rect.Right - 2, rect.Y + rect.Height * .5f + 40) + offset;
                            var up = down + new Vector2((state.Size.WindowWidth - state.ContentLeft) * (resize == 0 ? .25f : -.25f) / imgui.Workspace.ViewTime, 0);
                            foreach (var kind in new[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp })
                                imgui.SendEvent(new Event { type = kind, button = 0, shift = true, mousePosition = kind == EventType.MouseDown ? down : up });
                            if (imgui.Timeline.Error != null) throw new InvalidOperationException(imgui.Timeline.Error);
                        }
                        clip = VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                        if (Mathf.Abs(clip.StartTime - .5f) > .0001f || Mathf.Abs(clip.Length - (resize == 0 ? 1.5f : 1.25f)) > .0001f)
                            throw new InvalidOperationException("Tutorial move/resize differs: " + (toolkit ? "Toolkit" : "IMGUI") + ", step=" + resize + ", start=" + clip.StartTime + ", length=" + clip.Length);
                        await TutorialFrames();
                    }
                    var editedLength = VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].Length;
                    Undo.PerformUndo(); if (VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].Length != 1.5f) throw new InvalidOperationException("Undo failed.");
                    Undo.PerformRedo(); if (VerificationSession.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].Length != editedLength) throw new InvalidOperationException("Redo failed.");
                    preview = ScriptableObject.CreateInstance<Samples.FeatureSamplePreview>();
                    preview.Bind(new EditorExtensionContext(new NBC.ActionEditor.Presentation.Presenters.ExtensionDocumentPresenter(() => VerificationSession.Shared.Document, () => VerificationSession.Shared.Player.CurrentTime).Capture)); preview.Show(); preview.Repaint();
                    var now = (float)EditorApplication.timeSinceStartup; VerificationSession.Shared.Playback.Play(now); VerificationSession.Shared.Playback.Update(now + .1f, 1);
                    if (!VerificationSession.Shared.Playback.IsPlaying || VerificationSession.Shared.Player.CurrentTime <= 0) throw new InvalidOperationException("Playback failed.");
                    VerificationSession.Shared.Playback.Pause(true); VerificationSession.Shared.Player.Sample(.75f); await TutorialFrames(); VerificationSession.Shared.Playback.Stop(); preview.Close(); preview = null;
                    if (toolkit) toolkitWindow.SaveChanges(); else imgui.SaveChanges();
                    if (VerificationSession.Shared.Session.IsDirty) throw new InvalidOperationException("Real window save failed.");
                    var expected = JsonDocumentCodec.Content(VerificationSession.Shared.Document); var bytes = File.ReadAllBytes(path);
                    if (toolkit) { toolkitWindow.Close(); toolkitWindow = null; } else { imgui.Close(); imgui = null; }
                    VerificationSession.Shared.Open(null);
                    if (toolkit) { ToolkitWindow.Open(); toolkitWindow = Resources.FindObjectsOfTypeAll<ToolkitWindow>().Single(); }
                    else { ImguiWindow.Open(); imgui = Resources.FindObjectsOfTypeAll<ImguiWindow>().Single(); }
                    VerificationSession.Shared.CreateDocuments().OpenPicked(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); await TutorialFrames();
                    if (VerificationSession.Shared.Session.Path != path || VerificationSession.Shared.Session.IsDirty || JsonDocumentCodec.Content(VerificationSession.Shared.Document) != expected) throw new InvalidOperationException("Reopened source differs.");
                    var before = VerificationSession.Shared.Session.CaptureRecovery(); var graph = VerificationSession.Shared.Document;
                    Action openExport = null; Composition.DocumentPresenterComposition.PopulateSaveMenu(VerificationSession.Shared.CreateDocuments(), (label, action) => { if (label == Lan.Export) openExport = action; });
                    openExport(); export = Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Single(); await TutorialFrames();
                    var target = path.Replace(".json", "-Export.json"); export.ExportTo("actioneditor.json", target); export.Close(); export = null;
                    if (!ReferenceEquals(graph, VerificationSession.Shared.Document) || VerificationSession.Shared.Session.CaptureRecovery() != before || !File.ReadAllBytes(path).SequenceEqual(bytes)
                        || JsonDocumentCodec.Content(JsonDocumentCodec.Decode(File.ReadAllText(target))) != expected) throw new InvalidOperationException("Export not independent or readback differs.");
                    results.Add(new { ui = toolkit ? "UI Toolkit" : "IMGUI", source = path, exported = target, groups = 3, tracks = 7, clips = 13, start = .5f, length = 1.25f,
                        checks = "create origin/property/move/resize/Undo/Redo/playback/save/close/reopen/export readback", sourceBytes = bytes.Length });
                    if (toolkit) { toolkitWindow.Close(); toolkitWindow = null; } else { imgui.Close(); imgui = null; }
                }
                return new { status = "passed", results, limitations = "OS path/confirmation input replaced by fixed approved paths; IMGUI property uses shared path and gesture uses actual window adapter; not human acceptance." };
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Fixed replay failed: " + error.Message + "\n" + error.StackTrace, error);
            }
            finally
            {
                VerificationSession.Shared.Session.CancelEdit(); VerificationSession.Shared.Playback.Stop();
                if (preview != null) preview.Close(); if (export != null) export.Close();
                if (imgui != null) { imgui.DiscardChanges(); imgui.Close(); } if (toolkitWindow != null) { toolkitWindow.DiscardChanges(); toolkitWindow.Close(); }
                try { VerificationSession.Shared.Open(previous); VerificationSession.Shared.Session.RestoreRecovery(recovery); }
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
        private static void TutorialPointer(VisualElement target, EventType type, Vector2 point)
        {
            var source = new Event { type = type, button = 0, shift = true, mousePosition = target.LocalToWorld(point) };
            if (type == EventType.MouseDown) { using var e = PointerDownEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else if (type == EventType.MouseDrag) { using var e = PointerMoveEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else { using var e = PointerUpEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
        }
    }
}
