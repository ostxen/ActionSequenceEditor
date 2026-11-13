using System;
using NBC.ActionEditor.Application.Documents;

namespace NBC.ActionEditor.Application.Editing
{
    // 执行前重新校验同一操作，变更由会话统一撤销与回滚。
    public sealed class EditOperation
    {
        private readonly DocumentSession session;
        private readonly string label;
        private readonly Func<string> validate;
        private readonly Action execute;
        public EditOperation(DocumentSession session, string label, Func<string> validate, Action execute)
        { this.session = session; this.label = label; this.validate = validate; this.execute = execute; }
        public bool CanExecute(out string reason) { reason = validate(); return reason == null; }
        public void Execute()
        {
            if (!CanExecute(out var reason)) throw new InvalidOperationException(reason);
            session.Edit(label, () =>
            {
                if (!CanExecute(out reason)) throw new InvalidOperationException(reason);
                execute();
            });
        }
    }
}
