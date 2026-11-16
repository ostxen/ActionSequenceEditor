using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Application.Sequences;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Model;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 一次片段手势保存按下快照，只通过应用用例更新作者候选。
    public sealed class ClipGesture
    {
        private readonly SequenceSession session;
        private readonly ClipEditing editing;
        private readonly Dictionary<ActionClip, float> originalStartTimes = new Dictionary<ActionClip, float>();
        private SequenceEditTransaction transaction;
        private ActionClip primaryClip;
        private float originalLength, downX;
        private int edge;
        private float[] snapReferenceTimes = Array.Empty<float>();
        private bool moved;
        public ClipGuideState[] Guides => !Active || !moved ? Array.Empty<ClipGuideState>() : new[]
        {
            Guide(originalStartTimes.Keys.Min(clip => clip.StartTime), edge <= 0),
            Guide(originalStartTimes.Keys.Max(clip => clip.EndTime), edge >= 0)
        };
        private ClipGuideState Guide(float time, bool visible) => new ClipGuideState(time, snapReferenceTimes.Any(reference => Math.Abs(reference - time) < .00001f), visible);
        public bool Active => transaction?.IsActive == true;
        public ClipGesture(SequenceSession session, ClipEditing editing) { this.session = session; this.editing = editing; }
        public bool Begin(object owner, ActionClip target, IEnumerable<ActionClip> selected, int resizeEdge, float x, float playhead)
        {
            transaction = session.BeginEdit(owner);
            if (transaction == null) return false;
            primaryClip = target; edge = resizeEdge; downX = x; originalLength = target.Length;
            originalStartTimes.Clear();
            foreach (var clip in selected.Where(clip => !clip.IsLocked)) originalStartTimes[clip] = clip.StartTime;
            if (!originalStartTimes.ContainsKey(primaryClip)) originalStartTimes.Add(primaryClip, primaryClip.StartTime);
            snapReferenceTimes = session.Sequence.AllTracks.SelectMany(track => track.Clips).Where(clip => !originalStartTimes.ContainsKey(clip))
                .SelectMany(clip => new[] { clip.StartTime, clip.EndTime }).Concat(new[] { 0f, session.Sequence.Length, playhead }).Distinct().ToArray();
            return true;
        }
        public void Move(float x, bool shift, TimeTransform transform, EditorSettings settings)
        {
            if (!Active) return;
            moved |= x != downX;
            var delta = transform.Duration(x - downX);
            var originalStartTime = originalStartTimes[primaryClip];
            if (edge == 0)
            {
                var candidate = Snap(originalStartTime + delta, originalLength, shift, transform.Span, settings);
                editing.Move(transaction, originalStartTimes, candidate - originalStartTime);
            }
            else if (edge < 0)
            {
                var end = originalStartTime + originalLength;
                var start = Math.Max(0, Math.Min(end - ClipTimingRules.MinimumLength, Snap(originalStartTime + delta, 0, shift, transform.Span, settings)));
                editing.Resize(transaction, primaryClip, start, Math.Max(ClipTimingRules.MinimumLength, end - start));
            }
            else
            {
                var end = Math.Max(originalStartTime + ClipTimingRules.MinimumLength, Snap(originalStartTime + originalLength + delta, 0, shift, transform.Span, settings));
                editing.Resize(transaction, primaryClip, originalStartTime, Math.Max(ClipTimingRules.MinimumLength, end - originalStartTime));
            }
        }
        // 网格由磁吸开关控制，Shift仅绕过邻近参考磁吸。
        private float Snap(float value, float duration, bool shift, float span, EditorSettings settings)
        {
            if (!settings.MagnetSnapping) return value;
            value = (float)Math.Round(value / settings.TimeStep, MidpointRounding.ToEven) * settings.TimeStep;
            if (shift) return value;
            var snapThreshold = span * .01f; var result = value;
            foreach (var reference in snapReferenceTimes)
            {
                var startDistance = Math.Abs(reference - value); var endDistance = Math.Abs(reference - value - duration);
                if (startDistance < snapThreshold) { snapThreshold = startDistance; result = reference; }
                if (endDistance < snapThreshold) { snapThreshold = endDistance; result = reference - duration; }
            }
            return result;
        }
        public void Finish()
        {
            var current = transaction;
            var candidates = originalStartTimes.Keys.ToArray();
            Clear();
            if (current?.IsActive == true) editing.Finish(current, candidates);
        }
        public void Cancel() { var current = transaction; Clear(); current?.Cancel(); }
        private void Clear() { transaction = null; primaryClip = null; originalStartTimes.Clear(); snapReferenceTimes = Array.Empty<float>(); moved = false; }
    }
}
