using System;
using NBC.ActionEditor.Domain;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 片段时间和混合边界的领域规则，供属性编辑与交互共用。
    public static class ClipTimingRules
    {
        public const float MinimumLength = .01f;
        public static void RequireTiming(TimelineClip clip, float start, float length)
        {
            if (!Finite(start) || !Finite(length) || !Finite(start + length) || start < 0 || length < 0)
                throw new ArgumentOutOfRangeException(nameof(start), "片段时间必须为有限非负值。");
            if (clip.CanScale() && length < MinimumLength)
                throw new ArgumentOutOfRangeException(nameof(length), "片段最短为0.01秒。");
        }
        public static void ApplyTiming(TimelineClip clip, float start, float length)
        {
            RequireTiming(clip, start, length);
            clip.StartTime = start;
            if (clip.CanScale())
            {
                var previousLength = clip.Length;
                clip.Length = length;
                if (clip.Length < previousLength) clip.ConstrainAuthoredBlends();
            }
        }
        // 属性输入先吸附再求可行区间；最短长度不能突破相邻片段边界。
        public static void ApplyPropertyTiming(TimelineClip clip, float start, float end, float step)
        {
            if (!Finite(start) || !Finite(end) || !Finite(step) || step <= 0) throw new ArgumentOutOfRangeException(nameof(start));
            var next = clip.GetNextSibling();
            var bounds = NeighborBounds(clip, clip.GetPreviousSibling(), next, true);
            var minimum = Math.Max(0, bounds.x);
            var maximum = next == null ? float.PositiveInfinity : bounds.y;
            var length = clip.CanScale() ? MinimumLength : 0;
            if (maximum - minimum < length) throw new InvalidOperationException("相邻片段之间没有满足最短长度的空间。");
            start = (float)(Math.Round((double)start / step, MidpointRounding.ToEven) * step);
            end = (float)(Math.Round((double)end / step, MidpointRounding.ToEven) * step);
            start = Math.Max(minimum, Math.Min(Math.Min(start, end), maximum - length));
            end = Math.Min(maximum, Math.Max(start + length, end));
            if (!Finite(start) || !Finite(end)) throw new ArgumentOutOfRangeException(nameof(start));
            // 点事件只移动起点，不能通过EndTime改写其保留的原始长度字段。
            ApplyTiming(clip, start, clip.CanScale() ? Math.Max(MinimumLength, end - start) : 0);
        }

        // 混合参数不能侵占另一个方向已使用的片段时长。
        public static void ApplyPropertyBlend(TimelineClip clip, bool incoming, float value)
        {
            if (!Finite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (incoming ? !clip.CanBlendIn() : !clip.CanBlendOut()) throw new InvalidOperationException("片段不支持此混合方向。");
            value = Math.Max(0, Math.Min(value, clip.Length - (incoming ? clip.BlendOut : clip.BlendIn)));
            if (incoming) clip.BlendIn = value; else clip.BlendOut = value;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // 属性允许相邻片段半长交叠；边缘拖动使用严格边界。
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
