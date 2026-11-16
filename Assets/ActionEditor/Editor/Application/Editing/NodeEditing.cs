using NBC.ActionEditor.Model;
using NBC.ActionEditor.Application.Sequences;
using System;

namespace NBC.ActionEditor.Application.Editing
{
    // 名称、锁定与启用是节点编辑，不承担结构创建或移动。
    public sealed class NodeEditing
    {
        private readonly SequenceSession session;
        public NodeEditing(SequenceSession session) => this.session = session ?? throw new ArgumentNullException(nameof(session));
        public void Rename(IDirectable item, string name)
        {
            new EditOperation(session, "Rename", () => Error(item, false), () =>
            {
                if (item is ActionGroup group) group.Name = name ?? "";
                else if (item is ActionTrack track) track.Name = name ?? "";
                else if (item is ActionClip clip) clip.Name = name ?? "";
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
            => !StructureRules.Contains(session.Sequence, item) || (changingLock ? item.Parent?.IsLocked == true : item.IsLocked)
                ? "对象已失效或锁定。" : null;
    }
}
