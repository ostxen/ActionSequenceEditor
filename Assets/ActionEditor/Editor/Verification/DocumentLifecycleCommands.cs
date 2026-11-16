using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Application.Sequences;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Composition;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Infrastructure.Sequences;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using EditorSettings = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Verification
{
    // 固定新目录验证双窗口持久闭环，不替换或恢复用户正在编辑的全局会话。
    public static class DocumentLifecycleCommands
    {
        private const string Root = "Assets/ActionEditorLifecycleVerification";

        private sealed class Preferences : IPreferencesStore
        {
            public EditorSettings Read() => new EditorSettings(TimeStepMode.Seconds, .1f, 30, true, true, 0, "Assets/");
            public void Write(EditorSettings value) => throw new InvalidOperationException("验证不得写个人偏好。");
        }
        private sealed class Clock : IEditorClock
        {
            public double Now { get; set; }
            public float PlaybackTime => (float)Now;
            public float TimeScale => 1;
            public DateTime LocalTime => new DateTime(2026, 1, 1).AddSeconds(Now);
        }

        [CliGroupCommand("actioneditor.verification", "Verify both real SaveChanges entrypoints with explicitly isolated contexts, mixed roots, save-as, recovery, export and failure boundaries. Creates and deletes only the fixed new fixture directory.")]
        public static object DocumentLifecycleProbe(
            [CliArg("confirm", "Apply reviewed fixed fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Inspect exact scope without writes.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("需要空闲编辑模式。");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta"))
                throw new IOException("固定验证目录已存在，拒绝覆盖。");
            if (VerificationSession.Shared.Session.IsEditing || VerificationSession.Shared.Playback.IsPlaying) throw new InvalidOperationException("需要当前会话没有活动编辑或播放。");
            if (dryRun) return new
            {
                root = Root,
                targets = new[] { "IMGUI.json", "IMGUI-Copy.json", "IMGUI-Export.json", "Toolkit.json", "Toolkit-Copy.json", "Toolkit-Export.json" }.Select(name => Root + "/" + name).ToArray(),
                operations = new[] { "create independent contexts and owned windows", "create FeatureSample", "edit property and interleave root group/track", "both real SaveChanges", "Undo/Redo", "close/reopen disk", "save-as/recovery/autosave", "export via bound window", "locked commit/import failure/external conflict", "close owned windows and delete owned directory" },
                touchesUserSession = false, writesPreferences = false, nativeFileDialogs = false
            };
            if (!confirm) throw new InvalidOperationException("需要预览与真实审批。");
            var graph = VerificationSession.Shared.Sequence;
            var source = VerificationSession.Shared.Source;
            var recovery = VerificationSession.Shared.Session.CaptureRecovery();

            var originalViews = VerificationSession.SharedViews.Count;
            var owned = false;
            try
            {
                owned = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorLifecycleVerification"));
                if (!owned) throw new IOException("无法创建验证目录。");
                Run(false);
                Run(true);
                Require(ReferenceEquals(VerificationSession.Shared.Sequence, graph) && VerificationSession.Shared.Source == source
                    && VerificationSession.Shared.Session.CaptureRecovery() == recovery && VerificationSession.SharedViews.Count == originalViews, "用户会话或窗口注册发生变化。");
                return new { status = "passed", interfaces = 2, cleanup = "owned windows and folder only", checks = "mixed roots/property/SaveChanges/Undo/Redo/reopen/save-as/recovery/autosave/export/locked target/import failure/external conflict" };
            }
            finally
            {

                if (owned && !AssetDatabase.DeleteAsset(Root)) throw new IOException("验证目录清理失败。");
            }
        }

        // 两套原生窗口使用同一隔离作者流程，路径输入只替换为已审批的新目标。
        private static void Run(bool toolkit)
        {
            var name = toolkit ? "Toolkit" : "IMGUI";
            var path = Root + "/" + name + ".json";
            var copy = Root + "/" + name + "-Copy.json";
            var exported = Root + "/" + name + "-Export.json";
            var repository = new SequenceFileRepository();
            using var context = new VerificationSession(SequenceComposition.Create(repository), repository);
            var views = new EditorViewRegistry();
            var settings = new SettingsSession(new Preferences());
            ImguiWindow imgui = null;
            ToolkitWindow native = null;
            SequenceExportWindow export = null;
            try
            {
                if (toolkit)
                {
                    native = ScriptableObject.CreateInstance<ToolkitWindow>();
                    native.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings)); native.Show();
                }
                else
                {
                    imgui = ScriptableObject.CreateInstance<ImguiWindow>();
                    imgui.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings)); imgui.Show();
                }
                context.CreateSequencePresenter().Create(typeof(FeatureSampleSequence), path);
                var clip = (FeatureSampleInterval)context.Sequence.AllTracks.First().Clips.First();
                var properties = context.CreateProperties(settings);
                properties.Write(properties.CaptureInputs(clip).Single(f => f.Name == "Count").Id, 27);
                var group = context.Sequence.RootContainers.OfType<ActionGroup>().First();
                context.Structure.Move(group.Tracks[0], null, 1, context.Session.Version);
                Require(context.Sequence.RootContainers[1] is ActionTrack, "根结构未形成组/轨交错。");
                if (toolkit) native.SaveChanges(); else imgui.SaveChanges();
                Require(!context.Session.IsDirty, "窗口SaveChanges未保存。");
                var saved = SequenceJsonCodec.GetContentKey(context.Sequence);
                Undo.PerformUndo(); Require(context.Session.IsDirty, "保存后Undo没有恢复脏状态。");
                Undo.PerformRedo(); Require(!context.Session.IsDirty && SequenceJsonCodec.GetContentKey(context.Sequence) == saved, "Redo未回到保存基线。");
                if (toolkit) { native.Close(); native = null; }
                else { imgui.Close(); imgui = null; }
                Require(views.Count == 0, "保存后关闭窗口未释放注册。");
                context.Open(null); context.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                Require(SequenceJsonCodec.GetContentKey(context.Sequence) == saved, "磁盘重开内容不符。");
                Require(context.Sequence.RootContainers.Count == 4 && context.Sequence.RootContainers[1] is ActionTrack
                    && ((FeatureSampleInterval)((ActionTrack)context.Sequence.RootContainers[1]).Clips[0]).Count == 27,
                    "磁盘重开未保留明确期望的混排和属性值。");
                if (toolkit)
                {
                    native = ScriptableObject.CreateInstance<ToolkitWindow>();
                    native.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings)); native.Show();
                }
                else
                {
                    imgui = ScriptableObject.CreateInstance<ImguiWindow>();
                    imgui.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings)); imgui.Show();
                }
                Require(context.Lifecycle.SaveAs(copy).Succeeded && context.Session.Path == copy, "另存失败。");
                context.Session.Edit("Recovery value", () => ((FeatureSampleSequence)context.Sequence).Version = 17);
                using (var restored = SequenceComposition.Create())
                {
                    restored.RestoreRecovery(context.Session.CaptureRecovery());
                    Require(restored.IsDirty && ((FeatureSampleSequence)restored.Sequence).Version == 17, "恢复丢失未保存值。");
                    restored.DiscardChanges(); Require(SequenceJsonCodec.GetContentKey(restored.Sequence) == saved, "恢复丢失保存基线。");
                }
                var clock = new Clock();
                var autoSave = new SequenceAutoSave(context.Session, context.Lifecycle, clock);
                clock.Now = 10; Require(autoSave.Tick(10)?.Succeeded == true, "自动保存未提交。");
                var windows = Resources.FindObjectsOfTypeAll<SequenceExportWindow>();
                context.CreateSequencePresenter().Invoke(NBC.ActionEditor.Presentation.Contracts.SequenceIntent.Export);
                export = Resources.FindObjectsOfTypeAll<SequenceExportWindow>().Except(windows).Single();
                Require(export.ExportTo("actioneditor.json", exported), "导出失败。");
                Require(SequenceJsonCodec.GetContentKey(SequenceJsonCodec.Decode(File.ReadAllText(exported))) == SequenceJsonCodec.GetContentKey(context.Sequence), "导出独立读回不符。");
                VerifyFailures(context, copy);
            }
            finally
            {
                if (export != null) export.Close();
                if (imgui != null) { imgui.DiscardChanges(); imgui.Close(); }
                if (native != null) { native.DiscardChanges(); native.Close(); }
            }
            Require(views.Count == 0, "隔离窗口注册未清理。");
        }

        // 每种失败都核对实际字节和基线，不重试未知提交。
        private static void VerifyFailures(VerificationSession context, string path)
        {
            context.Session.Edit("Locked target", () => ((FeatureSampleSequence)context.Sequence).Version = 18);
            var before = File.ReadAllBytes(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var result = context.Lifecycle.Save();
                Require(!result.Succeeded && !result.BytesCommitted && context.Session.IsDirty && File.ReadAllBytes(path).SequenceEqual(before), "提交前失败破坏字节或基线。");
            }
            Require(context.Lifecycle.Save().Succeeded, "解除占用后保存失败。");
            var failedRepository = new SequenceFileRepository(import: _ => throw new IOException("Injected import failure"));
            using (var failed = new VerificationSession(SequenceComposition.Create(failedRepository), failedRepository))
            {
                failed.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                failed.Session.Edit("Import failure", () => ((FeatureSampleSequence)failed.Sequence).Version = 19);
                var result = failed.Lifecycle.Save();
                Require(!result.Succeeded && result.BytesCommitted && !failed.Session.IsDirty, "导入失败丢失已提交语义。");
                Require(((FeatureSampleSequence)SequenceJsonCodec.Decode(File.ReadAllText(path))).Version == 19, "导入失败没有保留已提交字节。");
            }
            var external = File.ReadAllBytes(path);
            context.Session.Edit("Conflict", () => ((FeatureSampleSequence)context.Sequence).Version = 20);
            var conflict = context.Lifecycle.Save();
            Require(!conflict.Succeeded && !conflict.BytesCommitted && File.ReadAllBytes(path).SequenceEqual(external), "外部版本保护失败。");
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
