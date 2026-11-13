using System;
using NBC.ActionEditor.Application.Ports;

namespace NBC.ActionEditor.Application.Documents
{
    // 自动保存只调度已有来源的作者会话，失败节流且不弹路径对话框。
    public sealed class DocumentAutoSave
    {
        private readonly DocumentSession session;
        private readonly DocumentLifecycle lifecycle;
        private readonly IEditorClock clock;
        private double lastAttempt;
        public DateTime LastSaved { get; private set; }

        public DocumentAutoSave(DocumentSession session, DocumentLifecycle lifecycle, IEditorClock clock)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            lastAttempt = clock.Now; LastSaved = clock.LocalTime;
        }
        public void RecordSaved() { lastAttempt = clock.Now; LastSaved = clock.LocalTime; }

        // 返回null表示本次没有保存请求；已提交但导入失败保留原用例结果。
        public DocumentOperationResult? Tick(double interval)
        {
            if (interval <= 0 || double.IsNaN(interval) || double.IsInfinity(interval)
                || session.IsEditing || session.Document == null || string.IsNullOrEmpty(session.Path) || !session.IsDirty)
                return null;
            var now = clock.Now;
            if (now < lastAttempt) { lastAttempt = now; return null; }
            if (now - lastAttempt < interval) return null;
            lastAttempt = now;
            var result = lifecycle.Save();
            if (result.Succeeded) RecordSaved();
            return result;
        }
    }
}
