using System;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 协调一个偏好视图的草稿、提交和取消。
    public sealed class SettingsPresenter : IDisposable
    {
        private readonly SettingsSession session;
        private readonly ISettingsView view;
        private EditorSettings draft;
        private EditorSettings baseline;
        private bool disposed;

        public SettingsPresenter(SettingsSession session, ISettingsView view)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            session.Changed += OnChanged;
            try { view.Render(session.Current); }
            catch { session.Changed -= OnChanged; throw; }
        }

        public void Preview(EditorSettings candidate)
        {
            ThrowIfDisposed();
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            baseline ??= session.Current;
            draft = candidate;
            view.ShowError(null); view.Render(draft);
        }

        // 陈旧草稿不能覆盖另一窗口已经提交的偏好。
        public SettingsResult Apply()
        {
            ThrowIfDisposed();
            if (draft == null) return SettingsResult.Success(false);
            if (!baseline.Equals(session.Current))
            {
                Cancel();
                const string conflict = "偏好已由其他窗口修改，请重新编辑。";
                view.ShowError(conflict);
                return SettingsResult.Failure(conflict);
            }
            var result = session.Commit(draft);
            if (result.Succeeded) { draft = baseline = null; view.ShowError(result.Warning); view.Render(session.Current); }
            else view.ShowError(result.Error);
            return result;
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            draft = baseline = null; view.ShowError(null); view.Render(session.Current);
        }

        private void OnChanged()
        {
            if (draft == null) view.Render(session.Current);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SettingsPresenter));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.Changed -= OnChanged;
            draft = baseline = null;
        }
    }
}
