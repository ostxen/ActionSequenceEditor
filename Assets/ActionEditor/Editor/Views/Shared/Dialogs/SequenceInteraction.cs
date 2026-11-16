using System;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views
{
    // 原生交互仅收集选择与回调，不保存、切换会话或构造临时Presenter。
    internal sealed class SequenceInteraction : ISequenceInteraction
    {
        private readonly Func<string> getSourceName;
        private readonly Action saved;
        private readonly Func<bool, string> chooseSavePath;
        private readonly ISequenceDialogCatalog catalog;
        private readonly IEditorDialogs dialogs;
        public SequenceInteraction(Func<string> getSourceName, Action saved, Func<bool, string> chooseSavePath, ISequenceDialogCatalog catalog, IEditorDialogs dialogs)
        {
            this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.getSourceName = getSourceName; this.saved = saved;
            this.chooseSavePath = chooseSavePath ?? (saveAs => EditorUtility.SaveFilePanelInProject(
                saveAs ? EditorText.SaveAs : EditorText.Save, getSourceName(), "json", EditorText.NewPathHint));
        }
        public string ChooseSavePath(bool saveAs) => chooseSavePath(saveAs);
        public void PickDocument(Action<object> selected) => dialogs.Pick(catalog, selected);
        public void CreateDocument(Func<Type, string, bool> create) => dialogs.Create(catalog, create);
        public SequenceSwitchChoice ConfirmSwitch(bool dirty)
        {
            if (!dirty) return SequenceSwitchChoice.Discard;
            var choice = EditorUtility.DisplayDialogComplex(EditorText.UnsavedChanges, EditorText.SaveBeforeSwitch, EditorText.Save, EditorText.TipsCancel, EditorText.Discard);
            return choice == 0 ? SequenceSwitchChoice.Save : choice == 2 ? SequenceSwitchChoice.Discard : SequenceSwitchChoice.Cancel;
        }
        public void ShowExport(Func<string, string, bool> export) => dialogs.Export(catalog, getSourceName, export);
        public void Saved() => saved();
        public void Undo(bool redo) { if (redo) UnityEditor.Undo.PerformRedo(); else UnityEditor.Undo.PerformUndo(); }
    }
}
