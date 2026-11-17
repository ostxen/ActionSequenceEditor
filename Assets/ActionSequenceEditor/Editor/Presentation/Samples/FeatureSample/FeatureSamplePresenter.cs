using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Model;
using ActionSequenceEditor.Samples.FeatureSample;
using UnityEngine;
using ActionSequenceEditor.Presentation.Contracts;

namespace ActionSequenceEditor.Presentation.Presenters
{
    // 样例预览的纯值投影，不向窗口暴露作者片段或曲线实例。
    public sealed class FeatureSampleState
    {
        public bool Available { get; }
        public float Time { get; }
        public float Length { get; }
        public Color Background { get; }
        public IReadOnlyList<SampleIntervalState> Intervals { get; }
        public IReadOnlyList<SampleLoopState> Loops { get; }
        public IReadOnlyList<SampleMarkerState> Markers { get; }
        internal FeatureSampleState(ExtensionSequenceState sequenceState, SampleIntervalState[] intervals,
            SampleLoopState[] loops, SampleMarkerState[] markers)
        {
            Available = sequenceState.Type == typeof(FeatureSampleSequence); Time = sequenceState.Time; Length = sequenceState.Length;
            Background = sequenceState.Sequence?.Field<Color>(nameof(FeatureSampleSequence.Background)) ?? default;
            Intervals = Array.AsReadOnly(intervals); Loops = Array.AsReadOnly(loops); Markers = Array.AsReadOnly(markers);
        }
    }

    // 区间预览只保留归一化位置、颜色和展示参数。
    public readonly struct SampleIntervalState
    {
        public string Caption { get; }
        public float Progress { get; }
        public float Position { get; }
        public Color Tint { get; }
        public Vector3 Offset { get; }
        internal SampleIntervalState(ExtensionNodeState clip, float time)
        {
            Caption = clip.Field<string>(nameof(FeatureSampleInterval.Caption)); Offset = clip.Field<Vector3>(nameof(FeatureSampleInterval.Offset));
            Progress = Mathf.Clamp01((time - clip.Start) / clip.Length);
            var curve = clip.Field<AnimationCurve>(nameof(FeatureSampleInterval.Curve));
            Position = Mathf.Clamp01(curve == null ? Progress : curve.Evaluate(Progress));
            var tint = clip.Field<Color>(nameof(FeatureSampleInterval.Tint));
            tint.a *= Mathf.Clamp01(clip.Weight) * Mathf.Clamp01(clip.Field<float>(nameof(FeatureSampleInterval.Gain)));
            Tint = tint;
        }
    }

    // 循环求值在显示层完成，视图只格式化次数和局部时间。
    public readonly struct SampleLoopState
    {
        public string Caption { get; }
        public int Iteration { get; }
        public float Time { get; }
        internal SampleLoopState(ExtensionNodeState clip, float time)
        {
            Caption = clip.Field<string>(nameof(FeatureSampleLoop.Caption));
            var local = (time - clip.Start - clip.LoopOffset) * clip.LoopSpeed;
            Iteration = Mathf.FloorToInt(local / clip.LoopLength) + 1;
            Time = Mathf.Repeat(local, clip.LoopLength);
        }
    }

    // 已经过的标记按时间排序输出，不执行游戏逻辑。
    public readonly struct SampleMarkerState
    {
        public float Time { get; }
        public string Message { get; }
        public int Code { get; }
        internal SampleMarkerState(ExtensionNodeState clip)
        { Time = clip.Start; Message = clip.Field<string>(nameof(FeatureSampleMarker.Message)); Code = clip.Field<int>(nameof(FeatureSampleMarker.EventCode)); }
    }

    // 从明确注入的共享播放头生成样例状态，窗口开闭不取得预览资源所有权。
    public sealed class FeatureSamplePresenter
    {
        private readonly Func<ExtensionSequenceState> capture;
        public FeatureSamplePresenter(Func<ExtensionSequenceState> capture)
        {
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        }
        public FeatureSampleState Capture()
        {
            var sequenceState = capture(); var currentTime = sequenceState.Time;
            var clips = sequenceState.Type == typeof(FeatureSampleSequence) ? sequenceState.Clips : Array.Empty<ExtensionNodeState>();
            return new FeatureSampleState(sequenceState,
                clips.Where(c => c.Type == typeof(FeatureSampleInterval) && c.Field<bool>(nameof(FeatureSampleInterval.ShowInPreview)) && currentTime >= c.Start && currentTime < c.End)
                    .Select(c => new SampleIntervalState(c, currentTime)).ToArray(),
                clips.Where(c => c.Type == typeof(FeatureSampleLoop) && currentTime >= c.Start && currentTime < c.End)
                    .Select(c => new SampleLoopState(c, currentTime)).ToArray(),
                clips.Where(c => c.Type == typeof(FeatureSampleMarker) && c.Start <= currentTime).OrderBy(c => c.Start)
                    .Select(c => new SampleMarkerState(c)).ToArray());
        }
    }
}
