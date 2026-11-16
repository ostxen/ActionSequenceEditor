using System;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 将双UI播放按钮与快捷意图解释为共享播放协调操作。
    public sealed class PlaybackPresenter
    {
        private readonly IPlaybackControl control;
        public bool Playing => control.Playing;
        public bool Paused => control.Paused;
        public bool RangePlaybackEnabled => control.RangePlaybackEnabled;
        public float Time => control.Time;
        public PlaybackPresenter(IPlaybackControl control) { this.control = control ?? throw new ArgumentNullException(nameof(control)); }
        public void Seek(float time)
        {
            if (float.IsNaN(time) || float.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            control.Seek(Math.Max(0, Math.Min(control.Length, time)));
        }
        public void Invoke(PlaybackIntent intent)
        {
            switch (intent)
            {
                case PlaybackIntent.First: Seek(0); break;
                case PlaybackIntent.Previous: control.Step(false); break;
                case PlaybackIntent.TogglePlay: if (control.Playing) control.Stop(); else control.Play(); break;
                case PlaybackIntent.TogglePause: control.SetPaused(!control.Paused); break;
                case PlaybackIntent.Next: control.Step(true); break;
                case PlaybackIntent.Last: Seek(control.Length); break;
                case PlaybackIntent.ToggleRange: control.RangePlaybackEnabled = !control.RangePlaybackEnabled; break;
                default: throw new ArgumentOutOfRangeException(nameof(intent));
            }
        }
    }
}
