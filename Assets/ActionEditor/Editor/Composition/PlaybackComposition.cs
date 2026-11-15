using NBC.ActionEditor.Application.Playback;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 播放组合根只连接具体采样、时钟和设置，不处理播放决策。
    public static class PlaybackComposition
    {
        public static PlaybackPresenter Create(DocumentPlayback playback, IDocumentSampler sampler, IEditorClock clock, SettingsSession settings)
            => new PlaybackPresenter(new NBC.ActionEditor.Infrastructure.Preview.PlaybackControl(playback, sampler, clock, settings, () => !UnityEngine.Application.isPlaying));
    }
}
