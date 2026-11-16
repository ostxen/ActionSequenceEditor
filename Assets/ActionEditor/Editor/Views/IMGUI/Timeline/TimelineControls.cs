using System;
using System.Collections.Generic;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI
{
    // 控件仅消费显示快照并发送意图，空组不分配可点击折叠控件。
    internal sealed class TimelineControls
    {
        private readonly TimelinePresenter Presenter;
        private TimelineRenderState State => Presenter.State;
        public TimelineControls(TimelinePresenter presenter) { Presenter = presenter; }

        // 标题按钮先消费输入，避免折叠、锁定和菜单点击启动结构拖动。
        public void DrawHeaders()
        {
            foreach (var row in State.Rows)
            {
                if (row.Bounds.Bottom < 0 || row.Bounds.Y > State.Size.Height) continue;
                var y = row.Bounds.Y + 5;
                if (row.IsGroup && row.CanFold && GUI.Button(new Rect(4, y, 16, 16), row.Collapsed ? Styles.CollapsedIcon : Styles.ExpandedIcon, GUIStyle.none)) Presenter.ToggleFold(row.Key);
                if (!row.IsGroup)
                {
                    var type = Type.GetType(row.TypeName, false); var icon = type == null ? null : DirectableAppearance.GetIcon(type);
                    if (icon != null) GUI.DrawTexture(new Rect(28, y, 16, 16), icon);
                }
                if (row.IsGroup && GUI.Button(new Rect(State.LeftWidth - 16, y, 16, 16), Styles.MenuIcon, GUIStyle.none)) ShowMenu(Presenter.Menu(30, row.Bounds.Y + 13));
                var right = row.IsGroup ? 34 : 18;
                if (row.Locked) { if (GUI.Button(new Rect(State.LeftWidth - right + 16, y, 16, 16), Styles.LockIcon, GUIStyle.none)) Presenter.ToggleLock(row.Key); right += 18; }
                if (!row.Active && GUI.Button(new Rect(State.LeftWidth - right + 16, y, 16, 16), Styles.EyeIcon, GUIStyle.none)) Presenter.ToggleActive(row.Key);
            }
            var add = new Rect(15, State.Rows.Count * 28 - State.ScrollY + 2, Mathf.Max(60, State.LeftWidth - 30), 24);
            if (GUI.Button(add, EditorText.GroupAdd)) ShowMenu(Presenter.GroupMenu());
        }
        public void DrawScroll(Rect body)
        {
            var height = State.Rows.Count * State.Size.RowHeight + 32;
            if (height <= body.height) return;
            var value = GUI.VerticalScrollbar(new Rect(body.width - 14, body.y, 14, body.height), State.ScrollY, body.height, 0, height);
            if (value != State.ScrollY) Presenter.Scroll(value);
        }
        public void DrawFooter(Rect bounds)
        {
            var minimum = State.Minimum; var maximum = State.Maximum;
            var rect = new Rect(State.ContentLeft + 5, bounds.height - 16, bounds.width - State.ContentLeft - 10, 18);
            EditorGUI.BeginChangeCheck(); EditorGUI.MinMaxSlider(rect, ref minimum, ref maximum, 0, Mathf.Max(.25f, State.Duration));
            if (EditorGUI.EndChangeCheck()) Presenter.ViewRange(minimum, maximum);
            if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && rect.Contains(Event.current.mousePosition)) { Presenter.Fit(); Event.current.Use(); }
        }
        public void ShowMenu(IReadOnlyList<TimelineMenuEntry> entries)
        { BuildMenu(entries).ShowAsContext(); }
        public GenericMenu ContextMenu(float x, float y) => BuildMenu(Presenter.Menu(x, y));
        // 菜单适配只转发可用性与意图，供右键入口和验证使用同一控件链。
        private GenericMenu BuildMenu(IReadOnlyList<TimelineMenuEntry> entries)
        {
            var menu = new GenericMenu(); foreach (var entry in entries)
                if (entry.Enabled) menu.AddItem(new GUIContent(entry.Text), entry.Checked, () => Presenter.Invoke(entry));
                else menu.AddDisabledItem(new GUIContent(entry.Text), entry.Checked);
            return menu;
        }
    }
}
