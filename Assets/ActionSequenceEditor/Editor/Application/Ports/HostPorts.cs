using System;

namespace ActionSequenceEditor.Application.Ports
{
    // 单调时钟驱动调度，墙钟仅用于显示保存时间。
    public interface IEditorClock
    {
        double Now { get; }
        float PlaybackTime { get; }
        float TimeScale { get; }
        DateTime LocalTime { get; }
    }

    // 重载恢复的宿主存储与作者保存目标相互独立。
    public interface IRecoveryStore
    {
        string Read();
        void Write(string snapshot);
    }

    // 播放只消费采样能力与时间范围，不持有具体预览资源或作者图。
    public interface ISequenceSampler
    {
        event Action Faulted;
        bool HasSequence { get; }
        float CurrentTime { get; set; }
        float Length { get; }
        float RangeMin { get; }
        float RangeMax { get; }
        void Sample(float time);
        void Reset();
    }
}
