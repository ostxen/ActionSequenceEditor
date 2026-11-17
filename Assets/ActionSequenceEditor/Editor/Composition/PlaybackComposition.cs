using ActionSequenceEditor.Application.Playback;
using ActionSequenceEditor.Application.Ports;
using ActionSequenceEditor.Application.Settings;
using ActionSequenceEditor.Presentation.Presenters;

namespace ActionSequenceEditor.Composition
{
    // 播放组合根只连接具体采样、时钟和设置，不处理播放决策。
    public static class PlaybackComposition
    {
        public static PlaybackPresenter Create(SequencePlayback playback, ISequenceSampler sampler, IEditorClock clock, SettingsSession settings)
            => new PlaybackPresenter(new ActionSequenceEditor.Infrastructure.Preview.PlaybackControlAdapter(playback, sampler, clock, settings, () => !UnityEngine.Application.isPlaying));
    }
}
