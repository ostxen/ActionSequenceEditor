using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Composition;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Storage;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using EditorSettings = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Validation
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

        [CliGroupCommand("actioneditor.validation", "Verify both real SaveChanges entrypoints with explicitly isolated contexts, mixed roots, save-as, recovery, export and failure boundaries. Creates and deletes only the fixed new fixture directory.")]
        public static object DocumentLifecycleProbe(
            [CliArg("confirm", "Apply reviewed fixed fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Inspect exact scope without writes.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("需要空闲编辑模式。");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta"))
                throw new IOException("固定验证目录已存在，拒绝覆盖。");
            if (App.Session.IsEditing || App.IsPlay) throw new InvalidOperationException("需要当前会话没有活动编辑或播放。");
            if (dryRun) return new
            {
                root = Root,
                targets = new[] { "IMGUI.json", "IMGUI-Copy.json", "IMGUI-Export.json", "Toolkit.json", "Toolkit-Copy.json", "Toolkit-Export.json" }.Select(name => Root + "/" + name).ToArray(),
                operations = new[] { "create independent contexts and owned windows", "create FeatureSample", "edit property and interleave root group/track", "both real SaveChanges", "Undo/Redo", "close/reopen disk", "save-as/recovery/autosave", "export via bound window", "locked commit/import failure/external conflict", "close owned windows and delete owned directory" },
                touchesUserSession = false, writesPreferences = false, nativeFileDialogs = false
            };
            if (!confirm) throw new InvalidOperationException("需要预览与真实审批。");
            var graph = App.Context.Document;
            var source = App.TextAsset;
            var recovery = App.Session.CaptureRecovery();
            var previousHost = ActionEditorHost.Window;
            var originalViews = App.Views.Count;
            var owned = false;
            try
            {
                owned = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorLifecycleVerification"));
                if (!owned) throw new IOException("无法创建验证目录。");
                Run(false);
                Run(true);
                Require(ReferenceEquals(App.Context.Document, graph) && App.TextAsset == source
                    && App.Session.CaptureRecovery() == recovery && App.Views.Count == originalViews, "用户会话或窗口注册发生变化。");
                return new { status = "passed", interfaces = 2, cleanup = "owned windows and folder only", checks = "mixed roots/property/SaveChanges/Undo/Redo/reopen/save-as/recovery/autosave/export/locked target/import failure/external conflict" };
            }
            finally
            {
                ActionEditorHost.Window = previousHost;
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
            var repository = new JsonDocumentRepository();
            using var context = new ActionEditorContext(DocumentComposition.Create(repository), repository);
            var views = new ActionEditorViews();
            var settings = new SettingsSession(new Preferences());
            ActionEditorWindow imgui = null;
            Workbench.UIToolkitActionEditorWindow native = null;
            DocumentExportWindow export = null;
            try
            {
                if (toolkit)
                {
                    native = ScriptableObject.CreateInstance<Workbench.UIToolkitActionEditorWindow>();
                    native.BindContext(context, views, settings); native.Show();
                }
                else
                {
                    imgui = ScriptableObject.CreateInstance<ActionEditorWindow>();
                    imgui.BindContext(context, views, settings); imgui.Show();
                }
                new DocumentDialogComposition(context).CreateAndOpen(typeof(FeatureSampleDocument), path);
                var clip = (FeatureSampleInterval)context.Document.AllTracks.First().Clips.First();
                var properties = PropertyComposition.Create(context);
                properties.Write(properties.Field(clip, typeof(FeatureSampleInterval).GetField("Count")).Path, 27);
                var group = context.Document.RootItems.OfType<TimelineGroup>().First();
                context.Structure.Move(group.Tracks[0], null, 1, context.Session.Version);
                Require(context.Document.RootItems[1] is TimelineTrack, "根结构未形成组/轨交错。");
                if (toolkit) native.SaveChanges(); else imgui.SaveChanges();
                Require(!context.Session.IsDirty, "窗口SaveChanges未保存。");
                var saved = JsonDocumentCodec.Content(context.Document);
                Undo.PerformUndo(); Require(context.Session.IsDirty, "保存后Undo没有恢复脏状态。");
                Undo.PerformRedo(); Require(!context.Session.IsDirty && JsonDocumentCodec.Content(context.Document) == saved, "Redo未回到保存基线。");
                if (toolkit) { native.Close(); native = null; }
                else { imgui.Close(); imgui = null; }
                Require(views.Count == 0, "保存后关闭窗口未释放注册。");
                context.Open(null); context.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                Require(JsonDocumentCodec.Content(context.Document) == saved, "磁盘重开内容不符。");
                Require(context.Document.RootItems.Count == 4 && context.Document.RootItems[1] is TimelineTrack
                    && ((FeatureSampleInterval)((TimelineTrack)context.Document.RootItems[1]).Clips[0]).Count == 27,
                    "磁盘重开未保留明确期望的混排和属性值。");
                if (toolkit)
                {
                    native = ScriptableObject.CreateInstance<Workbench.UIToolkitActionEditorWindow>();
                    native.BindContext(context, views, settings); native.Show();
                }
                else
                {
                    imgui = ScriptableObject.CreateInstance<ActionEditorWindow>();
                    imgui.BindContext(context, views, settings); imgui.Show();
                }
                Require(context.Lifecycle.SaveAs(copy).Succeeded && context.Session.Path == copy, "另存失败。");
                context.Session.Edit("Recovery value", () => ((FeatureSampleDocument)context.Document).Version = 17);
                using (var restored = DocumentComposition.Create())
                {
                    restored.RestoreRecovery(context.Session.CaptureRecovery());
                    Require(restored.IsDirty && ((FeatureSampleDocument)restored.Document).Version == 17, "恢复丢失未保存值。");
                    restored.DiscardChanges(); Require(JsonDocumentCodec.Content(restored.Document) == saved, "恢复丢失保存基线。");
                }
                var clock = new Clock();
                var autoSave = new DocumentAutoSave(context.Session, context.Lifecycle, clock);
                clock.Now = 10; Require(autoSave.Tick(10)?.Succeeded == true, "自动保存未提交。");
                var windows = Resources.FindObjectsOfTypeAll<DocumentExportWindow>();
                DocumentExportWindow.ShowFor(context);
                export = Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Except(windows).Single();
                Require(export.ExportTo("actioneditor.json", exported), "导出失败。");
                Require(JsonDocumentCodec.Content(JsonDocumentCodec.Decode(File.ReadAllText(exported))) == JsonDocumentCodec.Content(context.Document), "导出独立读回不符。");
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
        private static void VerifyFailures(ActionEditorContext context, string path)
        {
            context.Session.Edit("Locked target", () => ((FeatureSampleDocument)context.Document).Version = 18);
            var before = File.ReadAllBytes(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var result = context.Lifecycle.Save();
                Require(!result.Succeeded && !result.BytesCommitted && context.Session.IsDirty && File.ReadAllBytes(path).SequenceEqual(before), "提交前失败破坏字节或基线。");
            }
            Require(context.Lifecycle.Save().Succeeded, "解除占用后保存失败。");
            var failedRepository = new JsonDocumentRepository(import: _ => throw new IOException("Injected import failure"));
            using (var failed = new ActionEditorContext(DocumentComposition.Create(failedRepository), failedRepository))
            {
                failed.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                failed.Session.Edit("Import failure", () => ((FeatureSampleDocument)failed.Document).Version = 19);
                var result = failed.Lifecycle.Save();
                Require(!result.Succeeded && result.BytesCommitted && !failed.Session.IsDirty, "导入失败丢失已提交语义。");
                Require(((FeatureSampleDocument)JsonDocumentCodec.Decode(File.ReadAllText(path))).Version == 19, "导入失败没有保留已提交字节。");
            }
            var external = File.ReadAllBytes(path);
            context.Session.Edit("Conflict", () => ((FeatureSampleDocument)context.Document).Version = 20);
            var conflict = context.Lifecycle.Save();
            Require(!conflict.Succeeded && !conflict.BytesCommitted && File.ReadAllBytes(path).SequenceEqual(external), "外部版本保护失败。");
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
