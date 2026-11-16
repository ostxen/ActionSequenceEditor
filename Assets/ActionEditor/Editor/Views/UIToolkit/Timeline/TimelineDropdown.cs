using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Views.UIToolkit
{
    // 类别逐层展开，所有项目、勾选和禁用状态来自同一菜单快照。
    internal static class TimelineDropdown
    {
        public static void Show(IReadOnlyList<TimelineMenuEntry> entries, VisualElement owner, Rect anchor, Action<TimelineMenuEntry> invoke, string prefix = "")
        {
            var menu = new GenericDropdownMenu();
            if (prefix.Length > 0)
                menu.AddItem("‹", false, () => owner.schedule.Execute(() => Show(entries, owner, anchor, invoke, prefix.TrimEnd('/').Contains('/')
                    ? prefix.Substring(0, prefix.TrimEnd('/').LastIndexOf('/') + 1) : "")));
            var branches = new HashSet<string>();
            foreach (var entry in entries.Where(entry => entry.Text.StartsWith(prefix, StringComparison.Ordinal)))
            {
                var name = entry.Text.Substring(prefix.Length); var slash = name.IndexOf('/');
                if (slash >= 0)
                {
                    var branch = name.Substring(0, slash); if (!branches.Add(branch)) continue;
                    var next = prefix + branch + "/";
                    if (entries.Any(item => item.Text.StartsWith(next, StringComparison.Ordinal) && item.Enabled))
                        menu.AddItem(branch + " ›", false, () => owner.schedule.Execute(() => Show(entries, owner, anchor, invoke, next)));
                    else menu.AddDisabledItem(branch + " ›", false);
                }
                else if (entry.Enabled) menu.AddItem(name, entry.Checked, () => invoke(entry));
                else menu.AddDisabledItem(name, entry.Checked);
            }
            DropDown(menu, anchor, owner);
        }

        internal static void DropDown(GenericDropdownMenu menu, Rect anchor, VisualElement owner)
        {
            var fontSize = Mathf.Max(16, owner.resolvedStyle.fontSize);
            if (owner.style.fontSize.keyword == StyleKeyword.Undefined)
                fontSize = Mathf.Max(fontSize, owner.style.fontSize.value.value);
            var content = menu.contentContainer;
            var labels = content.Query<Label>(className: GenericDropdownMenu.labelUssClassName).ToList();
            foreach (var label in labels)
            {
                label.style.fontSize = fontSize;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.flexShrink = 1;
                label.style.height = StyleKeyword.Auto;
                label.style.minHeight = Mathf.Ceil(fontSize * 1.5f);
                label.style.paddingTop = label.style.paddingBottom = fontSize * .25f;
                var row = label.parent;
                row.style.height = StyleKeyword.Auto;
                row.style.minHeight = Mathf.Ceil(fontSize * 1.8f);
            }
            // 挂到真实面板后按实际字体测量，长菜单在窗口内换行而不是裁字。
            content.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var width = fontSize * 12;
                foreach (var label in labels)
                    width = Mathf.Max(width, label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x + fontSize * 3);
                var available = owner.panel?.visualTree.worldBound.width - 8 ?? 0;
                if (available > 0) content.style.width = Mathf.Ceil(Mathf.Min(width, available));
            });
            menu.DropDown(anchor, owner);
        }
    }
}
