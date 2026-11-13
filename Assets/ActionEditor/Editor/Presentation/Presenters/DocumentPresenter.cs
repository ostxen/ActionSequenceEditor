using System;
using System.Runtime.ExceptionServices;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 文档按钮、菜单和快捷键共用的意图协调，不承担路径存储实现。
    public sealed class DocumentPresenter
    {
        private readonly DocumentSession session;
        private readonly DocumentLifecycle lifecycle;
        private readonly IDocumentInteraction interaction;
        public bool Dirty => session.IsDirty;
        public bool HasDocument => session.Document != null;
        public DocumentPresenter(DocumentSession session, DocumentLifecycle lifecycle, IDocumentInteraction interaction)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle)); this.interaction = interaction ?? throw new ArgumentNullException(nameof(interaction)); }

        public void Invoke(DocumentIntent intent)
        {
            // 切换也不能隐式结束另一窗口持有的作者手势。
            if (session.IsEditing && (intent == DocumentIntent.Create || intent == DocumentIntent.Open || intent == DocumentIntent.Close))
                throw new InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
            switch (intent)
            {
                case DocumentIntent.Create: if (interaction.ConfirmSwitch()) interaction.CreateDocument(); break;
                case DocumentIntent.Open: if (interaction.ConfirmSwitch()) interaction.PickDocument(); break;
                case DocumentIntent.Close: if (interaction.ConfirmSwitch()) interaction.CloseDocument(); break;
                case DocumentIntent.Save: Save(false); break;
                case DocumentIntent.SaveAs: Save(true); break;
                case DocumentIntent.Export: RequireExport(); interaction.ShowExport(); break;
                case DocumentIntent.Undo: interaction.Undo(false); break;
                case DocumentIntent.Redo: interaction.Undo(true); break;
                default: throw new ArgumentOutOfRangeException(nameof(intent));
            }
        }
        // 活动编辑拒绝在选择路径前发生，取消不写盘也不推进保存标记。
        private void Save(bool saveAs)
        {
            if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再保存。");
            var path = saveAs || string.IsNullOrEmpty(session.Path) ? interaction.ChooseSavePath(saveAs) : session.Path;
            if (string.IsNullOrEmpty(path)) return;
            var result = saveAs ? lifecycle.SaveAs(path) : lifecycle.Save(path);
            if (!result.Succeeded) ExceptionDispatchInfo.Capture(result.Exception).Throw();
            interaction.Saved();
        }
        public void Discard() => session.DiscardChanges();

        public void RequireExport()
        {
            if (!HasDocument) throw new InvalidOperationException("没有打开的文档。");
            if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再导出。");
        }
        public bool Export(string target, string formatId)
        {
            var result = lifecycle.Export(target, formatId);
            if (!result.Succeeded && !result.Cancelled) ExceptionDispatchInfo.Capture(result.Exception).Throw();
            return result.Succeeded;
        }
        public bool Create(Type type, string path)
        {
            if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再创建文档。");
            var result = lifecycle.Create(type, path);
            if (!result.Succeeded && !result.Cancelled) ExceptionDispatchInfo.Capture(result.Exception).Throw();
            return result.Succeeded;
        }
    }
}
