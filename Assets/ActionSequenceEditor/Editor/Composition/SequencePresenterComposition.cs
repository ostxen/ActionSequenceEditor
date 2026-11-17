using System;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Presenters;

namespace ActionSequenceEditor.Composition
{
    // 组合根只连接显式依赖，文档操作顺序由Presenter负责。
    public static class SequencePresenterComposition
    {
        public static SequencePresenter Create(Application.Sequences.SequenceSession session, Application.Sequences.SequenceLifecycle lifecycle,
            ISequenceNavigation navigation, Func<string> getSourceName, Action saved, Func<bool, string> chooseSavePath = null, Views.IEditorDialogs dialogs = null)
            => new SequencePresenter(session, lifecycle, new ActionSequenceEditor.Views.SequenceInteraction(getSourceName, saved, chooseSavePath,
                new Infrastructure.Sequences.SequenceDialogCatalog(), dialogs ?? new Views.IMGUI.EditorDialogAdapter()), navigation);
        public static void PopulateSaveMenu(SequencePresenter sequencePresenter, Action<string, Action> add)
        {
            add(EditorText.Save, () => sequencePresenter.Invoke(SequenceIntent.Save));
            add(EditorText.SaveAs, () => sequencePresenter.Invoke(SequenceIntent.SaveAs));
            add(EditorText.Export, () => sequencePresenter.Invoke(SequenceIntent.Export));
        }
    }
}