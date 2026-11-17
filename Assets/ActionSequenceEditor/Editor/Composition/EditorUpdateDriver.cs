using System;
using ActionSequenceEditor.Application.Settings;
using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Application.Playback;
using ActionSequenceEditor.Application.Ports;

namespace ActionSequenceEditor.Composition
{
    // 单一宿主时钟协调共享保存、播放和逐窗更新，不持有具体窗口。
    public sealed class EditorUpdateDriver
    {
        private readonly SequenceAutoSave autoSave;
        private readonly SequencePlayback playback;
        private readonly IEditorClock clock;
        private readonly EditorViewRegistry views;
        private readonly SettingsSession settings;
        private readonly Action<Exception> failed;
        public EditorUpdateDriver(SequenceAutoSave autoSave, SequencePlayback playback, IEditorClock clock,
            EditorViewRegistry views, SettingsSession settings, Action<Exception> failed)
        {
            this.autoSave = autoSave ?? throw new ArgumentNullException(nameof(autoSave));
            this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.failed = failed ?? throw new ArgumentNullException(nameof(failed));
        }

        // 保存失败不能跳过播放，播放或停止回调失败也不能阻断其他窗口。
        public void Tick()
        {
            if (views.Count == 0) return;
            try
            {
                var result = autoSave.Tick(settings.Current.AutoSaveIntervalSeconds);
                if (result.HasValue && !result.Value.Succeeded) failed(result.Value.Exception);
            }
            catch (Exception error) { failed(error); }
            try
            {
                if (playback.Update(clock.PlaybackTime, clock.TimeScale)) views.Repaint();
            }
            catch (Exception error)
            {
                try { playback.Stop(); }
                catch (Exception stopError) { failed(stopError); }
                failed(error);
            }
            finally { views.Tick(failed); }
        }
    }
}
