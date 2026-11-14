using NBC.ActionEditor.Views.IMGUI;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Composition
{
    // 文档宿主交互集中装配，双UI只持有同一意图接口。
    public static class DocumentPresenterComposition
    {
        private sealed class Interaction : IDocumentInteraction
        {
            private readonly ActionEditorContext context;
            private readonly System.Func<bool, string> chooseSavePath;
            public Interaction(ActionEditorContext context, System.Func<bool, string> chooseSavePath)
            {
                this.context = context;
                this.chooseSavePath = chooseSavePath ?? (saveAs => EditorUtility.SaveFilePanelInProject(
                    saveAs ? Lan.SaveAs : Lan.Save, context.Source != null ? context.Source.name : "Action", "json", Lan.NewPathHint));
            }
            public string ChooseSavePath(bool saveAs) => chooseSavePath(saveAs);
            public void PickDocument() => ObjectSelectorWindow.ShowObjectPicker<TextAsset>(null, value =>
            {
                // 选择窗口可跨多帧停留，真正切换前重验当前作者事务。
                OpenPicked(context, value, chooseSavePath);
            }, "Assets/", assetPathFilter: Infrastructure.Documents.JsonDocumentRepository.IsDocumentPath);
            public void CreateDocument() => CreateDocumentWindow.Show(context);
            public void CloseDocument() => context.Open(null);
            public bool ConfirmSwitch()
            {
                if (context.Session.IsEditing) throw new System.InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
                if (!context.Session.IsDirty) return true;
                var choice = EditorUtility.DisplayDialogComplex(Lan.UnsavedChanges, Lan.SaveBeforeSwitch, Lan.Save, Lan.TipsCancel, Lan.Discard);
                if (choice == 1) return false;
                if (choice == 0) Create(context, chooseSavePath).Invoke(DocumentIntent.Save);
                return choice == 2 || !context.Session.IsDirty;
            }
            public void ShowExport() => DocumentExportWindow.ShowFor(context);
            public void Saved() => context.AutoSave.RecordSaved();
            public void Undo(bool redo) { if (redo) UnityEditor.Undo.PerformRedo(); else UnityEditor.Undo.PerformUndo(); }
        }
        // 路径选择随窗口显式装配，隔离宿主不借用另一会话的可变全局回调。
        public static DocumentPresenter Create(ActionEditorContext context, System.Func<bool, string> chooseSavePath = null)
            => new DocumentPresenter(context.Session, context.Lifecycle, new Interaction(context, chooseSavePath));
        // 菜单只绑定传入Presenter，不能在点击时重新定位全局会话。
        public static void PopulateSaveMenu(DocumentPresenter documents, System.Action<string, System.Action> add)
        {
            add(Lan.Save, () => documents.Invoke(DocumentIntent.Save));
            add(Lan.SaveAs, () => documents.Invoke(DocumentIntent.SaveAs));
            add(Lan.Export, () => documents.Invoke(DocumentIntent.Export));
        }
        public static bool ConfirmSwitch(ActionEditorContext context) => new Interaction(context, null).ConfirmSwitch();
        // 真实选择回调先过滤扩展名，再进行脏文档确认和原子切换。
        public static void OpenPicked(ActionEditorContext context, Object value, System.Func<bool, string> chooseSavePath = null)
        {
            if (context.Session.IsEditing) throw new System.InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
            if (value is not TextAsset source) return;
            var path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path) && !Infrastructure.Documents.JsonDocumentRepository.IsDocumentPath(path))
            { Debug.LogWarning("请选择 .json 格式的 ActionEditor 文档。"); return; }
            if (source != context.Source && new Interaction(context, chooseSavePath).ConfirmSwitch()) context.Open(source);
        }
    }
}
