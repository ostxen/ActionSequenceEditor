using System;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI
{
    internal sealed class EditorDialogAdapter : IEditorDialogs
    {
        public void Pick(ISequenceDialogCatalog catalog, Action<object> selected) => AssetPickerWindow.ShowObjectPicker<TextAsset>(null,
            value => selected(value), "Assets/", assetPathFilter: catalog.IsDocumentPath);
        public void Create(ISequenceDialogCatalog catalog, Func<Type, string, bool> create) => SequenceCreationWindow.Show(catalog, create);
        public void Export(ISequenceDialogCatalog catalog, Func<string> name, Func<string, string, bool> export) => SequenceExportWindow.ShowFor(catalog, name, export);
        public void Rename(string initialName, Action<string> submit) => RenameWindow.Show(initialName, submit);
    }
}
