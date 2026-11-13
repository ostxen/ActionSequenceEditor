using System;
using System.Collections.Generic;

namespace NBC.ActionEditor.Application.Settings
{
    // 隔离偏好的持久化载体。
    public interface IPreferencesStore
    {
        EditorSettings Read();
        void Write(EditorSettings settings);
    }

    // 共享偏好提交结果，不把存储异常变成成功通知。
    public readonly struct SettingsResult
    {
        public bool Succeeded { get; }
        public bool Changed { get; }
        public string Error { get; }
        public string Warning { get; }
        private SettingsResult(bool succeeded, bool changed, string error, string warning)
        { Succeeded = succeeded; Changed = changed; Error = error; Warning = warning; }
        public static SettingsResult Success(bool changed, string warning = null) => new SettingsResult(true, changed, null, warning);
        public static SettingsResult Failure(string error) => new SettingsResult(false, false, error, null);
    }

    // 负责共享偏好提交和成功后的变更通知。
    public sealed class SettingsSession
    {
        private readonly IPreferencesStore store;
        private bool committing;
        public EditorSettings Current { get; private set; }
        public event Action Changed;

        public SettingsSession(IPreferencesStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Current = store.Read() ?? throw new InvalidOperationException("偏好存储返回空值。");
        }

        // 持久化成功才发布状态，失败保留原值。
        public SettingsResult Commit(EditorSettings candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (committing) return SettingsResult.Failure("偏好正在提交。");
            if (Current.Equals(candidate)) return SettingsResult.Success(false);
            committing = true;
            try
            {
                try { store.Write(candidate); }
                catch (Exception error) { return SettingsResult.Failure(error.Message); }
                Current = candidate;
                // 显示订阅失败不能把已落盘的提交变成失败，也不能阻断其他视图。
                var errors = new List<string>();
                foreach (var listener in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
                {
                    try { ((Action)listener)(); }
                    catch (Exception error) { errors.Add(error.Message); }
                }
                return SettingsResult.Success(true, errors.Count == 0 ? null : string.Join("; ", errors));
            }
            finally { committing = false; }
        }
    }
}
