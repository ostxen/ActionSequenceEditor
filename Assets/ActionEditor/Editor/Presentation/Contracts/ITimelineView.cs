using NBC.ActionEditor.Presentation.Timeline;

namespace NBC.ActionEditor.Presentation.Contracts
{
    // 视图只消费不可变显示快照，并适配本窗口的原生捕获。
    public interface ITimelineView
    {
        void Render(TimelineRenderState state);
        void SetCapture(bool captured);
        void ShowError(string message);
    }

    // 播放头仍共享，时间轴不拥有预览资源或宿主时钟。
    public interface ITimelinePlayhead
    {
        float Time { get; }
        void Seek(float time);
    }
}
