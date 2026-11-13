using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 保存单个窗口的视域、滚动和折叠，不参与作者快照。
    public sealed class TimelineWorkspace
    {
        private ActionDocument document;
        private readonly HashSet<TimelineGroup> collapsed = new HashSet<TimelineGroup>();
        private float minimum;
        private float maximum = 7;
        public float ScrollY { get; set; }
        public float LeftWidth { get; set; } = 240;
        public float ViewTimeMin
        {
            get => minimum;
            set { RequireFinite(value); minimum = Math.Min(value, maximum - .25f); }
        }
        public float ViewTimeMax
        {
            get => maximum;
            set { RequireFinite(value); maximum = Math.Max(value, minimum + .25f); }
        }
        public float ViewTime => maximum - minimum;

        // 图替换时释放旧引用，不把旧位置上的折叠误套给另一个分组。
        public void Bind(ActionDocument next)
        {
            if (ReferenceEquals(document, next))
            {
                collapsed.RemoveWhere(group => next == null || !next.RootItems.Contains(group));
                return;
            }
            collapsed.Clear();
            document = next;
            if (next == null) ScrollY = 0;
        }

        public bool IsCollapsed(IDirectable item) => item is TimelineGroup group ? collapsed.Contains(group)
            : item?.Parent is TimelineGroup parent && collapsed.Contains(parent);

        public void SetCollapsed(TimelineGroup group, bool value)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (value) collapsed.Add(group); else collapsed.Remove(group);
        }

        // 先直接设置完整区间，避免两个独立钳制访问器截断平移。
        public void SetRange(float min, float max)
        {
            RequireFinite(min); RequireFinite(max);
            var span = max - min;
            RequireFinite(span);
            if (span < .25f) throw new ArgumentOutOfRangeException(nameof(max));
            minimum = min; maximum = max;
        }

        private static void RequireFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
