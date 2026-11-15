using System;
using NBC.ActionEditor.Views.IMGUI;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views
{
    // 原生交互仅收集选择与回调，不保存、切换会话或构造临时Presenter。
    internal sealed class DocumentInteraction : IDocumentInteraction
    {
        private readonly Func<string> documentName;
        private readonly Action saved;
        private readonly Func<bool, string> chooseSavePath;
        private readonly IDocumentDialogCatalog catalog;
        public DocumentInteraction(Func<string> documentName, Action saved, Func<bool, string> chooseSavePath, IDocumentDialogCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.documentName = documentName; this.saved = saved;
            this.chooseSavePath = chooseSavePath ?? (saveAs => EditorUtility.SaveFilePanelInProject(
                saveAs ? Lan.SaveAs : Lan.Save, documentName(), "json", Lan.NewPathHint));
        }
        public string ChooseSavePath(bool saveAs) => chooseSavePath(saveAs);
        public void PickDocument(Action<object> selected) => ObjectSelectorWindow.ShowObjectPicker<TextAsset>(null,
            value => selected(value), "Assets/", assetPathFilter: catalog.IsDocumentPath);
        public void CreateDocument(Func<Type, string, bool> create) => CreateDocumentWindow.Show(catalog, create);
        public DocumentSwitchChoice ConfirmSwitch(bool dirty)
        {
            if (!dirty) return DocumentSwitchChoice.Discard;
            var choice = EditorUtility.DisplayDialogComplex(Lan.UnsavedChanges, Lan.SaveBeforeSwitch, Lan.Save, Lan.TipsCancel, Lan.Discard);
            return choice == 0 ? DocumentSwitchChoice.Save : choice == 2 ? DocumentSwitchChoice.Discard : DocumentSwitchChoice.Cancel;
        }
        public void ShowExport(Func<string, string, bool> export) => DocumentExportWindow.ShowFor(catalog, documentName, export);
        public void Saved() => saved();
        public void Undo(bool redo) { if (redo) UnityEditor.Undo.PerformRedo(); else UnityEditor.Undo.PerformUndo(); }
    }
}
