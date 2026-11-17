using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Model;

namespace ActionSequenceEditor.Presentation.Timeline
{
    // 保存单个窗口的视域、滚动和折叠，不参与作者快照。
    public sealed class TimelineWorkspace
    {
        private ActionSequence sequence;
        private readonly HashSet<ActionGroup> collapsed = new HashSet<ActionGroup>();
        private float minimum;
        private float maximum = 7;
        private float scrollY, leftWidth = 240;
        public float ScrollY { get => scrollY; set { RequireFinite(value); scrollY = Math.Max(0, value); } }
        public float LeftWidth { get => leftWidth; set { RequireFinite(value); leftWidth = Math.Max(0, value); } }
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
        public void Bind(ActionSequence next)
        {
            if (ReferenceEquals(sequence, next))
            {
                collapsed.RemoveWhere(group => next == null || !next.RootContainers.Contains(group));
                return;
            }
            collapsed.Clear();
            sequence = next;
            if (next == null) ScrollY = 0;
        }

        public bool IsCollapsed(IDirectable item) => item is ActionGroup group ? collapsed.Contains(group)
            : item?.Parent is ActionGroup parent && collapsed.Contains(parent);

        public void SetCollapsed(ActionGroup group, bool value)
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
