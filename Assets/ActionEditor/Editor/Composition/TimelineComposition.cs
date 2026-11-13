using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using UnityEditor;

namespace NBC.ActionEditor.Composition
{
    // 逐窗装配Presenter，作者会话和播放头由显式共享上下文提供。
    public static class TimelineComposition
    {
        private sealed class Playhead : ITimelinePlayhead
        {
            private readonly ActionEditorContext context;
            public Playhead(ActionEditorContext context) { this.context = context; }
            public float Time => context.Player.CurrentTime;
            public void Seek(float time) => context.Player.Sample(time);
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
            public void Rename(IDirectable item) => RenameItemWindow.Show(item);
        }
        public static TimelinePresenter Create(ActionEditorContext context, TimelineWorkspace workspace, ITimelineView view)
        {
            var presenter = new TimelinePresenter(context.Session, context.Selection, context.Structure, context.ClipboardEditing,
                context.Clips, workspace, new Playhead(context), () => Prefs.Session.Current, view);
            presenter.Menus = new TimelineMenuPresenter(context.Session, context.Structure, context.ClipboardEditing, context.Clipboard,
                context.Selection, new Catalog(), new Dialogs(), Text);
            presenter.HostAction += key =>
            {
                if (key == TimelineKey.Save) App.SaveAsset();
                else if (key == TimelineKey.Undo) Undo.PerformUndo();
                else if (key == TimelineKey.Redo) Undo.PerformRedo();
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
