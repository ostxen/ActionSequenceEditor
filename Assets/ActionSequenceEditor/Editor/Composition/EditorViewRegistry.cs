using System;
using System.Collections.Generic;
using System.Linq;

namespace ActionSequenceEditor
{
    // 注册逐窗绘制和更新回调，坏窗口不能阻断其他存活绑定。
    public sealed class EditorViewRegistry
    {
        private sealed class View
        {
            public Action Repaint, Changed, Tick;
        }
        private readonly Dictionary<object, View> views = new Dictionary<object, View>();
        public int Count => views.Count;
        public void Attach(object owner, Action repaint, Action changed)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            views[owner] = new View { Repaint = repaint, Changed = changed };
        }
        public void Detach(object owner)
        {
            if (owner == null) return;
            views.Remove(owner);
        }
        // 窗口更新由唯一宿主驱动，解除注册后快照内的旧回调也不能执行。
        public void SetTick(object owner, Action tick)
        {
            if (!views.TryGetValue(owner, out var view)) throw new InvalidOperationException("窗口尚未绑定。");
            view.Tick = tick;
        }
        public void Tick(Action<Exception> failed)
        {
            foreach (var entry in views.ToArray())
            {
                if (!views.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry.Value)) continue;
                try { current.Tick?.Invoke(); }
                catch (Exception error) { failed(error); }
            }
        }
        public void Repaint() => Notify(view => view.Repaint);
        public void NotifyChanged() => Notify(view => view.Changed);

        private void Notify(Func<View, Action> callback)
        {
            List<Exception> errors = null;
            foreach (var entry in views.ToArray())
            {
                // 回调可同步关闭其他窗口，旧快照不能重新调用已解除的注册。
                if (!views.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry.Value)) continue;
                try { callback(current)?.Invoke(); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            }
            if (errors != null) throw new AggregateException("窗口通知失败。", errors);
        }
    }
}
