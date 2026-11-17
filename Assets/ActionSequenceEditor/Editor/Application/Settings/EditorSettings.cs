using System;

namespace ActionSequenceEditor.Application.Settings
{
    public enum TimeStepMode { Seconds, Frames }

    // 表达编辑偏好的不可变值，不持有文档或窗口状态。
    public sealed class EditorSettings : IEquatable<EditorSettings>
    {
        public TimeStepMode TimeStepMode { get; }
        public float SnapInterval { get; }
        public int FrameRate { get; }
        public bool MagnetSnapping { get; }
        public bool ScrollWheelZooms { get; }
        public int AutoSaveIntervalSeconds { get; }
        public string DefaultSaveDirectory { get; }
        public float TimeStep => TimeStepMode == TimeStepMode.Frames ? 1f / FrameRate : SnapInterval;

        public EditorSettings(TimeStepMode timeStepMode, float snapInterval, int frameRate,
            bool magnetSnapping, bool scrollWheelZooms, int autoSaveIntervalSeconds, string defaultSaveDirectory)
        {
            if (!Enum.IsDefined(typeof(TimeStepMode), timeStepMode)) throw new ArgumentOutOfRangeException(nameof(timeStepMode));
            if (float.IsNaN(snapInterval) || float.IsInfinity(snapInterval) || snapInterval < .001f)
                throw new ArgumentOutOfRangeException(nameof(snapInterval));
            if (frameRate <= 0) throw new ArgumentOutOfRangeException(nameof(frameRate));
            if (autoSaveIntervalSeconds != 0 && (autoSaveIntervalSeconds < 5 || autoSaveIntervalSeconds > 120))
                throw new ArgumentOutOfRangeException(nameof(autoSaveIntervalSeconds));
            if (string.IsNullOrWhiteSpace(defaultSaveDirectory)) throw new ArgumentException("保存目录不能为空。", nameof(defaultSaveDirectory));
            TimeStepMode = timeStepMode; SnapInterval = snapInterval; FrameRate = frameRate;
            MagnetSnapping = magnetSnapping; ScrollWheelZooms = scrollWheelZooms;
            AutoSaveIntervalSeconds = autoSaveIntervalSeconds; DefaultSaveDirectory = defaultSaveDirectory;
        }

        public bool Equals(EditorSettings other) => other != null && TimeStepMode == other.TimeStepMode
            && SnapInterval.Equals(other.SnapInterval) && FrameRate == other.FrameRate
            && MagnetSnapping == other.MagnetSnapping && ScrollWheelZooms == other.ScrollWheelZooms
            && AutoSaveIntervalSeconds == other.AutoSaveIntervalSeconds && DefaultSaveDirectory == other.DefaultSaveDirectory;
        public override bool Equals(object obj) => Equals(obj as EditorSettings);
        public override int GetHashCode() => HashCode.Combine(TimeStepMode, SnapInterval, FrameRate,
            MagnetSnapping, ScrollWheelZooms, AutoSaveIntervalSeconds, DefaultSaveDirectory);
    }
}
