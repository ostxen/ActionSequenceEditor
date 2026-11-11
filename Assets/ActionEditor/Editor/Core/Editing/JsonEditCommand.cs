using System;

namespace NBC.ActionEditor.Storage
{
    /// <summary>A narrow, revalidated authoring operation, not a command bus.</summary>
    public sealed class JsonEditCommand
    {
        private readonly string label;
        private readonly Func<string> validate;
        private readonly Action execute;
        internal JsonEditCommand(string label, Func<string> validate, Action execute)
        { this.label = label; this.validate = validate; this.execute = execute; }
        public bool CanExecute(out string reason) { reason = validate(); return reason == null; }
        public void Execute()
        {
            if (!CanExecute(out var reason)) throw new InvalidOperationException(reason);
            App.Session.Edit(label, () =>
            {
                if (!CanExecute(out reason)) throw new InvalidOperationException(reason);
                execute();
            });
        }
    }
}
