using NBC.ActionEditor.Application.Settings;

namespace NBC.ActionEditor.Composition
{
    // 仅创建偏好会话；默认实例归宿主私有持有，不提供静态业务读写。
    public static class PreferencesComposition
    {
        public static SettingsSession Create() => new SettingsSession(new Infrastructure.Settings.PreferencesStore(
            System.IO.Path.Combine(UnityEngine.Application.dataPath, "../ProjectSettings/WhootActionSequencer.txt")));
    }
}