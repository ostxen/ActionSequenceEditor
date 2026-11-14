using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI.Controls
{
    // 计算原生控件在行内的垂直对齐位置。
    public static class RectExtensions
    {
        public static float GetY(this Rect rect, float height, float lineHeight = Styles.LineHeight)
        {
            return rect.y + (lineHeight - height) * 0.5f;
        }
    }
}