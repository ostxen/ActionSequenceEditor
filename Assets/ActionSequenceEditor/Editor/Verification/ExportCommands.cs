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
    // 验证独立导出及来源会话不变的边界。
    public static class ExportCommands
    {
        [CliGroupCommand("actionsequenceeditor.verification", "Fixed export lifecycle, both UI shared save-menu callbacks and independent new JSON targets. Restores prior session and cleans only owned fixtures.")]
        public static object ExportProbe([CliArg("confirm", "Apply isolated export lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            VerificationGuard.RequireReady();
            const string root = "Assets/ActionSequenceEditorExportVerification";
            if (Directory.Exists(root) || File.Exists(root) || File.Exists(root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (VerificationSession.Shared.Session.IsEditing || VerificationSession.Shared.Playback.IsPlaying) throw new InvalidOperationException("Requires no active edit/playback in previous session.");
            if (dryRun) return new { root, targets = new[] { root + "/Source.json", root + "/Clean.json", root + "/Dirty.json" },
                operations = new[] { "capture/restore prior session", "create isolated source", "open shared export window from IMGUI/Toolkit save menu", "export clean/dirty new JSON and read back", "verify source identity, content, baseline, Undo, selection and paused playback unchanged", "reject source alias, existing target and active gesture", "delete only owned fixture folder" } };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var recovery = VerificationSession.Shared.Session.CaptureRecovery(); var previous = VerificationSession.Shared.Source; var owns = false;
            SequenceExportWindow window = null;
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionSequenceEditorExportVerification"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                VerificationSession.Shared.CreateSequencePresenter().Create(typeof(FeatureSampleSequence), root + "/Source.json");
                var sourceBytes = File.ReadAllBytes(VerificationSession.Shared.Session.Path);
                for (var dirty = 0; dirty < 2; dirty++)
                {
                    if (dirty != 0) VerificationSession.Shared.Session.Edit("Export content", () => ((Samples.FeatureSample.FeatureSampleSequence)VerificationSession.Shared.Sequence).Version = 9);
                    VerificationSession.Shared.Selection.Set(new IDirectable[] { VerificationSession.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0] }, false);
                    VerificationSession.Shared.Sampler.Sample(.5f); VerificationSession.Shared.Playback.Play(VerificationSession.Shared.Clock.PlaybackTime); VerificationSession.Shared.Playback.SetPaused(true);
                    var source = VerificationSession.Shared.Sequence; var selection = VerificationSession.Shared.Selection.First; var before = VerificationSession.Shared.Session.CaptureRecovery(); var path = VerificationSession.Shared.Session.Path;
                    Action open = null;
                    Composition.SequencePresenterComposition.PopulateSaveMenu(VerificationSession.Shared.CreateSequencePresenter(), (label, action) => { if (label == EditorText.Export) open = action; });
                    if (open == null) throw new InvalidOperationException("Both UI shared menu missing export.");
                    open(); window = Resources.FindObjectsOfTypeAll<SequenceExportWindow>().Single();
                    var target = root + (dirty == 0 ? "/Clean.json" : "/Dirty.json");
                    if (window.ExportTo("actioneditor.json", null)) throw new InvalidOperationException("Cancel exported a file.");
                    window.ExportTo("actioneditor.json", target);
                    window.Close(); window = null;
                    var loaded = SequenceJsonCodec.Decode(File.ReadAllText(target));
                    if (((Samples.FeatureSample.FeatureSampleSequence)loaded).Version != (dirty == 0 ? 1 : 9)) throw new InvalidOperationException("Export lost business content.");
                    foreach (var rejected in new[] { target, root.ToLowerInvariant() + "/./Source.JSON" })
                    {
                        var refused = false;
                        try { VerificationSession.Shared.CreateSequencePresenter().Export(rejected, "actioneditor.json"); } catch (Exception error) when (error is IOException || error is ArgumentException) { refused = true; }
                        if (!refused) throw new InvalidOperationException("Unsafe export target accepted.");
                    }
                    if (!ReferenceEquals(source, VerificationSession.Shared.Sequence) || VerificationSession.Shared.Session.CaptureRecovery() != before || path != VerificationSession.Shared.Session.Path
                        || !ReferenceEquals(selection, VerificationSession.Shared.Selection.First) || !VerificationSession.Shared.Playback.IsPlaying || !VerificationSession.Shared.Playback.IsPaused || VerificationSession.Shared.Sampler.CurrentTime != .5f
                        || !File.ReadAllBytes(path).SequenceEqual(sourceBytes)) throw new InvalidOperationException("Export changed source/session/selection/playback.");
                    VerificationSession.Shared.Playback.Stop();
                }
                Undo.PerformUndo();
                if (((Samples.FeatureSample.FeatureSampleSequence)VerificationSession.Shared.Sequence).Version != 1) throw new InvalidOperationException("Export changed Undo.");
                Undo.PerformRedo();
                if (((Samples.FeatureSample.FeatureSampleSequence)VerificationSession.Shared.Sequence).Version != 9) throw new InvalidOperationException("Export changed Redo.");
                var edit = VerificationSession.Shared.Session.BeginEdit(new object());
                var refusedEdit = false;
                try { VerificationSession.Shared.CreateSequencePresenter().Export(root + "/Active.json", "actioneditor.json"); } catch (InvalidOperationException) { refusedEdit = true; }
                if (!refusedEdit || !edit.IsActive || File.Exists(root + "/Active.json")) throw new InvalidOperationException("Export ended active gesture.");
                edit.Cancel();
                return new { status = "passed", checks = new[] { "clean/dirty exact readback", "export window/menu entry", "cancel no writes", "existing/source alias rejected", "active edit remains owned", "source/baseline/selection/paused playback/Undo unchanged" } };
            }
            finally
            {
                if (window != null) window.Close();
                try { VerificationSession.Shared.Playback.Stop(); VerificationSession.Shared.Open(previous); VerificationSession.Shared.Session.RestoreRecovery(recovery); }
                finally { if (owns && !AssetDatabase.DeleteAsset(root)) throw new IOException("Owned fixture cleanup failed."); }
            }
        }
    }
}
