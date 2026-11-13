using System;
using NBC.ActionEditor.Application.Settings;

namespace NBC.ActionEditor.Composition
{
    // 单一宿主时钟协调共享保存、播放和逐窗更新，不持有具体窗口。
    public sealed class EditorUpdateDriver
    {
        private readonly ActionEditorContext context;
        private readonly ActionEditorViews views;
        private readonly SettingsSession settings;
        private readonly Action<Exception> failed;
        public EditorUpdateDriver(ActionEditorContext context, ActionEditorViews views, SettingsSession settings, Action<Exception> failed)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
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
                var result = context.AutoSave.Tick(settings.Current.AutoSaveSeconds);
                if (result.HasValue && !result.Value.Succeeded) failed(result.Value.Exception);
            }
            catch (Exception error) { failed(error); }
            try
            {
                if (context.Playback.Update(context.Clock.PlaybackTime, context.Clock.TimeScale)) views.Repaint();
            }
            catch (Exception error)
            {
                try { context.Playback.Stop(); }
                catch (Exception stopError) { failed(stopError); }
                failed(error);
            }
            finally { views.Tick(failed); }
        }
    }
}
