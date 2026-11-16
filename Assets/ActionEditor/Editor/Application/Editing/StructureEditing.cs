using NBC.ActionEditor.Model;
using NBC.ActionEditor.Application.Sequences;
using System;
using System.Linq;

namespace NBC.ActionEditor.Application.Editing
{
    // 结构修改在共享会话中预检和提交，不访问窗口或全局状态。
    public sealed class StructureEditing
    {
        private readonly SequenceSession session;
        private readonly Func<IDirectable[]> selection;
        public StructureEditing(SequenceSession session, Func<IDirectable[]> selection)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); this.selection = selection ?? throw new ArgumentNullException(nameof(selection)); }
        public bool Contains(IDirectable item) => StructureRules.Contains(session.Sequence, item);
        public void Delete(IDirectable item) => DeleteOperation(item).Execute();
        private void DeleteCore(IDirectable item)
        {
            if (item == null || item.IsLocked) throw new InvalidOperationException("锁定对象不能删除。");
            if (item is ActionGroup group) session.Sequence.RemoveRootContainer(group);
            else if (item is ActionTrack track)
            { if (track.Group == null) session.Sequence.RemoveRootContainer(track); else track.Group.Tracks.Remove(track); }
            else if (item is ActionClip clip && clip.Parent is ActionTrack owner) owner.Clips.Remove(clip);
        }
        public EditOperation CreateOperation(object parent, Type type, float time = 0) => new EditOperation(session, "Create timeline item",
            () => CreationError(parent, type, time), () =>
            {
                if (parent is ActionSequence sequence) sequence.AddGroup(type);
                else if (parent is ActionGroup group) group.AddTrack(type);
                else if (parent is ActionTrack track) track.AddClip(type, time);
            });
        private string CreationError(object parent, Type type, float time)
        {
            if (session.Sequence == null) return "没有打开的文档。";
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) return "创建时间必须为有限非负值。";
            if (parent is ActionSequence sequence)
            {
                if (!ReferenceEquals(sequence, session.Sequence) || type == null || !typeof(ActionGroup).IsAssignableFrom(type))
                    return "目标文档或分组类型已失效。";
            }
            else if (parent is not IDirectable item || !Contains(item) || item.IsLocked) return "目标已失效或被锁定。";
            return StructureRules.AttachmentError(parent, type);
        }
        public ActionGroup AddGroup(Type type)
        {
            var sequence = session.Sequence; var command = CreateOperation(sequence, type);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var index = sequence.RootContainers.OfType<ActionGroup>().Count();
            command.Execute(); return sequence.RootContainers.OfType<ActionGroup>().ElementAt(index);
        }
        // 创建结果交给调用者决定选择和视域，作者用例不持有窗口状态。
        public IDirectable Create(object parent, Type type, float time = 0)
        {
            if (parent is ActionSequence) return AddGroup(type);
            if (parent is ActionGroup group) return AddTrack(group, type);
            if (parent is ActionTrack track) return AddClip(track, type, time);
            throw new InvalidOperationException("没有有效创建目标。");
        }
        public ActionTrack AddTrack(ActionGroup group, Type type)
        {
            var command = CreateOperation(group, type);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var index = group.Tracks.Count; command.Execute(); return group.Tracks[index];
        }
        public ActionClip AddClip(ActionTrack track, Type type, float time)
        {
            var command = CreateOperation(track, type, time);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var before = track.Clips.ToArray(); command.Execute(); return track.Clips.Except(before).Single();
        }
        public EditOperation DeleteOperation(params IDirectable[] items) => new EditOperation(session, "Delete timeline items",
            () => StructureRules.DeletionError(session.Sequence, items),
            () => { foreach (var item in items.Distinct()) DeleteCore(item); });
        public string MoveError(ActionContainer source, ActionGroup targetGroup, int insertionBoundary, long version)
        {
            if (session.Version != version) return "文档版本已改变，请重新拖动。";
            if (session.IsEditing) return "另一项编辑尚未完成。";
            return StructureRules.MoveError(session.Sequence, source, targetGroup, insertionBoundary);
        }
        public EditOperation MoveOperation(ActionContainer source, ActionGroup targetGroup, int insertionBoundary)
        {
            var version = session.Version;
            return new EditOperation(session, "Move timeline structure",
                () => session.Version != version ? "文档版本已改变。" : StructureRules.MoveError(session.Sequence, source, targetGroup, insertionBoundary),
                () => StructureRules.Move(session.Sequence, source, targetGroup, insertionBoundary));
        }
        // 释放前重新校验版本，原位不创建事务，也不触发图替换通知。
        public bool Move(ActionContainer source, ActionGroup targetGroup, int insertionBoundary, long version)
        {
            var error = MoveError(source, targetGroup, insertionBoundary, version);
            if (error != null) throw new InvalidOperationException(error);
            if (StructureRules.IsMoveNoOp(session.Sequence, source, targetGroup, insertionBoundary)) return false;
            MoveOperation(source, targetGroup, insertionBoundary).Execute();
            return true;
        }
        public void DeleteSelection()
        {
            DeleteOperation(selection()).Execute();
        }

        public void MoveTrack(ActionTrack track, ActionGroup targetGroup, int insertionBoundary)
        {
            var operation = MoveOperation(track, targetGroup, insertionBoundary);
            if (!operation.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            if (StructureRules.IsMoveNoOp(session.Sequence, track, targetGroup, insertionBoundary)) return;
            operation.Execute();
        }
    }

}
