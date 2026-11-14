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
    // 验证独立导出及来源会话不变的边界。
    public static class ExportCommands
    {
        [CliGroupCommand("actioneditor.verification", "Fixed export lifecycle, both UI shared save-menu callbacks and independent new JSON targets. Restores prior session and cleans only owned fixtures.")]
        public static object ExportProbe([CliArg("confirm", "Apply isolated export lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            VerificationGuard.Ready();
            const string root = "Assets/ActionEditorExportVerification";
            if (Directory.Exists(root) || File.Exists(root) || File.Exists(root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (ActionEditorHost.Context.Session.IsEditing || ActionEditorHost.Context.Playback.IsPlaying) throw new InvalidOperationException("Requires no active edit/playback in previous session.");
            if (dryRun) return new { root, targets = new[] { root + "/Source.json", root + "/Clean.json", root + "/Dirty.json" },
                operations = new[] { "capture/restore prior session", "create isolated source", "open shared export window from IMGUI/Toolkit save menu", "export clean/dirty new JSON and read back", "verify source identity, content, baseline, Undo, selection and paused playback unchanged", "reject source alias, existing target and active gesture", "delete only owned fixture folder" } };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var recovery = ActionEditorHost.Context.Session.CaptureRecovery(); var previous = ActionEditorHost.Context.Source; var owns = false;
            DocumentExportWindow window = null;
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorExportVerification"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                new Composition.DocumentDialogComposition(ActionEditorHost.Context).CreateAndOpen(typeof(FeatureSampleDocument), root + "/Source.json");
                var sourceBytes = File.ReadAllBytes(ActionEditorHost.Context.Session.Path);
                for (var dirty = 0; dirty < 2; dirty++)
                {
                    if (dirty != 0) ActionEditorHost.Context.Session.Edit("Export content", () => ((Samples.FeatureSample.FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 9);
                    ActionEditorHost.Context.Selection.Set(new IDirectable[] { ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0] }, false);
                    ActionEditorHost.Context.Player.Sample(.5f); ActionEditorHost.Context.Playback.Play(ActionEditorHost.Context.Clock.PlaybackTime); ActionEditorHost.Context.Playback.Pause(true);
                    var source = ActionEditorHost.Context.Document; var selection = ActionEditorHost.Context.Selection.First; var before = ActionEditorHost.Context.Session.CaptureRecovery(); var path = ActionEditorHost.Context.Session.Path;
                    Action open = null;
                    Composition.DocumentPresenterComposition.PopulateSaveMenu(Composition.DocumentPresenterComposition.Create(ActionEditorHost.Context), (label, action) => { if (label == Lan.Export) open = action; });
                    if (open == null) throw new InvalidOperationException("Both UI shared menu missing export.");
                    open(); window = Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Single();
                    var target = root + (dirty == 0 ? "/Clean.json" : "/Dirty.json");
                    if (window.ExportTo("actioneditor.json", null)) throw new InvalidOperationException("Cancel exported a file.");
                    window.ExportTo("actioneditor.json", target);
                    window.Close(); window = null;
                    var loaded = JsonDocumentCodec.Decode(File.ReadAllText(target));
                    if (((Samples.FeatureSample.FeatureSampleDocument)loaded).Version != (dirty == 0 ? 1 : 9)) throw new InvalidOperationException("Export lost business content.");
                    foreach (var rejected in new[] { target, root.ToLowerInvariant() + "/./Source.JSON" })
                    {
                        var refused = false;
                        try { Composition.DocumentPresenterComposition.Create(ActionEditorHost.Context).Export(rejected, "actioneditor.json"); } catch (Exception error) when (error is IOException || error is ArgumentException) { refused = true; }
                        if (!refused) throw new InvalidOperationException("Unsafe export target accepted.");
                    }
                    if (!ReferenceEquals(source, ActionEditorHost.Context.Document) || ActionEditorHost.Context.Session.CaptureRecovery() != before || path != ActionEditorHost.Context.Session.Path
                        || !ReferenceEquals(selection, ActionEditorHost.Context.Selection.First) || !ActionEditorHost.Context.Playback.IsPlaying || !ActionEditorHost.Context.Playback.IsPaused || ActionEditorHost.Context.Player.CurrentTime != .5f
                        || !File.ReadAllBytes(path).SequenceEqual(sourceBytes)) throw new InvalidOperationException("Export changed source/session/selection/playback.");
                    ActionEditorHost.Context.Playback.Stop();
                }
                Undo.PerformUndo();
                if (((Samples.FeatureSample.FeatureSampleDocument)ActionEditorHost.Context.Document).Version != 1) throw new InvalidOperationException("Export changed Undo.");
                Undo.PerformRedo();
                if (((Samples.FeatureSample.FeatureSampleDocument)ActionEditorHost.Context.Document).Version != 9) throw new InvalidOperationException("Export changed Redo.");
                var edit = ActionEditorHost.Context.Session.BeginEdit(new object());
                var refusedEdit = false;
                try { Composition.DocumentPresenterComposition.Create(ActionEditorHost.Context).Export(root + "/Active.json", "actioneditor.json"); } catch (InvalidOperationException) { refusedEdit = true; }
                if (!refusedEdit || !edit.IsActive || File.Exists(root + "/Active.json")) throw new InvalidOperationException("Export ended active gesture.");
                edit.Cancel();
                return new { status = "passed", checks = new[] { "clean/dirty exact readback", "export window/menu entry", "cancel no writes", "existing/source alias rejected", "active edit remains owned", "source/baseline/selection/paused playback/Undo unchanged" } };
            }
            finally
            {
                if (window != null) window.Close();
                try { ActionEditorHost.Context.Playback.Stop(); ActionEditorHost.Context.Open(previous); ActionEditorHost.Context.Session.RestoreRecovery(recovery); }
                finally { if (owns && !AssetDatabase.DeleteAsset(root)) throw new IOException("Owned fixture cleanup failed."); }
            }
        }
    }
}
