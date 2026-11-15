using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor.Domain
{
    // 创建、粘贴及移动共用类型挂载和唯一性规则，不依赖事务或窗口。
    public static class StructureRules
    {
        public static bool AllowsParent(Type child, Type parent)
        {
            if (child == null || parent == null) return false;
            var parents = DirectableTypeInfo.For(child).AttachableTypes;
            return parents == null || parents.Contains(parent);
        }
        public static bool HasUniqueConflict(Type type, IEnumerable<object> siblings, object excluded = null)
            => DirectableTypeInfo.For(type).IsUnique
                && siblings.Any(other => other != null && !ReferenceEquals(other, excluded) && other.GetType() == type);

        public static string AttachmentError(object parent, Type type, object excluded = null)
        {
            if (type == null || type.IsAbstract || type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) == null)
                return "没有可创建的具体类型。";
            IEnumerable<object> siblings;
            if (parent is ActionDocument document)
            {
                if (!typeof(TimelineRootItem).IsAssignableFrom(type)) return "根项只能是分组或轨道。";
                // 根轨不受分组挂载声明限制；分组仍须适用于文档类型。
                if (typeof(TimelineGroup).IsAssignableFrom(type) && !AllowsParent(type, document.GetType())) return "分组类型不适用于当前文档。";
                siblings = document.RootItems;
            }
            else if (parent is TimelineGroup group)
            {
                if (!typeof(TimelineTrack).IsAssignableFrom(type) || !AllowsParent(type, group.GetType())) return "轨道不能挂载到目标分组。";
                siblings = group.Tracks;
            }
            else if (parent is TimelineTrack track)
            {
                if (!typeof(TimelineClip).IsAssignableFrom(type) || !AllowsParent(type, track.GetType())) return "片段类型不适用于当前轨道。";
                var category = track.Clips.FirstOrDefault(clip => !ReferenceEquals(clip, excluded))?.GetType();
                if (category != null && DirectableTypeInfo.For(category).Category is string expected
                    && expected != DirectableTypeInfo.For(type).Category) return "片段分类不适用于当前轨道。";
                siblings = track.Clips;
            }
            else return "没有有效挂载目标。";
            return HasUniqueConflict(type, siblings, excluded) ? "目标已存在唯一类型。" : null;
        }

        public static bool Contains(ActionDocument document, IDirectable item)
            => document != null && item != null && document.directables.Contains(item);
        public static string DeletionError(ActionDocument document, IEnumerable<IDirectable> items)
        {
            if (items == null) return "没有选择对象。";
            var targets = items.ToArray();
            if (targets.Length == 0) return "没有选择对象。";
            return targets.Any(item => !Contains(document, item) || item.IsLocked)
                ? "选择中有失效或锁定对象，未删除任何内容。" : null;
        }

        public static string Error(ActionDocument document, TimelineRootItem source, TimelineGroup target, int boundary)
        {
            if (document == null || source is not IDirectable item || !TryOwner(document, source, out var owner)) return "移动对象已失效。";
            if (item.IsLocked || owner?.IsLocked == true) return "锁定对象不能移动。";
            if (target != null && !document.RootItems.Contains(target)) return "目标分组已失效。";
            if (target?.IsLocked == true) return "目标分组已锁定。";
            if (source is TimelineGroup && target != null) return "分组只能在根级排序。";
            var count = target == null ? document.RootItems.Count : target.Tracks.Count;
            if (boundary < 0 || boundary > count) return "插入边界已失效。";
            return AttachmentError((object)target ?? document, source.GetType(), source);
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
