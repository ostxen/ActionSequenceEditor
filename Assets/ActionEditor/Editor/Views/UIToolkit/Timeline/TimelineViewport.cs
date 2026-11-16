using NBC.ActionEditor.Model;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Views.UIToolkit
{
    // 时间轴表面周边拥有滚动条和视域范围，同步期间不回送控件事件。
    internal sealed class TimelineViewport
    {
        public VisualElement Body { get; } = new VisualElement();
        public VisualElement Footer { get; } = new VisualElement { name = "timeline-footer" };
        private readonly TimelineCanvas canvas;
        private readonly TimelineWorkspace workspace;
        private readonly Scroller scroll;
        private readonly MinMaxSlider range;
        private readonly VisualElement blank;
        private bool syncing;
        public TimelineViewport(TimelineCanvas canvas, TimelineWorkspace workspace, Action sync)
        {
            this.canvas = canvas; this.workspace = workspace;
            Body.style.flexGrow = 1; Body.style.minHeight = 0; Body.style.flexDirection = FlexDirection.Row;
            Body.Add(canvas);
            scroll = new Scroller(0, 0, value => { if (!syncing) canvas.Presenter.Scroll(value); }, SliderDirection.Vertical);
            scroll.style.width = 14; scroll.style.flexShrink = 0; Body.Add(scroll);
            Footer.style.height = ActionEditorWindow.BottomHeight; Footer.style.flexShrink = 0; Footer.style.flexDirection = FlexDirection.Row;
            blank = new VisualElement { name = "timeline-footer-blank" }; blank.style.width = workspace.LeftWidth + 9; blank.style.flexShrink = 0; Footer.Add(blank);
            range = new MinMaxSlider(0, 7, 0, 7) { name = "timeline-view-range" }; range.style.flexGrow = 1;
            range.RegisterValueChangedCallback(e => { if (!syncing && canvas.State.HasSequence) canvas.Presenter.ViewRange(e.newValue.x, e.newValue.y); });
            range.style.marginLeft = 0; range.style.marginRight = 5; range.style.marginTop = 0; range.style.height = 18;
            range.RegisterCallback<PointerDownEvent>(e => { if (e.clickCount == 2 && canvas.State.HasSequence) { canvas.Presenter.Fit(); sync(); } });
            Footer.Add(range);
        }
        public void Sync()
        {
            Footer.style.display = canvas.State.HasSequence ? DisplayStyle.Flex : DisplayStyle.None;
            blank.style.width = workspace.LeftWidth + 9;
            canvas.SyncRuler();
            syncing = true;
            try
            {
                var height = canvas.State.Rows.Count * ActionEditorWindow.RowHeight + 32;
                scroll.highValue = Mathf.Max(0, height - canvas.contentRect.height);
                scroll.style.display = canvas.State.HasSequence && scroll.highValue > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                scroll.Adjust(canvas.contentRect.height / Mathf.Max(1, height));
                workspace.ScrollY = Mathf.Clamp(workspace.ScrollY, 0, scroll.highValue); scroll.value = workspace.ScrollY;
                canvas.SyncAddGroup();
                if (canvas.State.HasSequence)
                {
                    range.highLimit = Mathf.Max(.001f, canvas.State.Duration);
                    range.SetValueWithoutNotify(new Vector2(workspace.ViewTimeMin, workspace.ViewTimeMax));
                }
            }
            finally { syncing = false; }
            canvas.MarkDirtyRepaint();
        }
    }
}
