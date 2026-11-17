using ActionSequenceEditor.Infrastructure.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Presenters;
using ActionSequenceEditor.Presentation.Timeline;
using UnityEditor;
using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Application.Editing;
using ActionSequenceEditor.Application.Ports;

namespace ActionSequenceEditor.Composition
{
    // 逐窗装配Presenter，逐项注入编辑能力与共享播放头。
    public static class TimelineComposition
    {
        private sealed class Playhead : ITimelinePlayhead
        {
            private readonly ISequenceSampler sampler;
            public Playhead(ISequenceSampler sampler) { this.sampler = sampler; }
            public float Time => sampler.CurrentTime;
            public void Seek(float time) => sampler.Sample(time);
        }
        private sealed class Catalog : ITimelineTypeCatalog
        {
            public IReadOnlyList<TimelineTypeChoice> DerivedFrom(Type baseType) => ActionSequenceEditorTypeCatalog.DerivedFrom(baseType)
                .Where(info => !info.type.IsAbstract).Select(info => new TimelineTypeChoice(info.type, info.name, info.category)).ToArray();
        }
        private sealed class Dialogs : ITimelineDialogs
        {
            private readonly Views.IEditorDialogs dialogs;
            public Dialogs(Views.IEditorDialogs dialogs) { this.dialogs = dialogs; }
            public void Delete(string title, Action confirmed)
            { if (EditorUtility.DisplayDialog(title, title == EditorText.GroupDelete ? EditorText.GroupDeleteTips : EditorText.TrackDeleteTips, EditorText.TipsConfirm, EditorText.TipsCancel)) confirmed(); }
            public void Rename(string initialName, Action<string> submit) => dialogs.Rename(initialName, submit);
        }
        public static TimelinePresenter Create(SequenceSession session, SelectionState selection, StructureEditing structure,
            NodeEditing nodes, ClipboardEditing clipboardEditing, ClipEditing clips, ClipboardState clipboard, ISequenceSampler sampler,
            TimelineWorkspace workspace, ITimelineView view,
            Application.Settings.SettingsSession settings, SequencePresenter sequencePresenter, Views.IEditorDialogs dialogs)
        {
            var presenter = new TimelinePresenter(session, selection, structure, nodes, clipboardEditing,
                clips, workspace, new Playhead(sampler), () => settings.Current, view);
            presenter.Menus = new TimelineMenuPresenter(session, structure, nodes, clips, clipboardEditing, clipboard,
                selection, new Catalog(), new Dialogs(dialogs), Text, presenter.Reveal);
            presenter.HostAction += key =>
            {
                if (key == TimelineKey.Save) sequencePresenter.Invoke(SequenceIntent.Save);
                else if (key == TimelineKey.Undo) sequencePresenter.Invoke(SequenceIntent.Undo);
                else if (key == TimelineKey.Redo) sequencePresenter.Invoke(SequenceIntent.Redo);
            };
            return presenter;
        }
        private static string Text(string key)
        {
            switch (key)
            {
                case "AddTrack": return EditorText.MenuAddTrack;
                case "PasteTrack": return EditorText.MenuPasteTrack;
                case "Rename": return EditorText.Rename;
                case "GroupDisable": return EditorText.GroupDisable;
                case "GroupLocked": return EditorText.GroupLocked;
                case "GroupDelete": return EditorText.GroupDelete;
                case "TrackDisable": return EditorText.TrackDisable;
                case "TrackLocked": return EditorText.TrackLocked;
                case "TrackCopy": return EditorText.TrackCopy;
                case "TrackReplica": return EditorText.TrackReplica;
                case "TrackDelete": return EditorText.TrackDelete;
                case "ClipCopy": return EditorText.ClipCopy;
                case "ClipCut": return EditorText.ClipCut;
                case "ClipPaste": return EditorText.ClipPaste;
                case "ClipDelete": return EditorText.ClipDelete;
                case "MatchPreviousLoop": return EditorText.MatchPreviousLoop;
                case "MatchClipLength": return EditorText.MatchClipLength;
                case "MatchNextLoop": return EditorText.MatchNextLoop;
                default: throw new ArgumentException("未知菜单文本：" + key);
            }
        }
    }
}
