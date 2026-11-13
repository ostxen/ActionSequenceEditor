using System;

namespace NBC.ActionEditor.Application.Documents
{
    // 一次编辑令牌只对创建它的会话和发起者有效。
    public sealed class DocumentEditTransaction : IDisposable
    {
        private readonly DocumentSession session;
        internal object Owner { get; }
        internal DocumentEditTransaction(DocumentSession session, object owner) { this.session = session; Owner = owner; }
        public bool IsActive => session.Owns(this);
        public bool Update(Action action)
        {
            if (!IsActive) return false;
            try { action(); return true; }
            catch { Cancel(); throw; }
        }
        public void Commit(string label = "Edit document") => session.Commit(this, label);
        public void Cancel() => session.Cancel(this);
        public void Dispose() => Cancel();
    }
}
