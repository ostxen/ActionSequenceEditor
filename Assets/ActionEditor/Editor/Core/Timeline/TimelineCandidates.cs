using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBC.ActionEditor
{
    public static class TimelineCandidates
    {
        public static float ClampMoveDelta(float requested, IEnumerable<float> starts) => Mathf.Max(requested, -starts.Min());
        public static bool Valid(IEnumerable<TimelineClip> clips) => clips.All(clip => clip == null || clip.CanValidTime());
        public static float[] References(IEnumerable<TimelineClip> clips, IEnumerable<TimelineClip> selected, float length, float playhead, bool seedsFirst)
        {
            var excluded = new HashSet<TimelineClip>(selected);
            var edges = clips.Where(clip => !excluded.Contains(clip)).SelectMany(clip => new[] { clip.StartTime, clip.EndTime });
            var seeds = new[] { 0f, length, playhead };
            return (seedsFirst ? seeds.Concat(edges) : edges.Concat(seeds)).Distinct().ToArray();
        }
        public static Vector2 Resize(Vector2 initial, bool left, float candidate, float minimumLength, float minimum, float maximum, Func<float, float> quantizeDelta = null)
        {
            var end = initial.x + initial.y;
            if (left)
            {
                var start = quantizeDelta == null ? Mathf.Clamp(candidate, minimum, Mathf.Min(end - minimumLength, maximum))
                    : candidate > maximum ? maximum : candidate < minimum ? minimum : candidate;
                return new Vector2(start, quantizeDelta == null ? end - start : initial.y + quantizeDelta(initial.x - start));
            }
            var target = quantizeDelta == null ? Mathf.Clamp(candidate, Mathf.Max(initial.x + minimumLength, minimum), maximum)
                : candidate > maximum ? maximum : candidate < minimum ? minimum : candidate;
            return new Vector2(initial.x, target - initial.x);
        }
        // Original starts win equal distances and include the boundary. Toolkit preserves candidate order and excludes it.
        public static float Magnet(float start, float length, IEnumerable<float> times, float threshold, bool originalOrder)
        {
            if (originalOrder)
            {
                float Nearest(float time)
                {
                    var best = float.PositiveInfinity; var result = -1f;
                    foreach (var reference in times)
                    {
                        var distance = Mathf.Abs(reference - time);
                        if (distance < best) { best = distance; result = reference; }
                    }
                    return best <= threshold ? result : -1;
                }
                var first = Nearest(start); var last = Nearest(start + length);
                if (first >= 0 && last > 0) return Mathf.Abs(last - start - length) < Mathf.Abs(first - start) ? last - length : first;
                if (first >= 0) return first;
                return last >= 0 ? last - length : start;
            }
            var gap = threshold; var value = start;
            foreach (var reference in times)
            {
                var a = Mathf.Abs(reference - start); var b = Mathf.Abs(reference - start - length);
                if (a < gap) { gap = a; value = reference; }
                if (b < gap) { gap = b; value = reference - length; }
            }
            return Mathf.Max(0, value);
        }
    }
}
