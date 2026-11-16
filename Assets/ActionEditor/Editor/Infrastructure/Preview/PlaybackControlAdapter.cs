using System;
using NBC.ActionEditor.Application.Playback;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Infrastructure.Preview
{
    // 连接应用播放、时钟和偏好，不引用具体采样器或默认宿主。
    public sealed class PlaybackControlAdapter : IPlaybackControl
    {
        private readonly SequencePlayback playback;
        private readonly ISequenceSampler sampler;
        private readonly IEditorClock clock;
        private readonly SettingsSession settings;
        private readonly Func<bool> canPlay;
        public PlaybackControlAdapter(SequencePlayback playback, ISequenceSampler sampler, IEditorClock clock, SettingsSession settings, Func<bool> canPlay)
        {
            this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.canPlay = canPlay ?? throw new ArgumentNullException(nameof(canPlay));
        }
        public bool Playing => playback.IsPlaying;
        public bool Paused => playback.IsPaused;
        public bool RangePlaybackEnabled { get => playback.RangePlaybackEnabled; set => playback.RangePlaybackEnabled = value; }
        public float Time => sampler.CurrentTime;
        public float Length => sampler.Length;
        public void Play() { if (canPlay()) playback.Play(clock.PlaybackTime); }
        public void Stop() => playback.Stop();
        public void SetPaused(bool paused) => playback.SetPaused(paused);
        public void Seek(float time) => sampler.Sample(time);
        public void Step(bool forward)
        {
            var interval = settings.Current.SnapInterval;
            if (forward) playback.StepForward(interval);
            else playback.StepBackward(interval);
        }
    }
}
