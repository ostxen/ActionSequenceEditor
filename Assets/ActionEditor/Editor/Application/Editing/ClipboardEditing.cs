using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Ports;
using System;
using System.Linq;

namespace NBC.ActionEditor.Application.Editing
{
    // 剪贴板归共享会话，窗口只发送复制或粘贴意图。
    public sealed class ClipboardState
    {
        public string Snapshot { get; internal set; }
        public Type Type { get; internal set; }
        public IDirectable CutSource { get; internal set; }
        public IDirectable Item { get; set; }
        public bool IsCut { get; set; }
    }

    // 粘贴在单次事务中重验源和目标，异常不丢失剪切源。
    public sealed class ClipboardEditing
    {
        private readonly DocumentSession session;
        private readonly StructureEditing structure;
        private readonly ClipboardState state;
        private readonly IFragmentCodec codec;
        private readonly Action<IDirectable> select;
        public ClipboardEditing(DocumentSession session, StructureEditing structure, ClipboardState state, IFragmentCodec codec, Action<IDirectable> select)
        { this.session = session; this.structure = structure; this.state = state; this.codec = codec; this.select = select; }
        public bool HasClipboard => state.Snapshot != null;
        public Type CopiedType => state.Type;
        public void Copy(IDirectable item, bool cut = false)
        {
            if (item == null || !structure.Contains(item) || cut && item.IsLocked)
                throw new InvalidOperationException("无法复制失效对象或剪切锁定对象。");
            var serialized = codec.Capture(item);
            state.Snapshot = serialized; state.Type = item.GetType(); state.CutSource = cut ? item : null;
            state.Item = item; state.IsCut = cut;
        }
        private string PasteError(IDirectable parent, float time)
        {
            if (state.Snapshot == null || state.Type == null) return "剪贴板为空。";
            if (session.Document == null) return "没有打开的文档。";
            if (float.IsNaN(time) || float.IsInfinity(time)) return "粘贴时间必须为有限值。";
            if (state.CutSource != null && structure.Contains(state.CutSource) && state.CutSource.IsLocked) return "剪切源已锁定。";
            if (parent == null && typeof(TimelineTrack).IsAssignableFrom(state.Type))
                return DirectableTypeInfo.For(state.Type).IsUnique && session.Document.RootItems.OfType<TimelineTrack>().Any(t => t.GetType() == state.Type && t != state.CutSource)
                    ? "根级已存在唯一轨道。" : null;
            if (parent == null || !structure.Contains(parent) || parent.IsLocked) return "粘贴目标已失效或锁定。";
            if (state.CutSource == parent) return "不能把对象剪切到自身。";
            if (typeof(TimelineTrack).IsAssignableFrom(state.Type) && parent is TimelineGroup group)
                return group.CanAddTrackOfType(state.Type) ? null : "轨道类型、唯一性或挂载规则不允许粘贴。";
            if (typeof(TimelineClip).IsAssignableFrom(state.Type) && parent is TimelineTrack track)
            {
                var info = DirectableTypeInfo.For(state.Type);
                if (info.AttachableTypes != null && !info.AttachableTypes.Contains(track.GetType())) return "片段类型不适用于当前轨道。";
                if (info.IsUnique && track.Clips.Any(c => c.GetType() == state.Type && c != state.CutSource)) return "当前轨道已存在唯一片段。";
                return null;
            }
            return "剪贴板类型不适用于当前目标。";
        }
        public bool CanPaste(IDirectable parent) => PasteError(parent, 0) == null;
        public IDirectable Paste(IDirectable parent, float time = 0)
        {
            IDirectable clone = null;
            new EditOperation(session, "Paste", () => PasteError(parent, time), () =>
            {
                clone = codec.Restore(state.Type, state.Snapshot);
                if (clone == null) throw new InvalidOperationException("剪贴板反序列化失败。");
                if (state.CutSource != null && structure.Contains(state.CutSource)) structure.Delete(state.CutSource);
                if (parent is TimelineGroup group && clone is TimelineTrack track) group.Tracks.Add(track);
                else if (parent == null && clone is TimelineTrack rootTrack) session.Document.AddRoot(rootTrack);
                else if (parent is TimelineTrack target && clone is TimelineClip clip) { clip.StartTime = Math.Max(0, time); target.Clips.Add(clip); }
                session.Document.Init();
            }).Execute();
            state.CutSource = null; state.IsCut = false; select(clone); return clone;
        }

        public void Duplicate(IDirectable item)
        {
            if (item == null || !structure.Contains(item) || item.IsLocked) throw new InvalidOperationException("锁定或失效对象不能复制副本。");
            var previousJson = state.Snapshot; var previousType = state.Type; var previousCut = state.CutSource;
            var previousItem = state.Item; var wasCut = state.IsCut;
            try { Copy(item); Paste(item.Parent, item is TimelineClip clip ? clip.EndTime : 0); }
            catch
            {
                state.Snapshot = previousJson; state.Type = previousType; state.CutSource = previousCut;
                state.Item = previousItem; state.IsCut = wasCut; throw;
            }
        }
    }
}
