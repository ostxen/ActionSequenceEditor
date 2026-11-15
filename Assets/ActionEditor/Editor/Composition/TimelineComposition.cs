using NBC.ActionEditor.Infrastructure.Assets;
using NBC.ActionEditor.Views.IMGUI;
using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using UnityEditor;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Ports;

namespace NBC.ActionEditor.Composition
{
    // 逐窗装配Presenter，逐项注入编辑能力与共享播放头。
    public static class TimelineComposition
    {
        private sealed class Playhead : ITimelinePlayhead
        {
            private readonly IDocumentSampler sampler;
            public Playhead(IDocumentSampler sampler) { this.sampler = sampler; }
            public float Time => sampler.CurrentTime;
            public void Seek(float time) => sampler.Sample(time);
        }
        private sealed class Catalog : ITimelineTypeCatalog
        {
            public IReadOnlyList<TimelineTypeChoice> DerivedFrom(Type baseType) => ActionEditorTypeCatalog.DerivedFrom(baseType)
                .Where(info => !info.type.IsAbstract).Select(info => new TimelineTypeChoice(info.type, info.name, info.category)).ToArray();
        }
        private sealed class Dialogs : ITimelineDialogs
        {
            public void Delete(string title, Action confirmed)
            { if (EditorUtility.DisplayDialog(title, title == Lan.GroupDelete ? Lan.GroupDeleteTips : Lan.TrackDeleteTips, Lan.TipsConfirm, Lan.TipsCancel)) confirmed(); }
            public void Rename(string initialName, Action<string> submit) => RenameItemWindow.Show(initialName, submit);
        }
        public static TimelinePresenter Create(DocumentSession session, SelectionState selection, StructureEditing structure,
            NodeEditing nodes, ClipboardEditing clipboardEditing, ClipEditing clips, ClipboardState clipboard, IDocumentSampler sampler,
            TimelineWorkspace workspace, ITimelineView view,
            Application.Settings.SettingsSession settings, DocumentPresenter documents)
        {
            var presenter = new TimelinePresenter(session, selection, structure, nodes, clipboardEditing,
                clips, workspace, new Playhead(sampler), () => settings.Current, view);
            presenter.Menus = new TimelineMenuPresenter(session, structure, nodes, clips, clipboardEditing, clipboard,
                selection, new Catalog(), new Dialogs(), Text, presenter.Reveal);
            presenter.HostAction += key =>
            {
                if (key == TimelineKey.Save) documents.Invoke(DocumentIntent.Save);
                else if (key == TimelineKey.Undo) documents.Invoke(DocumentIntent.Undo);
                else if (key == TimelineKey.Redo) documents.Invoke(DocumentIntent.Redo);
            };
            return presenter;
        }
        private static string Text(string key)
        {
            switch (key)
            {
                case "AddTrack": return Lan.MenuAddTrack;
                case "PasteTrack": return Lan.MenuPasteTrack;
                case "Rename": return Lan.Rename;
                case "GroupDisable": return Lan.GroupDisable;
                case "GroupLocked": return Lan.GroupLocked;
                case "GroupDelete": return Lan.GroupDelete;
                case "TrackDisable": return Lan.TrackDisable;
                case "TrackLocked": return Lan.TrackLocked;
                case "TrackCopy": return Lan.TrackCopy;
                case "TrackReplica": return Lan.TrackReplica;
                case "TrackDelete": return Lan.TrackDelete;
                case "ClipCopy": return Lan.ClipCopy;
                case "ClipCut": return Lan.ClipCut;
                case "ClipPaste": return Lan.ClipPaste;
                case "ClipDelete": return Lan.ClipDelete;
                case "MatchPreviousLoop": return Lan.MatchPreviousLoop;
                case "MatchClipLength": return Lan.MatchClipLength;
                case "MatchNextLoop": return Lan.MatchNextLoop;
                default: throw new ArgumentException("未知菜单文本：" + key);
            }
        }
    }
}
