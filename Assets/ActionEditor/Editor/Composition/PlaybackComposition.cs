using System;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 播放Presenter显式绑定共享上下文，窗口不访问静态播放器。
    public static class PlaybackComposition
    {
        private sealed class Control : IPlaybackControl
        {
            private readonly ActionEditorContext context;
            private readonly SettingsSession settings;
            public Control(ActionEditorContext context, SettingsSession settings)
            {
                this.context = context ?? throw new ArgumentNullException(nameof(context));
                this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            }
            public bool Playing => context.Playback.IsPlaying;
            public bool Paused => context.Playback.IsPaused;
            public bool Range { get => context.Playback.IsRange; set => context.Playback.IsRange = value; }
            public float Time => context.Player.CurrentTime;
            public float Length => context.Player.Length;
            public void Play() { if (!UnityEngine.Application.isPlaying) context.Playback.Play(context.Clock.PlaybackTime); }
            public void Stop() => context.Playback.Stop();
            public void Pause(bool paused) => context.Playback.Pause(paused);
            public void Seek(float time) => context.Player.Sample(time);
            public void Step(bool forward)
            {
                var interval = settings.Current.SnapInterval;
                if (forward) context.Playback.StepForward(interval);
                else context.Playback.StepBackward(interval);
            }
        }
        public static PlaybackPresenter Create(ActionEditorContext context, SettingsSession settings)
            => new PlaybackPresenter(new Control(context, settings));
    }
}
