using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 菜单的可用性与动作来自同一应用用例，控件只展示描述。
    public sealed class TimelineMenuPresenter
    {
        private readonly DocumentSession session;
        private readonly StructureEditing structure;
        private readonly ClipboardEditing clipboard;
        private readonly ClipboardState copied;
        private readonly SelectionState selection;
        private readonly ITimelineTypeCatalog catalog;
        private readonly ITimelineDialogs dialogs;
        private readonly Func<string, string> text;
        private readonly Action<IDirectable> reveal;
        public TimelineMenuPresenter(DocumentSession session, StructureEditing structure, ClipboardEditing clipboard, ClipboardState copied,
            SelectionState selection, ITimelineTypeCatalog catalog, ITimelineDialogs dialogs, Func<string, string> text, Action<IDirectable> reveal)
        { this.session = session; this.structure = structure; this.clipboard = clipboard; this.copied = copied;
            this.selection = selection; this.catalog = catalog; this.dialogs = dialogs; this.text = text; this.reveal = reveal; }

        public IReadOnlyList<TimelineMenuEntry> Groups() => Creation(session.Document, typeof(TimelineGroup), 0, "").ToArray();
        public IReadOnlyList<TimelineMenuEntry> Build(IDirectable item, bool header, float time)
        {
            var entries = new List<TimelineMenuEntry>();
            if (!structure.Contains(item)) return entries;
            void Entry(string key, Action invoke, bool enabled = true, bool check = false) => entries.Add(new TimelineMenuEntry(text(key), enabled, invoke, check));
            if (item is TimelineGroup group)
            {
                entries.AddRange(Creation(group, typeof(TimelineTrack), 0, text("AddTrack") + "/"));
                if (copied.Type != null && typeof(TimelineTrack).IsAssignableFrom(copied.Type)) Entry("PasteTrack", () => clipboard.Paste(group), clipboard.CanPaste(group));
                Entry("GroupDisable", () => structure.ToggleActive(group), !group.IsLocked, !group.IsActive);
                Entry("GroupLocked", () => structure.ToggleLock(group), true, group.IsLocked);
                Entry("Rename", () => dialogs.Rename(group), !group.IsLocked);
                Entry("GroupDelete", () => dialogs.Delete(text("GroupDelete"), () => structure.Delete(group)), !group.IsLocked);
            }
            else if (item is TimelineTrack track && header)
            {
                Entry("TrackDisable", () => structure.ToggleActive(track), !track.IsLocked, !track.IsActive);
                Entry("TrackLocked", () => structure.ToggleLock(track), track.Parent?.IsLocked != true, track.IsLocked);
                Entry("TrackCopy", () => clipboard.Copy(track));
                Entry("TrackReplica", () => clipboard.Duplicate(track), !track.IsLocked && !DirectableTypeInfo.For(track.GetType()).IsUnique);
                Entry("TrackDelete", () => dialogs.Delete(text("TrackDelete"), () => structure.Delete(track)), !track.IsLocked);
            }
            else if (item is TimelineClip clip)
            {
                selection.Set(new[] { clip }, true);
                Entry("ClipCopy", () => clipboard.Copy(clip), !clip.IsLocked);
                Entry("ClipCut", () => clipboard.Copy(clip, true), !clip.IsLocked);
                if (clip is ISubClipContainable loop && loop.SubClipLength > 0)
                {
                    Entry("MatchPreviousLoop", () => session.Edit("Match previous loop", clip.TryMatchPreviousSubClipLoop), !clip.IsLocked);
                    Entry("MatchClipLength", () => session.Edit("Match source", clip.TryMatchSubClipLength), !clip.IsLocked);
                    Entry("MatchNextLoop", () => session.Edit("Match next loop", clip.TryMatchNexSubClipLoop), !clip.IsLocked);
                }
                Entry("ClipDelete", () => structure.Delete(clip), !clip.IsLocked);
            }
            else if (item is TimelineTrack owner)
            {
                entries.AddRange(Creation(owner, typeof(TimelineClip), time, ""));
                if (copied.Type != null && typeof(TimelineClip).IsAssignableFrom(copied.Type))
                    entries.Add(new TimelineMenuEntry(string.Format(text("ClipPaste"), copied.Type.Name), clipboard.CanPaste(owner), () => clipboard.Paste(owner, time)));
            }
            return entries.AsReadOnly();
        }
        private IEnumerable<TimelineMenuEntry> Creation(object parent, Type baseType, float time, string prefix)
        {
            if (session.Document == null || parent == null) yield break;
            foreach (var choice in catalog.DerivedFrom(baseType))
            {
                var command = structure.Creation(parent, choice.Type, time);
                // 不显示不适用的业务类型；当前锁定或唯一冲突则保留禁用项。
                var facts = DirectableTypeInfo.For(choice.Type);
                if (facts.AttachableTypes != null && !facts.AttachableTypes.Contains(parent.GetType())) continue;
                yield return new TimelineMenuEntry(prefix + (string.IsNullOrEmpty(choice.Category) ? "" : choice.Category + "/") + choice.Name,
                    command.CanExecute(out _), () =>
                    {
                        var created = structure.Create(parent, choice.Type, time);
                        selection.Set(new[] { created }, true);
                        reveal(created);
                    });
            }
        }
    }
}
