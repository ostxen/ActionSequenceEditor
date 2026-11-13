using NBC.ActionEditor.Domain;
using UnityEngine;

namespace NBC.ActionEditor
{
    public static class ClipTimingRules
    {
        /// <summary>Existing property editors allow half-length cross blends; edge drags do not.</summary>
        public static Vector2 NeighborBounds(TimelineClip clip, IDirectable previous, IDirectable next, bool allowCrossBlend)
        {
            var minimum = previous?.EndTime ?? clip.Parent?.StartTime ?? 0;
            var maximum = next?.StartTime ?? clip.Parent?.EndTime ?? float.PositiveInfinity;
            if (allowCrossBlend && clip.CanCrossBlend(previous))
                minimum -= Mathf.Min(clip.Length / 2, (previous.EndTime - previous.StartTime) / 2);
            if (allowCrossBlend && clip.CanCrossBlend(next))
                maximum += Mathf.Min(clip.Length / 2, (next.EndTime - next.StartTime) / 2);
            return new Vector2(minimum, maximum);
        }
    }
}
