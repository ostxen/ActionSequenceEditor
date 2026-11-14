using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor
{
    // 组合根拥有唯一作者会话和Editor更新，窗口只在建立绑定时取得显式依赖。
    [InitializeOnLoad]
    public static class ActionEditorHost
    {
        public static ActionEditorContext Context { get; } = new ActionEditorContext(Composition.DocumentComposition.Create());
        public static ActionEditorViews Views { get; } = new ActionEditorViews();
        private static InspectorPreviewAsset inspectorPreview;
        private static readonly Composition.EditorUpdateDriver driver;
        static ActionEditorHost()
        {
            driver = new Composition.EditorUpdateDriver(Context, Views, Prefs.Session, Debug.LogException);
            Context.Selection.InspectorRequested += ShowInspector;
            EditorApplication.update += UpdateSharedEditor;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            RestoreRecovery();
            EditorApplication.playModeStateChanged += _ => Context.Playback.Stop();
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Context.Playback.Stop();
        }
        // 恢复失败保留快照；不能把损坏内容当空文档继续覆盖。
        private static void RestoreRecovery()
        {
            var recovery = Context.Recovery.Read();
            if (string.IsNullOrEmpty(recovery)) return;
            try
            {
                Context.Session.RestoreRecovery(recovery);
                Context.RestoreSource(AssetDatabase.LoadAssetAtPath<TextAsset>(Context.Session.Path ?? ""));
            }
            catch (Exception error) { Debug.LogError("ActionEditor 文档恢复失败，恢复快照仍保留：" + error.Message); }
        }
        // 即使恢复编码失败也释放预览和选择代理，但不代替用户保存文件。
        private static void BeforeReload()
        {
            try { Context.Recovery.Write(Context.Session.CaptureRecovery()); }
            finally
            {
                try { Context.Player.Reset(); }
                finally { if (inspectorPreview != null) Object.DestroyImmediate(inspectorPreview); }
            }
        }
        private static void ShowInspector()
        {
            if (inspectorPreview == null) inspectorPreview = ScriptableObject.CreateInstance<InspectorPreviewAsset>();
            inspectorPreview.Bind(() => Composition.PropertyComposition.Create(Context, Prefs.Session));
            Selection.activeObject = inspectorPreview;
            EditorUtility.SetDirty(inspectorPreview);
        }
        private static void UpdateSharedEditor() => driver.Tick();
    }
}
