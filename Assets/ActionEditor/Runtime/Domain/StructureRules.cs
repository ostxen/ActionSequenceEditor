using System;
using System.Linq;

namespace NBC.ActionEditor.Domain
{
    // 结构移动只改变所属列表，统一校验当前图、锁定、挂载与唯一性。
    public static class StructureRules
    {
        public static string Error(ActionDocument document, TimelineRootItem source, TimelineGroup target, int boundary)
        {
            if (document == null || source is not IDirectable item || !TryOwner(document, source, out var owner)) return "移动对象已失效。";
            if (item.IsLocked || owner?.IsLocked == true) return "锁定对象不能移动。";
            if (target != null && !document.RootItems.Contains(target)) return "目标分组已失效。";
            if (target?.IsLocked == true) return "目标分组已锁定。";
            if (source is TimelineGroup && target != null) return "分组只能在根级排序。";
            var count = target == null ? document.RootItems.Count : target.Tracks.Count;
            if (boundary < 0 || boundary > count) return "插入边界已失效。";
            var info = DirectableTypeInfo.For(source.GetType());
            if (source is TimelineTrack && owner != target && target != null
                && (info.AttachableTypes == null || !info.AttachableTypes.Contains(target.GetType()))) return "轨道不能挂载到目标分组。";
            var siblings = target == null ? document.RootItems.AsEnumerable() : target.Tracks.Cast<TimelineRootItem>();
            if (info.IsUnique && siblings.Any(other => !ReferenceEquals(other, source) && other.GetType() == source.GetType())) return "目标已存在唯一类型。";
            return null;
        }

        // 候选边界指向移除之前的列表，相邻两条边界都可能表示原位。
        public static bool IsNoOp(ActionDocument document, TimelineRootItem source, TimelineGroup target, int boundary)
        {
            if (!TryOwner(document, source, out var owner) || owner != target) return false;
            var index = owner == null ? document.RootItems.ToList().IndexOf(source) : owner.Tracks.IndexOf((TimelineTrack)source);
            return boundary == index || boundary == index + 1;
        }

        public static void Move(ActionDocument document, TimelineRootItem source, TimelineGroup target, int boundary)
        {
            var error = Error(document, source, target, boundary);
            if (error != null) throw new InvalidOperationException(error);
            if (IsNoOp(document, source, target, boundary)) return;
            TryOwner(document, source, out var owner);
            var index = owner == null ? document.RootItems.ToList().IndexOf(source) : owner.Tracks.IndexOf((TimelineTrack)source);
            if (owner == target && index < boundary) boundary--;
            // 整组仅移动根项，子轨及片段对象和字段保持原样。
            if (owner == null) document.RemoveRoot(source); else owner.Tracks.Remove((TimelineTrack)source);
            if (target == null) document.InsertRoot(boundary, source); else target.Tracks.Insert(boundary, (TimelineTrack)source);
        }

        private static bool TryOwner(ActionDocument document, TimelineRootItem source, out TimelineGroup owner)
        {
            owner = null;
            if (document == null || source == null) return false;
            if (document.RootItems.Contains(source)) return true;
            if (source is not TimelineTrack track) return false;
            owner = document.RootItems.OfType<TimelineGroup>().FirstOrDefault(group => group.Tracks.Contains(track));
            return owner != null;
        }
    }
}
