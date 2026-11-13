using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Samples.FeatureSample;
using UnityEngine;

namespace NBC.ActionEditor.Presentation.Presenters
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
        internal FeatureSampleState(float time, FeatureSampleDocument document, SampleIntervalState[] intervals,
            SampleLoopState[] loops, SampleMarkerState[] markers)
        {
            Available = document != null; Time = time; Length = document?.Length ?? 0;
            Background = document?.Background ?? default;
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
        internal SampleIntervalState(FeatureSampleInterval clip, float time)
        {
            Caption = clip.Caption; Offset = clip.Offset;
            Progress = Mathf.Clamp01((time - clip.StartTime) / clip.Length);
            Position = Mathf.Clamp01(clip.Curve == null ? Progress : clip.Curve.Evaluate(Progress));
            var tint = clip.Tint;
            tint.a *= Mathf.Clamp01(clip.GetClipWeight(time - clip.StartTime)) * Mathf.Clamp01(clip.Gain);
            Tint = tint;
        }
    }

    // 循环求值在显示层完成，视图只格式化次数和局部时间。
    public readonly struct SampleLoopState
    {
        public string Caption { get; }
        public int Iteration { get; }
        public float Time { get; }
        internal SampleLoopState(FeatureSampleLoop clip, float time)
        {
            Caption = clip.Caption;
            var local = (time - clip.StartTime - clip.SubClipOffset) * clip.SubClipSpeed;
            Iteration = Mathf.FloorToInt(local / clip.SubClipLength) + 1;
            Time = Mathf.Repeat(local, clip.SubClipLength);
        }
    }

    // 已经过的标记按时间排序输出，不执行游戏逻辑。
    public readonly struct SampleMarkerState
    {
        public float Time { get; }
        public string Message { get; }
        public int Code { get; }
        internal SampleMarkerState(FeatureSampleMarker clip)
        { Time = clip.StartTime; Message = clip.Message; Code = clip.EventCode; }
    }

    // 从明确注入的共享播放头生成样例状态，窗口开闭不取得预览资源所有权。
    public sealed class FeatureSamplePresenter
    {
        private readonly Func<ActionDocument> document;
        private readonly Func<float> time;
        public FeatureSamplePresenter(Func<ActionDocument> document, Func<float> time)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
        }
        public FeatureSampleState Capture()
        {
            var asset = document() as FeatureSampleDocument;
            var current = time();
            var clips = asset == null ? Array.Empty<TimelineClip>() : asset.AllTracks.Where(t => t.IsActive).SelectMany(t => t.Clips).ToArray();
            return new FeatureSampleState(current, asset,
                clips.OfType<FeatureSampleInterval>().Where(c => c.ShowInPreview && current >= c.StartTime && current < c.EndTime)
                    .Select(c => new SampleIntervalState(c, current)).ToArray(),
                clips.OfType<FeatureSampleLoop>().Where(c => current >= c.StartTime && current < c.EndTime)
                    .Select(c => new SampleLoopState(c, current)).ToArray(),
                clips.OfType<FeatureSampleMarker>().Where(c => c.StartTime <= current).OrderBy(c => c.StartTime)
                    .Select(c => new SampleMarkerState(c)).ToArray());
        }
    }
}
