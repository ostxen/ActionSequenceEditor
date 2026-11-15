using System;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 组合根只连接显式依赖，文档操作顺序由Presenter负责。
    public static class DocumentPresenterComposition
    {
        public static DocumentPresenter Create(Application.Documents.DocumentSession session, Application.Documents.DocumentLifecycle lifecycle,
            IDocumentNavigation navigation, Func<string> documentName, Action saved, Func<bool, string> chooseSavePath = null)
            => new DocumentPresenter(session, lifecycle, new NBC.ActionEditor.Views.DocumentInteraction(documentName, saved, chooseSavePath,
                new Infrastructure.Documents.DocumentDialogCatalog()), navigation);
        public static void PopulateSaveMenu(DocumentPresenter documents, Action<string, Action> add)
        {
            add(Lan.Save, () => documents.Invoke(DocumentIntent.Save));
            add(Lan.SaveAs, () => documents.Invoke(DocumentIntent.SaveAs));
            add(Lan.Export, () => documents.Invoke(DocumentIntent.Export));
        }
    }
}