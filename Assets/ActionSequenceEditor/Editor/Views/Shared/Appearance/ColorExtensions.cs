using UnityEngine;

namespace ActionSequenceEditor
{
    // 生成灰度或透明度变体，不修改共享绘制状态。
    public static class ColorExtensions
    {
        public static Color Grey(float value) {
            return new Color(value, value, value);
        }

        public static Color WithAlpha(this Color color, float alpha) {
            color.a = alpha;
            return color;
        }
    }
}