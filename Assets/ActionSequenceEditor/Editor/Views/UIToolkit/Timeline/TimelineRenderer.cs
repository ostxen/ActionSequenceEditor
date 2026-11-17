using System;
using ActionSequenceEditor.Presentation.Timeline;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Views.UIToolkit
{
    // 原生绘制器只消费值快照，所有时间和混合边界已由共享表现层计算。
    internal static class TimelineRenderer
    {
        public static Rect Rect(TimelineRect value) => new Rect(value.X, value.Y, value.Width, value.Height);
        public static float X(TimelineRenderState state, float time) => state.ContentLeft + (time - state.Minimum) * Scale(state);
        public static float Scale(TimelineRenderState state) => (state.Size.WindowWidth - state.ContentLeft) / (state.Maximum - state.Minimum);
        public static void Body(MeshGenerationContext context, TimelineRenderState state, Rect bounds)
        {
            var painter = context.painter2D; Fill(painter, bounds, new Color(.21f, .21f, .21f));
            if (state == null || !state.HasSequence) return;
            foreach (var row in state.Rows)
            {
                var y = row.Bounds.Y;
                if (row.Bounds.Bottom < 0 || y > bounds.height) continue;
                Fill(painter, new Rect(0, y, bounds.width, 26), row.Selected ? new Color(.2f, .32f, .48f) : new Color(.18f, .18f, .18f));
                if (!row.IsGroup) Fill(painter, new Rect(22, y + 3, 4, 20), row.Color);
            }
            foreach (var clip in state.Clips)
            {
                var rect = Rect(clip.Bounds); if (rect.yMax < 0 || rect.yMin > bounds.height) continue;
                var visible = rect; visible.xMin = Mathf.Max(state.ContentLeft, rect.xMin); visible.xMax = Mathf.Min(bounds.width, rect.xMax);
                if (visible.width <= 0) continue;
                if (clip.Point)
                { Texture(context, new Rect(rect.center.x - Styles.SignalIcon.width * .5f, rect.y - 3, Styles.SignalIcon.width, Styles.SignalIcon.height), Styles.SignalIcon, clip.Selected ? new Color(.6f, .95f, 1) : Styles.ClipBackColor); continue; }
                Fill(painter, visible, Styles.ClipBackColor);
                Fill(painter, new Rect(visible.x, visible.yMax - 4, visible.width, 4), clip.Color);
                Blend(painter, rect.xMin, clip.BlendInX, rect, false, state.ContentLeft, bounds.width);
                if (clip.DrawBlendOut) Blend(painter, clip.BlendOutX, rect.xMax, rect, true, state.ContentLeft, bounds.width);
                if (clip.OverlapBefore) Line(painter, new Vector2(clip.BlendInX, rect.y), new Vector2(clip.BlendInX, rect.yMax), new Color(0, 0, 0, .4f));
                if (clip.Selected)
                {
                    Fill(painter, new Rect(visible.x, rect.y, visible.width, 2), Styles.ClipSelectColor);
                    Fill(painter, new Rect(visible.x, rect.yMax - 2, visible.width, 2), Styles.ClipSelectColor);
                    if (rect.xMin >= state.ContentLeft) Fill(painter, new Rect(rect.xMin, rect.y, 2, rect.height), Styles.ClipSelectColor);
                    if (rect.xMax <= bounds.width) Fill(painter, new Rect(rect.xMax - 2, rect.y, 2, rect.height), Styles.ClipSelectColor);
                }
                if (!clip.Valid) Fill(painter, visible, new Color(1, 0, 0, .2f));
            }
            foreach (var row in state.Rows)
            {
                var y = row.Bounds.Y;
                if (row.Bounds.Bottom < 0 || y > bounds.height) continue;
                Text(context, row.Title, row.IsGroup ? 20 : 48, y + 5, state.LeftWidth - (row.IsGroup ? 50 : 78), row.IsGroup ? new Color(1, 1, 1, .5f) : Color.white);
                if (!row.Locked && row.Active) continue;
                Fill(painter, new Rect(0, y, bounds.width, 26), new Color(0, 0, 0, row.Active ? .15f : .2f));
                for (var x = 0f; x < bounds.width; x += row.Active ? 20 : 5)
                    Line(painter, new Vector2(x, y + 26), new Vector2(x + 26, y), new Color(0, 0, 0, .08f));
            }
            Line(painter, new Vector2(state.ContentLeft - 7, 0), new Vector2(state.ContentLeft - 7, bounds.height), Color.gray);
            void Cursor(float time, Color color)
            { var x = X(state, time); if (x >= state.ContentLeft) Line(painter, new Vector2(x, 0), new Vector2(x, bounds.height), color); }
            if (state.Playing) Cursor(state.Playhead, Color.white);
            Cursor(state.Duration, Styles.EndPointerColor);
            if (state.RangeEnabled) { Cursor(state.RangeStart, Color.white); Cursor(state.RangeEnd, Color.white); }
            if (state.Marquee.HasValue) Fill(painter, Rect(state.Marquee.Value), new Color(.3f, .6f, 1, .25f));
        }
        // overlay最后绘制，避免被子行、标题控件或内容裁剪截短。
        public static void Insertion(MeshGenerationContext context, InsertionIndicator line)
        {
            if (line.Visible) Line(context.painter2D, new Vector2(0, line.Y), new Vector2(line.Width, line.Y), new Color(line.Red, line.Green, line.Blue), 2);
        }
        private static readonly float[] Steps = { .01f, .1f, .5f, 1, 5, 10, 50, 100, 500, 1000, 5000, 10000, 50000, 100000, 250000, 500000 };
        public static void Ruler(MeshGenerationContext context, TimelineRenderState state, Rect bounds)
        {
            var painter = context.painter2D; Fill(painter, bounds, new Color(.22f, .22f, .22f));
            if (state == null || !state.HasSequence) return;
            var scale = Scale(state); var index = Array.FindIndex(Steps, step => step * scale > 50);
            var step = index < 0 ? 1000000 : Steps[index]; var minor = index > 0 ? Steps[index - 1] : .01f;
            float At(float time) => X(state, time) - state.ContentLeft;
            Fill(painter, new Rect(0, 16, Mathf.Clamp(At(state.Duration), 0, bounds.width), 4), Styles.TimeStepRectColor);
            if (minor * scale > 6) for (var time = Mathf.Ceil(state.Minimum / minor) * minor; time <= state.Maximum; time += minor)
                Line(painter, new Vector2(At(time), 16), new Vector2(At(time), 20), Color.white);
            for (var time = Mathf.Ceil(state.Minimum / step) * step; time <= state.Maximum; time += step)
            {
                var x = At(time); if (x < 0) continue;
                context.DrawText((Mathf.Round(time * 10) / 10).ToString("0.00"), new Vector2(x, 0), 12, Color.white);
                Line(painter, new Vector2(x, 8), new Vector2(x, 20), Color.white);
            }
            Fill(painter, new Rect(At(state.Duration) - 2, 0, 5, 20), Styles.EndPointerColor);
            if (state.Playing) Texture(context, new Rect(At(state.Playhead) - 5, 0, 11, 20), Styles.TimelineTimeCursorIcon, Color.white);
            if (!state.RangeEnabled) return;
            Texture(context, new Rect(At(state.RangeStart) - Styles.TimelineStartPlaybackIcon.width, 0, Styles.TimelineStartPlaybackIcon.width, Styles.TimelineStartPlaybackIcon.height), Styles.TimelineStartPlaybackIcon, Color.white);
            Texture(context, new Rect(At(state.RangeEnd), 0, Styles.TimelineEndPlaybackIcon.width, Styles.TimelineEndPlaybackIcon.height), Styles.TimelineEndPlaybackIcon, Color.white);
        }
        private static void Text(MeshGenerationContext context, string text, float x, float y, float width, Color color)
        {
            text = text ?? ""; var count = Mathf.Max(1, Mathf.FloorToInt(width / 8));
            if (text.Length > count) text = text.Substring(0, count) + "…";
            context.DrawText(text, new Vector2(x, y), 12, color);
        }
        private static void Blend(Painter2D painter, float start, float end, Rect rect, bool outgoing, float minimum, float maximum)
        {
            if (end <= start) return;
            var left = Mathf.Max(minimum, start); var right = Mathf.Min(maximum, end); if (right <= left) return;
            var a = Mathf.InverseLerp(start, end, left); var b = Mathf.InverseLerp(start, end, right);
            var ya = Mathf.Lerp(outgoing ? rect.yMin : rect.yMax, outgoing ? rect.yMax : rect.yMin, a);
            var yb = Mathf.Lerp(outgoing ? rect.yMin : rect.yMax, outgoing ? rect.yMax : rect.yMin, b);
            painter.fillColor = Styles.ClipBlendColor; painter.BeginPath(); painter.MoveTo(new Vector2(left, rect.yMin));
            painter.LineTo(new Vector2(right, rect.yMin)); painter.LineTo(new Vector2(right, yb)); painter.LineTo(new Vector2(left, ya)); painter.ClosePath(); painter.Fill();
            Line(painter, new Vector2(left, ya), new Vector2(right, yb), new Color(0, 0, 0, .5f));
        }
        public static void Texture(MeshGenerationContext context, Rect rect, Texture texture, Color tint)
        {
            if (texture == null || rect.width <= 0 || rect.height <= 0) return;
            var mesh = context.Allocate(4, 6, texture); var uv = mesh.uvRegion;
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMin, uv.yMax) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMax, uv.yMax) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMax, uv.yMin) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMin, uv.yMin) });
            foreach (ushort index in new ushort[] { 0, 1, 2, 2, 3, 0 }) mesh.SetNextIndex(index);
        }
        public static void Fill(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color; painter.BeginPath(); painter.MoveTo(new Vector2(rect.xMin, rect.yMin)); painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax)); painter.LineTo(new Vector2(rect.xMin, rect.yMax)); painter.ClosePath(); painter.Fill();
        }
        public static void Line(Painter2D painter, Vector2 start, Vector2 end, Color color, float width = 1)
        { painter.strokeColor = color; painter.lineWidth = width; painter.BeginPath(); painter.MoveTo(start); painter.LineTo(end); painter.Stroke(); }
    }
}
