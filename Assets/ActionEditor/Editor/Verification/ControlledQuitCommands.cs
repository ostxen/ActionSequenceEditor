using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NBC.ActionEditor.Verification
{
    // 仅在作者数据与编辑器现场均可安全退出时安排正常退出。
    public static class ControlledQuitCommands
    {
        private static string[] CollectBlockers()
        {
            var blockers = new System.Collections.Generic.List<string>();
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating)
                blockers.Add("Requires idle EditMode");
            if (VerificationSession.Shared.Session.IsEditing || VerificationSession.Shared.Session.IsDirty) blockers.Add("ActionEditor has an active or unsaved edit");
            if (VerificationSession.Shared.Document != null && (string.IsNullOrEmpty(VerificationSession.Shared.Session.Path) || !File.Exists(VerificationSession.Shared.Session.Path)))
                blockers.Add("ActionEditor document has no existing disk source; SessionState does not survive process restart");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) blockers.Add("dirty scene");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) blockers.Add("open prefab stage must be closed by its owner");
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.hasUnsavedChanges))
                blockers.Add("unsaved window: " + window.GetType().FullName);
            foreach (var asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(o => EditorUtility.IsPersistent(o) && EditorUtility.IsDirty(o)
                && AssetDatabase.GetAssetPath(o).StartsWith("Assets/ActionEditor/", StringComparison.OrdinalIgnoreCase)))
                blockers.Add("dirty persistent asset: " + AssetDatabase.GetAssetPath(asset) + " (" + asset.GetType().FullName + ")");
            return blockers.Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        [CliGroupCommand("actioneditor.verification", "Preview/approve/apply only. Refuses unsaved scenes, prefab stages, ActionEditor assets, windows or JSON. Ignores unrelated asset dirty flags without changing them. Never saves, discards or force-kills.")]
        public static object ControlledQuit(
            [CliArg("confirm", "Apply reviewed normal Editor exit.")] bool confirm = false,
            [CliArg("dry-run", "Inspect blockers without exiting.")] bool dryRun = false)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating)
                throw new InvalidOperationException("Requires idle EditMode.");
            var blockers = CollectBlockers().ToList();
            var quit = TypeCache.GetMethodsWithAttribute<CliGroupCommandAttribute>().SingleOrDefault(m =>
                m.DeclaringType?.FullName == "Unity.Pipeline.Editor.Commands.EditorQuitCommand" && m.Name == "Quit");
            if (quit == null) blockers.Add("audited Pipeline normal quit method missing");
            var lifecycle = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Unity.Pipeline.EditorQuit"))
                .SingleOrDefault(type => type != null);
            var cancel = lifecycle?.GetMethod("CancelScheduledExit", BindingFlags.Static | BindingFlags.NonPublic);
            var pending = lifecycle?.GetProperty("IsExitScheduled", BindingFlags.Static | BindingFlags.NonPublic);
            if (cancel == null || pending == null) blockers.Add("audited Pipeline cancellation contract missing");
            if (dryRun) return new { schema = 1, project = UnityEngine.Application.dataPath, processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                blockers = blockers.Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                operations = new[] { "recheck ActionEditor and unsaved scene/prefab/window state", "ignore unrelated asset dirty flags without saving or clearing them", "Pipeline normal quit with save=false discard=false exitCode=0" },
                savesAssets = false, discardsChanges = false, forceKills = false };
            if (!confirm) throw new InvalidOperationException("Preview and human approval required.");
            if (blockers.Count != 0) throw new InvalidOperationException("Controlled quit refused: " + string.Join("; ", blockers));
            // 先于Pipeline延迟退出检查，接受后出现的新编辑仍须取消退出。
            EditorApplication.CallbackFunction guard = null;
            guard = () =>
            {
                try
                {
                    if (!(bool)pending.GetValue(null)) { EditorApplication.update -= guard; return; }
                    if (CollectBlockers().Length == 0) return;
                    cancel.Invoke(null, null); EditorApplication.update -= guard;
                    Debug.LogWarning("ActionEditor controlled quit cancelled: editor state changed after acceptance.");
                }
                catch
                {
                    cancel.Invoke(null, null); EditorApplication.update -= guard;
                    throw;
                }
            };
            EditorApplication.update += guard;
            // 只调用审核过的固定退出方法，不接受执行目标或标志覆盖。
            try { return quit.Invoke(null, new object[] { false, false, 0 }); }
            catch (TargetInvocationException error) { EditorApplication.update -= guard; throw error.InnerException ?? error; }
        }
    }
}
