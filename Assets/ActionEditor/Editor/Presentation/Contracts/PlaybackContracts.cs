namespace NBC.ActionEditor.Presentation.Contracts
{
    // 播放协调能力由宿主适配，Presenter不持有Unity预览资源。
    public interface IPlaybackControl
    {
        bool Playing { get; }
        bool Paused { get; }
        bool Range { get; set; }
        float Time { get; }
        float Length { get; }
        void Play();
        void Stop();
        void Pause(bool paused);
        void Seek(float time);
        void Step(bool forward);
    }
    public enum PlaybackIntent { First, Previous, TogglePlay, TogglePause, Next, Last, ToggleRange }
}
