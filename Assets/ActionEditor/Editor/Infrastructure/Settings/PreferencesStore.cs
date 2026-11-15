using System;
using System.IO;
using NBC.ActionEditor.Application.Settings;
using UnityEngine;

namespace NBC.ActionEditor.Infrastructure.Settings
{
    // 偏好文件由基础设施独占，写入失败不先改变共享偏好值。
    public sealed class PreferencesStore : IPreferencesStore
    {
        private readonly string path;
        private float margin = 180;
        [Serializable]
        private sealed class Data
        {
            public TimeStepMode TimeStepMode;
            public float SnapInterval = .1f;
            public int FrameRate = 30;
            public int AutoSaveSeconds;
            public string SavePath = "Assets/";
            public bool ScrollWheelZooms = true;
            public bool MagnetSnapping = true;
            public float TrackListLeftMargin = 180;
        }
        public PreferencesStore(string path) { this.path = path ?? throw new ArgumentNullException(nameof(path)); }
        public EditorSettings Read()
        {
            var data = File.Exists(path) ? JsonUtility.FromJson<Data>(File.ReadAllText(path)) : new Data();
            if (data == null) throw new InvalidDataException("偏好内容为空。");
            var settings = new EditorSettings(data.TimeStepMode, data.SnapInterval, data.FrameRate,
                data.MagnetSnapping, data.ScrollWheelZooms, data.AutoSaveSeconds, data.SavePath);
            margin = data.TrackListLeftMargin;
            return settings;
        }
        public void Write(EditorSettings settings)
        {
            var data = new Data
            {
                TimeStepMode = settings.TimeStepMode, SnapInterval = settings.SnapInterval, FrameRate = settings.FrameRate,
                AutoSaveSeconds = settings.AutoSaveSeconds, SavePath = settings.SavePath, ScrollWheelZooms = settings.ScrollWheelZooms,
                MagnetSnapping = settings.MagnetSnapping, TrackListLeftMargin = margin
            };
            File.WriteAllText(path, JsonUtility.ToJson(data));
        }
        public float TrackListLeftMargin => margin;
        public void WriteMargin(EditorSettings settings, float value)
        {
            var previous = margin;
            try { margin = value; Write(settings); }
            catch { margin = previous; throw; }
        }
    }
}
