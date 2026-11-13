using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 一次片段手势保存按下快照，只通过应用用例更新作者候选。
    public sealed class ClipGesture
    {
        private readonly DocumentSession session;
        private readonly ClipEditing editing;
        private readonly Dictionary<TimelineClip, float> starts = new Dictionary<TimelineClip, float>();
        private DocumentEditTransaction token;
        private TimelineClip hit;
        private float length, downX;
        private int edge;
        private float[] references = Array.Empty<float>();
        private bool moved;
        public ClipGuideState[] Guides => !Active || !moved ? Array.Empty<ClipGuideState>() : new[]
        {
            Guide(starts.Keys.Min(clip => clip.StartTime), edge <= 0),
            Guide(starts.Keys.Max(clip => clip.EndTime), edge >= 0)
        };
        private ClipGuideState Guide(float time, bool visible) => new ClipGuideState(time, references.Any(reference => Math.Abs(reference - time) < .00001f), visible);
        public bool Active => token?.IsActive == true;
        public ClipGesture(DocumentSession session, ClipEditing editing) { this.session = session; this.editing = editing; }
        public bool Begin(object owner, TimelineClip target, IEnumerable<TimelineClip> selected, int resizeEdge, float x, float playhead)
        {
            token = session.BeginEdit(owner);
            if (token == null) return false;
            hit = target; edge = resizeEdge; downX = x; length = target.Length;
            starts.Clear();
            foreach (var clip in selected.Where(clip => !clip.IsLocked)) starts[clip] = clip.StartTime;
            if (!starts.ContainsKey(hit)) starts.Add(hit, hit.StartTime);
            references = session.Document.AllTracks.SelectMany(track => track.Clips).Where(clip => !starts.ContainsKey(clip))
                .SelectMany(clip => new[] { clip.StartTime, clip.EndTime }).Concat(new[] { 0f, session.Document.Length, playhead }).Distinct().ToArray();
            return true;
        }
        public void Move(float x, bool shift, TimeTransform transform, EditorSettings settings)
        {
            if (!Active) return;
            moved |= x != downX;
            var delta = transform.Duration(x - downX);
            var original = starts[hit];
            if (edge == 0)
            {
                var candidate = Snap(original + delta, length, shift, transform.Span, settings);
                editing.Move(token, starts, candidate - original);
            }
            else if (edge < 0)
            {
                var end = original + length;
                var start = Math.Max(0, Math.Min(end - .01f, Snap(original + delta, 0, shift, transform.Span, settings)));
                editing.Resize(token, hit, start, Math.Max(.01f, end - start));
            }
            else
            {
                var end = Math.Max(original + .01f, Snap(original + length + delta, 0, shift, transform.Span, settings));
                editing.Resize(token, hit, original, Math.Max(.01f, end - original));
            }
        }
        // 网格由磁吸开关控制，Shift仅绕过邻近参考磁吸。
        private float Snap(float value, float duration, bool shift, float span, EditorSettings settings)
        {
            if (!settings.MagnetSnapping) return value;
            value = (float)Math.Round(value / settings.TimeStep, MidpointRounding.ToEven) * settings.TimeStep;
            if (shift) return value;
            var gap = span * .01f; var result = value;
            foreach (var reference in references)
            {
                var first = Math.Abs(reference - value); var last = Math.Abs(reference - value - duration);
                if (first < gap) { gap = first; result = reference; }
                if (last < gap) { gap = last; result = reference - duration; }
            }
            return result;
        }
        public void Commit()
        {
            var current = token;
            Clear();
            if (current?.IsActive != true) return;
            current.Commit("Drag timeline clips");
        }
        public void Finish()
        {
            // 非法重叠整次取消，不保留部分移动。
            var valid = starts.Keys.All(clip => clip.CanValidTime());
            if (valid) Commit(); else Cancel();
        }
        public void Cancel() { var current = token; Clear(); current?.Cancel(); }
        private void Clear() { token = null; hit = null; starts.Clear(); references = Array.Empty<float>(); moved = false; }
    }
}
