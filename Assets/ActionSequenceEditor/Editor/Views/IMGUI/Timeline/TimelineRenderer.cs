using System;
using ActionSequenceEditor.Presentation.Timeline;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI
{
    // IMGUI只消费共享值快照，主体绘制不建立静态行缓存。
    internal static class TimelineRenderer
    {
        public static Rect Rect(TimelineRect value) => new Rect(value.X, value.Y, value.Width, value.Height);
        public static float X(TimelineRenderState state, float time) => state.ContentLeft + (time - state.Minimum) * Scale(state);
        public static float Scale(TimelineRenderState state) => (state.Size.WindowWidth - state.ContentLeft) / (state.Maximum - state.Minimum);
        public static void Body(TimelineRenderState state)
        {
            EditorGUI.DrawRect(new Rect(0, 0, state.Size.Width, state.Size.Height), new Color(.21f, .21f, .21f));
            foreach (var row in state.Rows)
            {
                var rect = Rect(row.Bounds); if (rect.yMax < 0 || rect.y > state.Size.Height) continue;
                rect.height -= 2; EditorGUI.DrawRect(rect, row.Selected ? new Color(.2f, .32f, .48f) : new Color(.18f, .18f, .18f));
                if (!row.IsGroup) EditorGUI.DrawRect(new Rect(22, rect.y + 3, 4, 20), row.Color);
            }
            foreach (var clip in state.Clips)
            {
                var rect = Rect(clip.Bounds); if (rect.yMax < 0 || rect.y > state.Size.Height) continue;
                var visible = rect; visible.xMin = Mathf.Max(state.ContentLeft, rect.xMin); visible.xMax = Mathf.Min(state.Size.Width, rect.xMax);
                if (visible.width <= 0) continue;
                if (clip.Point)
                { DrawTexture(new Rect(rect.center.x - Styles.SignalIcon.width * .5f, rect.y - 3, Styles.SignalIcon.width, Styles.SignalIcon.height), Styles.SignalIcon, clip.Selected ? new Color(.6f, .95f, 1) : Styles.ClipBackColor); continue; }
                EditorGUI.DrawRect(visible, Styles.ClipBackColor);
                EditorGUI.DrawRect(new Rect(visible.x, visible.yMax - 4, visible.width, 4), clip.Color);
                Blend(rect.xMin, clip.BlendInX, rect, false, state.ContentLeft, state.Size.Width);
                if (clip.DrawBlendOut) Blend(clip.BlendOutX, rect.xMax, rect, true, state.ContentLeft, state.Size.Width);
                if (clip.OverlapBefore) EditorGUI.DrawRect(new Rect(clip.BlendInX, rect.y, 1, rect.height), new Color(0, 0, 0, .4f));
                if (clip.Selected)
                {
                    EditorGUI.DrawRect(new Rect(visible.x, rect.y, visible.width, 2), Styles.ClipSelectColor);
                    EditorGUI.DrawRect(new Rect(visible.x, rect.yMax - 2, visible.width, 2), Styles.ClipSelectColor);
                    if (rect.xMin >= state.ContentLeft) EditorGUI.DrawRect(new Rect(rect.xMin, rect.y, 2, rect.height), Styles.ClipSelectColor);
                    if (rect.xMax <= state.Size.Width) EditorGUI.DrawRect(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Styles.ClipSelectColor);
                }
                var label = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                if (rect.width > label.CalcSize(new GUIContent(clip.Title)).x) GUI.Label(rect, clip.Title, label);
                if (!clip.Valid) EditorGUI.DrawRect(visible, new Color(1, 0, 0, .2f));
            }
            foreach (var row in state.Rows)
            {
                if (row.Bounds.Bottom < 0 || row.Bounds.Y > state.Size.Height) continue;
                var x = row.IsGroup ? 20 : 48;
                var style = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
                style.normal.textColor = row.IsGroup ? new Color(1, 1, 1, .5f) : Color.white;
                GUI.Label(new Rect(x, row.Bounds.Y + 4, state.LeftWidth - x - 30, 20), row.Title, style);
                if (row.Locked || !row.Active)
                {
                    var stamp = !row.Active && row.Locked ? "禁用 & 锁定" : row.Locked ? "锁定" : "禁用";
                    EditorGUI.DrawRect(new Rect(0, row.Bounds.Y, state.Size.Width, 26), new Color(0, 0, 0, row.Active ? .15f : .2f));
                    GUI.Label(new Rect(state.ContentLeft, row.Bounds.Y, state.Size.Width - state.ContentLeft, 26), stamp,
                        new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter });
                }
            }
            EditorGUI.DrawRect(new Rect(state.ContentLeft - 7, 0, 1, state.Size.Height), Color.gray);
            void Cursor(float time, Color color)
            { var x = X(state, time); if (x >= state.ContentLeft) EditorGUI.DrawRect(new Rect(x, 0, 1, state.Size.Height), color); }
            Cursor(state.Duration, Styles.EndPointerColor);
            if (state.Playing) Cursor(state.Playhead, Color.white);
            if (state.RangeEnabled) { Cursor(state.RangeStart, Color.white); Cursor(state.RangeEnd, Color.white); }
            if (state.Marquee.HasValue) EditorGUI.DrawRect(Rect(state.Marquee.Value), new Color(.3f, .6f, 1, .25f));
        }
        public static void Insertion(InsertionIndicator line, float bodyTop)
        {
            if (line.Visible) EditorGUI.DrawRect(new Rect(0, bodyTop + line.Y, line.Width, 2), new Color(line.Red, line.Green, line.Blue));
        }
        private static readonly float[] Steps = { .01f, .1f, .5f, 1, 5, 10, 50, 100, 500, 1000, 5000, 10000, 50000, 100000, 250000, 500000 };
        public static void Ruler(TimelineRenderState state)
        {
            var scale = Scale(state); var index = Array.FindIndex(Steps, value => value * scale > 50);
            var step = index < 0 ? 1000000 : Steps[index]; var minor = index > 0 ? Steps[index - 1] : .01f;
            var left = state.ContentLeft;
            EditorGUI.DrawRect(new Rect(left, 36, Mathf.Clamp(X(state, state.Duration) - left, 0, state.Size.Width - left), 4), Styles.TimeStepRectColor);
            if (minor * scale > 6) for (var time = Mathf.Ceil(state.Minimum / minor) * minor; time <= state.Maximum; time += minor)
            { var x = X(state, time); if (x >= left) EditorGUI.DrawRect(new Rect(x, 36, 1, 4), Color.white); }
            for (var time = Mathf.Ceil(state.Minimum / step) * step; time <= state.Maximum; time += step)
            {
                var x = X(state, time); if (x < left) continue;
                GUI.Label(new Rect(x, 20, 70, 18), (Mathf.Round(time * 10) / 10).ToString("0.00"));
                EditorGUI.DrawRect(new Rect(x, 28, 1, 12), Color.white);
            }
            EditorGUI.DrawRect(new Rect(X(state, state.Duration) - 2, 20, 5, 20), Styles.EndPointerColor);
            if (state.Playing) DrawTexture(new Rect(X(state, state.Playhead) - 5, 20, 11, 20), Styles.TimelineTimeCursorIcon, Color.white);
            if (state.RangeEnabled)
            {
                DrawTexture(new Rect(X(state, state.RangeStart) - Styles.TimelineStartPlaybackIcon.width, 20, Styles.TimelineStartPlaybackIcon.width, Styles.TimelineStartPlaybackIcon.height), Styles.TimelineStartPlaybackIcon, Color.white);
                DrawTexture(new Rect(X(state, state.RangeEnd), 20, Styles.TimelineEndPlaybackIcon.width, Styles.TimelineEndPlaybackIcon.height), Styles.TimelineEndPlaybackIcon, Color.white);
            }
            foreach (var guide in state.Guides)
            {
                if (!guide.Visible) continue; var x = X(state, guide.Time);
                if (guide.Solid) EditorGUI.DrawRect(new Rect(x, 20, 1, state.Size.Height + 20), Color.white);
                else for (var y = 20f; y < state.Size.Height + 40; y += 10) EditorGUI.DrawRect(new Rect(x, y, 1, 5), new Color(.48f, .48f, .48f));
                var text = guide.Time.ToString("0.00"); var width = EditorStyles.label.CalcSize(new GUIContent(text)).x + 5;
                EditorGUI.DrawRect(new Rect(x - width * .5f, 20, width, 18), new Color(0, 0, 0, .8f));
                GUI.Label(new Rect(x - width * .5f + 2, 20, width, 18), text);
            }
        }
        private static void DrawTexture(Rect rect, Texture texture, Color color)
        { var previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, texture); GUI.color = previous; }
        private static void Blend(float start, float end, Rect rect, bool outgoing, float minimum, float maximum)
        {
            if (end <= start) return;
            var left = Mathf.Max(minimum, start); var right = Mathf.Min(maximum, end); if (right <= left) return;
            var ya = Mathf.Lerp(outgoing ? rect.yMin : rect.yMax, outgoing ? rect.yMax : rect.yMin, Mathf.InverseLerp(start, end, left));
            var yb = Mathf.Lerp(outgoing ? rect.yMin : rect.yMax, outgoing ? rect.yMax : rect.yMin, Mathf.InverseLerp(start, end, right));
            var previous = Handles.color; Handles.color = Styles.ClipBlendColor;
            Handles.DrawAAConvexPolygon(new Vector3(left, rect.yMin), new Vector3(right, rect.yMin), new Vector3(right, yb), new Vector3(left, ya));
            Handles.color = new Color(0, 0, 0, .5f); Handles.DrawLine(new Vector3(left, ya), new Vector3(right, yb)); Handles.color = previous;
        }
    }
}
