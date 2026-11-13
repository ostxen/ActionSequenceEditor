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
            public Interaction(ActionEditorContext context) { this.context = context; }
            public string ChooseSavePath(bool saveAs) => saveAs
                ? EditorUtility.SaveFilePanelInProject(Lan.SaveAs, context.Source != null ? context.Source.name : "Action", "json", Lan.NewPathHint)
                : App.SavePathRequested?.Invoke();
            public void PickDocument() => ObjectSelectorWindow.ShowObjectPicker<TextAsset>(null, value =>
            {
                // 选择窗口可跨多帧停留，真正切换前重验当前作者事务。
                if (context.Session.IsEditing) throw new System.InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
                if (value is TextAsset source && source != context.Source && ConfirmSwitch()) context.Open(source);
            }, "Assets/", assetPathFilter: App.IsJsonDocumentPath);
            public void CreateDocument() => CreateAssetWindow.Show(context);
            public void CloseDocument() => context.Open(null);
            public bool ConfirmSwitch()
            {
                if (context.Session.IsEditing) throw new System.InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
                if (!context.Session.IsDirty) return true;
                var choice = EditorUtility.DisplayDialogComplex(Lan.UnsavedChanges, Lan.SaveBeforeSwitch, Lan.Save, Lan.TipsCancel, Lan.Discard);
                if (choice == 1) return false;
                if (choice == 0) Create(context).Invoke(DocumentIntent.Save);
                return choice == 2 || !context.Session.IsDirty;
            }
            public void ShowExport() => DocumentExportWindow.ShowFor(context);
            public void Saved() => context.AutoSave.RecordSaved();
            public void Undo(bool redo) { if (redo) UnityEditor.Undo.PerformRedo(); else UnityEditor.Undo.PerformUndo(); }
        }
        public static DocumentPresenter Create(ActionEditorContext context) => new DocumentPresenter(context.Session, context.Lifecycle, new Interaction(context));
        public static bool ConfirmSwitch(ActionEditorContext context) => new Interaction(context).ConfirmSwitch();
    }
}
