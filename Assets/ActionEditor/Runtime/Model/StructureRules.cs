using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor.Model
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
            if (parent is ActionSequence sequence)
            {
                if (!typeof(ActionContainer).IsAssignableFrom(type)) return "根项只能是分组或轨道。";
                // 根轨不受分组挂载声明限制；分组仍须适用于文档类型。
                if (typeof(ActionGroup).IsAssignableFrom(type) && !AllowsParent(type, sequence.GetType())) return "分组类型不适用于当前文档。";
                siblings = sequence.RootContainers;
            }
            else if (parent is ActionGroup group)
            {
                if (!typeof(ActionTrack).IsAssignableFrom(type) || !AllowsParent(type, group.GetType())) return "轨道不能挂载到目标分组。";
                siblings = group.Tracks;
            }
            else if (parent is ActionTrack track)
            {
                if (!typeof(ActionClip).IsAssignableFrom(type) || !AllowsParent(type, track.GetType())) return "片段类型不适用于当前轨道。";
                var category = track.Clips.FirstOrDefault(clip => !ReferenceEquals(clip, excluded))?.GetType();
                if (category != null && DirectableTypeInfo.For(category).Category is string expected
                    && expected != DirectableTypeInfo.For(type).Category) return "片段分类不适用于当前轨道。";
                siblings = track.Clips;
            }
            else return "没有有效挂载目标。";
            return HasUniqueConflict(type, siblings, excluded) ? "目标已存在唯一类型。" : null;
        }

        public static bool Contains(ActionSequence sequence, IDirectable item)
            => sequence != null && item != null && sequence.directables.Contains(item);
        public static string DeletionError(ActionSequence sequence, IEnumerable<IDirectable> items)
        {
            if (items == null) return "没有选择对象。";
            var targets = items.ToArray();
            if (targets.Length == 0) return "没有选择对象。";
            return targets.Any(item => !Contains(sequence, item) || item.IsLocked)
                ? "选择中有失效或锁定对象，未删除任何内容。" : null;
        }

        public static string MoveError(ActionSequence sequence, ActionContainer source, ActionGroup targetGroup, int insertionBoundary)
        {
            if (sequence == null || source is not IDirectable item || !TryGetOwningGroup(sequence, source, out var owner)) return "移动对象已失效。";
            if (item.IsLocked || owner?.IsLocked == true) return "锁定对象不能移动。";
            if (targetGroup != null && !sequence.RootContainers.Contains(targetGroup)) return "目标分组已失效。";
            if (targetGroup?.IsLocked == true) return "目标分组已锁定。";
            if (source is ActionGroup && targetGroup != null) return "分组只能在根级排序。";
            var count = targetGroup == null ? sequence.RootContainers.Count : targetGroup.Tracks.Count;
            if (insertionBoundary < 0 || insertionBoundary > count) return "插入边界已失效。";
            return AttachmentError((object)targetGroup ?? sequence, source.GetType(), source);
        }

        // 候选边界指向移除之前的列表，相邻两条边界都可能表示原位。
        public static bool IsMoveNoOp(ActionSequence sequence, ActionContainer source, ActionGroup targetGroup, int insertionBoundary)
        {
            if (!TryGetOwningGroup(sequence, source, out var owner) || owner != targetGroup) return false;
            var index = owner == null ? sequence.RootContainers.ToList().IndexOf(source) : owner.Tracks.IndexOf((ActionTrack)source);
            return insertionBoundary == index || insertionBoundary == index + 1;
        }

        public static void Move(ActionSequence sequence, ActionContainer source, ActionGroup targetGroup, int insertionBoundary)
        {
            var error = MoveError(sequence, source, targetGroup, insertionBoundary);
            if (error != null) throw new InvalidOperationException(error);
            if (IsMoveNoOp(sequence, source, targetGroup, insertionBoundary)) return;
            TryGetOwningGroup(sequence, source, out var owner);
            var index = owner == null ? sequence.RootContainers.ToList().IndexOf(source) : owner.Tracks.IndexOf((ActionTrack)source);
            if (owner == targetGroup && index < insertionBoundary) insertionBoundary--;
            // 整组仅移动根项，子轨及片段对象和字段保持原样。
            if (owner == null) sequence.RemoveRootContainer(source); else owner.Tracks.Remove((ActionTrack)source);
            if (targetGroup == null) sequence.InsertRootContainer(insertionBoundary, source); else targetGroup.Tracks.Insert(insertionBoundary, (ActionTrack)source);
        }

        private static bool TryGetOwningGroup(ActionSequence sequence, ActionContainer source, out ActionGroup owner)
        {
            owner = null;
            if (sequence == null || source == null) return false;
            if (sequence.RootContainers.Contains(source)) return true;
            if (source is not ActionTrack track) return false;
            owner = sequence.RootContainers.OfType<ActionGroup>().FirstOrDefault(group => group.Tracks.Contains(track));
            return owner != null;
        }
    }
}
