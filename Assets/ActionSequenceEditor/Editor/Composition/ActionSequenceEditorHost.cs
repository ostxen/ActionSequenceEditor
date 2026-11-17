using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionSequenceEditor
{
    // 组合根拥有唯一作者会话和Editor更新，窗口只在建立绑定时取得显式依赖。
    [InitializeOnLoad]
    public static class ActionSequenceEditorHost
    {
        private static readonly Composition.EditorCompositionRoot composition = new Composition.EditorCompositionRoot(Composition.SequenceComposition.Create());
        private static readonly EditorViewRegistry views = new EditorViewRegistry();
        private static readonly Application.Settings.SettingsSession settings = Composition.SettingsSessionComposition.Create();
        public static Composition.WindowBinding CreateBinding(object owner, Action repaint, Action changed)
            => composition.CreateBinding(owner, repaint, changed, views, settings);
        private static InspectorSelectionTarget inspectorPreview;
        private static readonly Composition.EditorUpdateDriver driver;
        static ActionSequenceEditorHost()
        {
            driver = composition.CreateDriver(views, settings, Debug.LogException);
            composition.ConnectInspector(ShowInspector);
            EditorApplication.update += UpdateSharedEditor;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            RestoreRecovery();
            EditorApplication.playModeStateChanged += _ => composition.StopPlayback();
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (_, __) => composition.StopPlayback();
        }
        // 恢复失败保留快照；不能把损坏内容当空文档继续覆盖。
        private static void RestoreRecovery()
        {
            try
            {
                composition.RestoreRecovery();
            }
            catch (Exception error) { Debug.LogError("ActionSequenceEditor 文档恢复失败，恢复快照仍保留：" + error.Message); }
        }
        // 即使恢复编码失败也释放预览和选择代理，但不代替用户保存文件。
        private static void BeforeReload()
        {
            try { composition.DisconnectInspector(ShowInspector); composition.BeforeReload(); }
            finally
            {
                if (inspectorPreview != null) Object.DestroyImmediate(inspectorPreview);
            }
        }
        private static void ShowInspector()
        {
            if (inspectorPreview == null) inspectorPreview = ScriptableObject.CreateInstance<InspectorSelectionTarget>();
            inspectorPreview.Bind(() => composition.CreateProperties(settings));
            Selection.activeObject = inspectorPreview;
            EditorUtility.SetDirty(inspectorPreview);
        }
        private static void UpdateSharedEditor() => driver.Tick();
    }
}
