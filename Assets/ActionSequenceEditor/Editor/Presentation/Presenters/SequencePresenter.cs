using System;
using System.Runtime.ExceptionServices;
using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Presentation.Contracts;

namespace ActionSequenceEditor.Presentation.Presenters
{
    // 文档按钮、菜单和快捷键共用的意图协调，不承担路径存储实现。
    public sealed class SequencePresenter
    {
        private readonly SequenceSession session;
        private readonly SequenceLifecycle lifecycle;
        private readonly ISequenceInteraction interaction;
        private readonly ISequenceNavigation navigation;
        public bool Dirty => session.IsDirty;
        public bool HasSequence => session.Sequence != null;
        public Type SequenceType => session.Sequence?.GetType();
        public event Action Changed { add => session.Changed += value; remove => session.Changed -= value; }
        public SequencePresenter(SequenceSession session, SequenceLifecycle lifecycle, ISequenceInteraction interaction, ISequenceNavigation navigation)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public void Invoke(SequenceIntent intent)
        {
            // 切换也不能隐式结束另一窗口持有的作者手势。
            if (session.IsEditing && (intent == SequenceIntent.Create || intent == SequenceIntent.Open || intent == SequenceIntent.Close))
                throw new InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
            switch (intent)
            {
                case SequenceIntent.Create: interaction.CreateDocument(Create); break;
                case SequenceIntent.Open: interaction.PickDocument(OpenPicked); break;
                case SequenceIntent.Close: if (ConfirmSwitch()) navigation.Close(); break;
                case SequenceIntent.Save: Save(false); break;
                case SequenceIntent.SaveAs: Save(true); break;
                case SequenceIntent.Export: RequireExport(); interaction.ShowExport(Export); break;
                case SequenceIntent.Undo: interaction.Undo(false); break;
                case SequenceIntent.Redo: interaction.Undo(true); break;
                default: throw new ArgumentOutOfRangeException(nameof(intent));
            }
        }
        public bool ConfirmSwitch()
        {
            RequireSwitch();
            var choice = interaction.ConfirmSwitch(session.IsDirty);
            if (choice == SequenceSwitchChoice.Cancel) return false;
            if (choice == SequenceSwitchChoice.Save) Save(false);
            RequireSwitch();
            return choice == SequenceSwitchChoice.Discard || !session.IsDirty;
        }
        private void RequireSwitch()
        {
            if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
        }
        public void OpenPicked(object source)
        {
            RequireSwitch();
            if (source == null || !navigation.Accepts(source) || navigation.IsCurrent(source)) return;
            if (ConfirmSwitch()) navigation.Open(source);
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
            if (!HasSequence) throw new InvalidOperationException("没有打开的文档。");
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
            if (string.IsNullOrEmpty(path)) return false;
            if (!ConfirmSwitch()) return false;
            var result = lifecycle.Create(type, path);
            if (!result.Succeeded && !result.Cancelled) ExceptionDispatchInfo.Capture(result.Exception).Throw();
            if (result.Succeeded) navigation.OpenPath(path);
            return result.Succeeded;
        }
    }
}
