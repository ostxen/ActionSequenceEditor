using System;

namespace NBC.ActionEditor.Application.Settings
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
        public int AutoSaveSeconds { get; }
        public string SavePath { get; }
        public float TimeStep => TimeStepMode == TimeStepMode.Frames ? 1f / FrameRate : SnapInterval;

        public EditorSettings(TimeStepMode timeStepMode, float snapInterval, int frameRate,
            bool magnetSnapping, bool scrollWheelZooms, int autoSaveSeconds, string savePath)
        {
            if (!Enum.IsDefined(typeof(TimeStepMode), timeStepMode)) throw new ArgumentOutOfRangeException(nameof(timeStepMode));
            if (float.IsNaN(snapInterval) || float.IsInfinity(snapInterval) || snapInterval < .001f)
                throw new ArgumentOutOfRangeException(nameof(snapInterval));
            if (frameRate <= 0) throw new ArgumentOutOfRangeException(nameof(frameRate));
            if (autoSaveSeconds != 0 && (autoSaveSeconds < 5 || autoSaveSeconds > 120))
                throw new ArgumentOutOfRangeException(nameof(autoSaveSeconds));
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("保存目录不能为空。", nameof(savePath));
            TimeStepMode = timeStepMode; SnapInterval = snapInterval; FrameRate = frameRate;
            MagnetSnapping = magnetSnapping; ScrollWheelZooms = scrollWheelZooms;
            AutoSaveSeconds = autoSaveSeconds; SavePath = savePath;
        }

        public bool Equals(EditorSettings other) => other != null && TimeStepMode == other.TimeStepMode
            && SnapInterval.Equals(other.SnapInterval) && FrameRate == other.FrameRate
            && MagnetSnapping == other.MagnetSnapping && ScrollWheelZooms == other.ScrollWheelZooms
            && AutoSaveSeconds == other.AutoSaveSeconds && SavePath == other.SavePath;
        public override bool Equals(object obj) => Equals(obj as EditorSettings);
        public override int GetHashCode() => HashCode.Combine(TimeStepMode, SnapInterval, FrameRate,
            MagnetSnapping, ScrollWheelZooms, AutoSaveSeconds, SavePath);
    }
}
