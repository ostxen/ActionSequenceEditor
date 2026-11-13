using System;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 缩放锚点和过零平移只改变当前窗口视域。
    public static class ViewportNavigation
    {
        public static void Zoom(TimelineWorkspace workspace, TimelineSize size, float x, float wheel, bool enabled)
        {
            if (!enabled || wheel == 0 || wheel > 0 && workspace.ViewTime > 240) return;
            var span = workspace.ViewTime;
            var next = Math.Max(.25f, span + Math.Sign(wheel) * 225 / Math.Max(1, size.WindowWidth) * span);
            var left = workspace.LeftWidth + size.ContentInset;
            var fraction = Math.Max(0, Math.Min(1, (x - left) / Math.Max(1, size.WindowWidth - left)));
            var anchor = workspace.ViewTimeMin + span * fraction;
            var min = anchor - next * fraction;
            workspace.SetRange(min, min + next);
        }
        public static void Pan(TimelineWorkspace workspace, TimelineSize size, float pixels)
        {
            var span = workspace.ViewTime;
            var min = Math.Max(0, workspace.ViewTimeMin - pixels / Math.Max(1, size.WindowWidth - workspace.LeftWidth - size.ContentInset) * span);
            workspace.SetRange(min, min + span);
        }
    }
}
