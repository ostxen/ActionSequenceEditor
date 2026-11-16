using System;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Views
{
    // 宿主只选择自己的交互实现，不向Presenter暴露窗口或控件。
    public interface IEditorDialogHost
    {
        IEditorDialogs Dialogs { get; }
    }
    public interface IEditorDialogs
    {
        void Pick(ISequenceDialogCatalog catalog, Action<object> selected);
        void Create(ISequenceDialogCatalog catalog, Func<Type, string, bool> create);
        void Export(ISequenceDialogCatalog catalog, Func<string> name, Func<string, string, bool> export);
        void Rename(string initialName, Action<string> submit);
    }
}
