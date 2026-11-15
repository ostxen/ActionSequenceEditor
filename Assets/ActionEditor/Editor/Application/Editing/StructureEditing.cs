using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Application.Documents;
using System;
using System.Linq;

namespace NBC.ActionEditor.Application.Editing
{
    // 结构修改在共享会话中预检和提交，不访问窗口或全局状态。
    public sealed class StructureEditing
    {
        private readonly DocumentSession session;
        private readonly Func<IDirectable[]> selection;
        public StructureEditing(DocumentSession session, Func<IDirectable[]> selection)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); this.selection = selection ?? throw new ArgumentNullException(nameof(selection)); }
        public bool Contains(IDirectable item) => StructureRules.Contains(session.Document, item);
        public void Delete(IDirectable item) => Deletion(item).Execute();
        private void DeleteCore(IDirectable item)
        {
            if (item == null || item.IsLocked) throw new InvalidOperationException("锁定对象不能删除。");
            if (item is TimelineGroup group) session.Document.RemoveRoot(group);
            else if (item is TimelineTrack track)
            { if (track.Group == null) session.Document.RemoveRoot(track); else track.Group.Tracks.Remove(track); }
            else if (item is TimelineClip clip && clip.Parent is TimelineTrack owner) owner.Clips.Remove(clip);
        }
        public EditOperation Creation(object parent, Type type, float time = 0) => new EditOperation(session, "Create timeline item",
            () => CreationError(parent, type, time), () =>
            {
                if (parent is ActionDocument asset) asset.AddGroup(type);
                else if (parent is TimelineGroup group) group.AddTrack(type);
                else if (parent is TimelineTrack track) track.AddClip(type, time);
            });
        private string CreationError(object parent, Type type, float time)
        {
            if (session.Document == null) return "没有打开的文档。";
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) return "创建时间必须为有限非负值。";
            if (parent is ActionDocument document)
            {
                if (!ReferenceEquals(document, session.Document) || type == null || !typeof(TimelineGroup).IsAssignableFrom(type))
                    return "目标文档或分组类型已失效。";
            }
            else if (parent is not IDirectable item || !Contains(item) || item.IsLocked) return "目标已失效或被锁定。";
            return StructureRules.AttachmentError(parent, type);
        }
        public TimelineGroup AddGroup(Type type)
        {
            var asset = session.Document; var command = Creation(asset, type);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var index = asset.RootItems.OfType<TimelineGroup>().Count();
            command.Execute(); return asset.RootItems.OfType<TimelineGroup>().ElementAt(index);
        }
        // 创建结果交给调用者决定选择和视域，作者用例不持有窗口状态。
        public IDirectable Create(object parent, Type type, float time = 0)
        {
            if (parent is ActionDocument) return AddGroup(type);
            if (parent is TimelineGroup group) return AddTrack(group, type);
            if (parent is TimelineTrack track) return AddClip(track, type, time);
            throw new InvalidOperationException("没有有效创建目标。");
        }
        public TimelineTrack AddTrack(TimelineGroup group, Type type)
        {
            var command = Creation(group, type);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var index = group.Tracks.Count; command.Execute(); return group.Tracks[index];
        }
        public TimelineClip AddClip(TimelineTrack track, Type type, float time)
        {
            var command = Creation(track, type, time);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var before = track.Clips.ToArray(); command.Execute(); return track.Clips.Except(before).Single();
        }
        public EditOperation Deletion(params IDirectable[] items) => new EditOperation(session, "Delete timeline items",
            () => StructureRules.DeletionError(session.Document, items),
            () => { foreach (var item in items.Distinct()) DeleteCore(item); });
        public string MoveError(TimelineRootItem source, TimelineGroup target, int boundary, long version)
        {
            if (session.Version != version) return "文档版本已改变，请重新拖动。";
            if (session.IsEditing) return "另一项编辑尚未完成。";
            return StructureRules.Error(session.Document, source, target, boundary);
        }
        public EditOperation Reorder(TimelineRootItem source, TimelineGroup target, int index)
        {
            var version = session.Version;
            return new EditOperation(session, "Move timeline structure",
                () => session.Version != version ? "文档版本已改变。" : StructureRules.Error(session.Document, source, target, index),
                () => StructureRules.Move(session.Document, source, target, index));
        }
        // 释放前重新校验版本，原位不创建事务，也不触发图替换通知。
        public bool Move(TimelineRootItem source, TimelineGroup target, int boundary, long version)
        {
            var error = MoveError(source, target, boundary, version);
            if (error != null) throw new InvalidOperationException(error);
            if (StructureRules.IsNoOp(session.Document, source, target, boundary)) return false;
            Reorder(source, target, boundary).Execute();
            return true;
        }
        public void DeleteSelection()
        {
            Deletion(selection()).Execute();
        }

        public void MoveTrack(TimelineTrack track, TimelineGroup target, int index)
        {
            var operation = Reorder(track, target, index);
            if (!operation.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            if (StructureRules.IsNoOp(session.Document, track, target, index)) return;
            operation.Execute();
        }
    }

    // 名称、锁定与启用是节点编辑，不承担结构创建或移动。
    public sealed class NodeEditing
    {
        private readonly DocumentSession session;
        public NodeEditing(DocumentSession session) => this.session = session ?? throw new ArgumentNullException(nameof(session));
        public void Rename(IDirectable item, string name)
        {
            new EditOperation(session, "Rename", () => Error(item, false), () =>
            {
                if (item is TimelineGroup group) group.Name = name ?? "";
                else if (item is TimelineTrack track) track.Name = name ?? "";
                else if (item is TimelineClip clip) clip.Name = name ?? "";
            }).Execute();
        }
        public void ToggleLock(IDirectable item)
        {
            new EditOperation(session, "Toggle lock", () => Error(item, true), () => item.IsLocked = !item.IsLocked).Execute();
        }
        public void ToggleActive(IDirectable item)
        {
            new EditOperation(session, "Toggle active", () => Error(item, false), () => item.IsActive = !item.IsActive).Execute();
        }
        private string Error(IDirectable item, bool changingLock)
            => !StructureRules.Contains(session.Document, item) || (changingLock ? item.Parent?.IsLocked == true : item.IsLocked)
                ? "对象已失效或锁定。" : null;
    }
}
