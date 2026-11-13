using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Workbench
{
    // 标题按钮和片段文字按值快照同步，按钮不参与标题拖动捕获。
    internal sealed class TimelineControls
    {
        private readonly VisualElement headers, labels;
        private readonly Action<int> menu;
        private readonly Dictionary<int, VisualElement> rows = new Dictionary<int, VisualElement>();
        private readonly Dictionary<int, Label> clips = new Dictionary<int, Label>();
        private string signature;
        public TimelineControls(VisualElement headers, VisualElement labels, Action<int> menu)
        { this.headers = headers; this.labels = labels; this.menu = menu; }
        public void Render(TimelineRenderState state, TimelinePresenter presenter)
        {
            var next = string.Join(";", state.Rows.Select(row => $"{row.Key}:{row.CanFold}:{row.Collapsed}:{row.Locked}:{row.Active}"));
            if (next != signature)
            {
                signature = next; headers.Clear(); rows.Clear();
                foreach (var row in state.Rows)
                {
                    var element = new VisualElement { name = "timeline-row-" + row.Key, pickingMode = PickingMode.Ignore };
                    element.style.position = Position.Absolute; element.style.left = 0; element.style.height = 26;
                    headers.Add(element); rows.Add(row.Key, element);
                    if (row.IsGroup)
                    {
                        if (row.CanFold) Icon(element, row.Collapsed ? Styles.CollapsedIcon : Styles.ExpandedIcon, 4, () => presenter.ToggleFold(row.Key), "timeline-fold");
                        else
                        {
                            var placeholder = new VisualElement { name = "timeline-fold-placeholder", pickingMode = PickingMode.Ignore };
                            placeholder.style.position = Position.Absolute; placeholder.style.left = 4; placeholder.style.width = 16; element.Add(placeholder);
                        }
                        Icon(element, Styles.MenuIcon, -.001f, () => menu(row.Key), "timeline-menu");
                    }
                    else
                    {
                        var type = Type.GetType(row.TypeName, false);
                        var texture = type == null ? null : DirectableExtends.GetIcon(type);
                        if (texture != null)
                        {
                            var icon = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                            icon.style.position = Position.Absolute; icon.style.left = 28; icon.style.top = 5;
                            icon.style.width = icon.style.height = 16; element.Add(icon);
                        }
                    }
                    var right = row.IsGroup ? 34 : 18;
                    if (row.Locked) { Icon(element, Styles.LockIcon, -(right - 16), () => presenter.ToggleLock(row.Key), "timeline-lock"); right += 18; }
                    if (!row.Active) Icon(element, Styles.EyeIcon, -(right - 16), () => presenter.ToggleActive(row.Key), "timeline-active");
                }
            }
            foreach (var row in state.Rows)
            { var element = rows[row.Key]; element.style.top = row.Bounds.Y; element.style.width = state.LeftWidth; }
            var current = new HashSet<int>(state.Clips.Where(clip => !clip.Point).Select(clip => clip.Key));
            foreach (var key in clips.Keys.Where(key => !current.Contains(key)).ToArray()) { clips[key].RemoveFromHierarchy(); clips.Remove(key); }
            labels.style.left = state.ContentLeft;
            foreach (var clip in state.Clips.Where(clip => !clip.Point))
            {
                if (!clips.TryGetValue(clip.Key, out var label))
                {
                    label = new Label { pickingMode = PickingMode.Ignore }; label.style.position = Position.Absolute;
                    label.style.unityFontStyleAndWeight = FontStyle.Bold; label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.fontSize = 12;
                    label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
                    clips.Add(clip.Key, label); labels.Add(label);
                }
                label.text = clip.Title; label.style.left = clip.Bounds.X - state.ContentLeft; label.style.top = clip.Bounds.Y;
                label.style.width = clip.Bounds.Width; label.style.height = clip.Bounds.Height;
                label.style.overflow = Overflow.Hidden;
            }
        }
        private static void Icon(VisualElement row, Texture2D texture, float left, Action action, string name)
        {
            var button = new Button(action) { name = name }; button.style.position = Position.Absolute;
            if (left < 0) button.style.right = -left; else button.style.left = left;
            button.style.top = 5; button.style.width = button.style.height = 16;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 0;
            button.style.marginLeft = button.style.marginRight = button.style.marginTop = button.style.marginBottom = 0;
            button.style.borderLeftWidth = button.style.borderRightWidth = button.style.borderTopWidth = button.style.borderBottomWidth = 0;
            button.style.backgroundColor = Color.clear; button.style.opacity = .5f;
            var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore }; image.style.flexGrow = 1;
            button.Add(image); row.Add(button);
        }
    }
}
