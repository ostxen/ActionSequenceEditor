#if UNITY_EDITOR

using System;
using UnityEngine;
using System.IO;


namespace NBC.ActionEditor
{
    // 共享偏好会话与原有键的入口，不承担业务类型发现或创建目录。
    public static class Prefs
    {
        public static readonly string CONFIG_PATH =
            $"{UnityEngine.Application.dataPath}/../ProjectSettings/WhootActionSequencer.txt";

        [Serializable]
        public enum TimeStepMode
        {
            Seconds,
            Frames
        }

        private static readonly Infrastructure.PreferencesStore store = new Infrastructure.PreferencesStore(CONFIG_PATH);
        private static Application.Settings.SettingsSession session;
        public static Application.Settings.SettingsSession Session => session ??= new Application.Settings.SettingsSession(store);
        private static Application.Settings.EditorSettings Current => Session.Current;

        // 设置入口提交共享不可变值，写入失败不改变当前偏好。
        private static void Change(TimeStepMode? mode = null, float? snap = null, int? fps = null,
            int? autoSave = null, string path = null, bool? wheel = null, bool? magnet = null)
        {
            var value = Current;
            var candidate = new Application.Settings.EditorSettings((Application.Settings.TimeStepMode)(mode ?? timeStepMode),
                snap ?? value.SnapInterval, fps ?? value.FrameRate, magnet ?? value.MagnetSnapping,
                wheel ?? value.ScrollWheelZooms, autoSave ?? value.AutoSaveSeconds, path ?? value.SavePath);
            var result = Session.Commit(candidate);
            if (!result.Succeeded) throw new IOException(result.Error);
        }
        public static readonly float[] snapIntervals = { .001f, .01f, .1f };
        public static readonly int[] frameRates = { 24, 25, 30, 60 };
        public static bool scrollWheelZooms { get => Current.ScrollWheelZooms; set => Change(wheel: value); }
        public static int autoSaveSeconds { get => Current.AutoSaveSeconds; set => Change(autoSave: value); }
        public static string savePath { get => Current.SavePath; set => Change(path: value); }
        public static bool MagnetSnapping { get => Current.MagnetSnapping; set => Change(magnet: value); }
        public static float trackListLeftMargin
        {
            get { _ = Current; return store.TrackListLeftMargin; }
            set { if (Math.Abs(trackListLeftMargin - value) > .001f) store.WriteMargin(Current, value); }
        }
        public static TimeStepMode timeStepMode
        {
            get => (TimeStepMode)Current.TimeStepMode;
            set { if (value != timeStepMode) { var rate = value == TimeStepMode.Frames ? 30 : 10; Change(mode: value, fps: rate, snap: 1f / rate); } }
        }
        public static int FrameRate { get => Current.FrameRate; set => Change(fps: value, snap: 1f / value); }
        public static float SnapInterval { get => Current.SnapInterval; set => Change(snap: Mathf.Max(value, .001f)); }

    }
}

#endif