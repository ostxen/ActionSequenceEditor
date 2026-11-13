using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor
{
    /// <summary>Unity lifecycle, window and Inspector adapter for the shared authoring context.</summary>
    [InitializeOnLoad]
    public static class ActionEditorHost
    {
        public static EditorWindow Window;
        public static float Width;
        private static InspectorPreviewAsset inspectorPreview;
        static ActionEditorHost()
        {
            App.SavePathRequested = () => ChooseSavePath(false);
            App.Context.Selection.InspectorRequested += ShowInspector;
            App.RepaintRequested += () => { if (App.Views.Count == 0 && Window != null) Window.Repaint(); };
            EditorApplication.update += UpdateSharedEditor;
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                App.Context.Recovery.Write(App.Session.CaptureRecovery());
                App.Context.Player.Reset();
                if (inspectorPreview != null) Object.DestroyImmediate(inspectorPreview);
            };
            var recovery = App.Context.Recovery.Read();
            if (!string.IsNullOrEmpty(recovery))
            {
                try
                {
                    App.Session.RestoreRecovery(recovery);
                    App.Context.RestoreSource(AssetDatabase.LoadAssetAtPath<TextAsset>(App.Session.Path ?? ""));
                }
                catch (Exception error) { Debug.LogError("ActionEditor 文档恢复失败，恢复快照仍保留：" + error.Message); }
            }
            EditorApplication.playModeStateChanged += _ => { App.Stop(); App.Context.Player.Reset(); };
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (_, __) => App.Stop();
        }
        private static void ShowInspector()
        {
            if (inspectorPreview == null) inspectorPreview = ScriptableObject.CreateInstance<InspectorPreviewAsset>();
            Selection.activeObject = inspectorPreview;
            EditorUtility.SetDirty(inspectorPreview);
        }
        private static void UpdateSharedEditor()
        {
            if (App.Views.Count == 0) return;
            try { App.OnUpdate(); }
            catch (Exception error) { App.Stop(); Debug.LogException(error); }
        }
        public static void OnObjectPickerConfig(Object obj)
        {
            if (obj is not TextAsset textAsset) return;
            var path = AssetDatabase.GetAssetPath(textAsset);
            if (!string.IsNullOrEmpty(path) && !App.IsJsonDocumentPath(path))
            {
                Debug.LogWarning("请选择 .json 格式的 ActionEditor 文档。");
                return;
            }
            if (textAsset != App.TextAsset && !ConfirmDocumentSwitch()) return;
            App.TextAsset = textAsset;
        }
        public static bool ConfirmDocumentSwitch()
        {
            if (!App.Session.IsDirty) return true;
            var choice = EditorUtility.DisplayDialogComplex(Lan.UnsavedChanges, Lan.SaveBeforeSwitch, Lan.Save, Lan.TipsCancel, Lan.Discard);
            if (choice == 1) return false;
            if (choice == 0) { App.SaveAsset(); return !App.Session.IsDirty; }
            return true;
        }
        private static string ChooseSavePath(bool saveAs) => EditorUtility.SaveFilePanelInProject(saveAs ? Lan.SaveAs : Lan.Save, App.TextAsset != null ? App.TextAsset.name : "Action", "json", Lan.NewPathHint);
        public static void SaveAs()
        {
            if (App.Session.IsEditing) throw new InvalidOperationException(Lan.FinishEditBeforeSave);
            var path = ChooseSavePath(true);
            if (!string.IsNullOrEmpty(path))
            {
                path = DocumentCreationService.ValidateNewPath(path);
                App.SaveAssetTo(path);
            }
        }
        public static void PopulateSaveMenu(Action<string, Action> add)
        {
            add(Lan.Save, App.SaveAsset);
            add(Lan.SaveAs, SaveAs);
            add(Lan.Export, DocumentExportWindow.ShowForCurrent);
        }
    }
}
