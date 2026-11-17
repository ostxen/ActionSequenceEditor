using ActionSequenceEditor.Application.Settings;

namespace ActionSequenceEditor.Composition
{
    // 仅创建偏好会话；默认实例归宿主私有持有，不提供静态业务读写。
    public static class SettingsSessionComposition
    {
        public static SettingsSession Create() => new SettingsSession(new Infrastructure.Settings.SettingsFileStore(
            System.IO.Path.Combine(UnityEngine.Application.dataPath, "../ProjectSettings/WhootActionSequencer.txt")));
    }
}