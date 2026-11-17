using System;
using ActionSequenceEditor.Presentation.Contracts;

namespace ActionSequenceEditor.Views.UIToolkit
{
    internal sealed class EditorDialogAdapter : IEditorDialogs
    {
        public void Pick(ISequenceDialogCatalog catalog, Action<object> selected) => SequencePickerWindow.ShowFor(catalog, selected);
        public void Create(ISequenceDialogCatalog catalog, Func<Type, string, bool> create) => SequenceCreationWindow.ShowFor(catalog, create);
        public void Export(ISequenceDialogCatalog catalog, Func<string> name, Func<string, string, bool> export) => SequenceExportWindow.ShowFor(catalog, name, export);
        public void Rename(string initialName, Action<string> submit) => RenameWindow.ShowFor(initialName, submit);
    }
}
