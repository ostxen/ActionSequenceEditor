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
    // 属性区域保留自身面板注册；重绑时丢弃旧Presenter，外部Inspector可重新附着。
    internal sealed class SelectionPropertiesPanel : IDisposable
    {
        private readonly List<VisualElement> inspectors = new List<VisualElement>();
        private PropertyPresenter presenter;
        private Action refresh;
        public void Bind(PropertyPresenter value, Action changed) { presenter = value; refresh = changed; Refresh(); }
        public void Refresh() { foreach (var view in inspectors.ToArray()) Build(view); }
        public void Dispose()
        {
            presenter = null; refresh = null;
            foreach (var view in inspectors.ToArray()) view.Clear();
        }
        public VisualElement CreateView()
        {
            var view = new ScrollView(); inspectors.Add(view);
            view.RegisterCallback<DetachFromPanelEvent>(_ => inspectors.Remove(view));
            view.RegisterCallback<AttachToPanelEvent>(_ => { if (!inspectors.Contains(view)) inspectors.Add(view); Build(view); });
            Build(view); return view;
        }
        private void Build(VisualElement root)
        {
            root.Clear();
            if (presenter == null) return;
            foreach (var issue in presenter.CaptureIssues())
                root.Add(new Button(() => { presenter.SelectIssue(issue); refresh(); })
                    { name = "property-issue-" + issue.Key, text = issue.Message });
            var selectionTarget = presenter.CaptureSelectionTarget();
            var sequenceTarget = presenter.CaptureSequenceTarget();
            if (selectionTarget == null) { root.Add(new HelpBox(EditorText.NoSelection, HelpBoxMessageType.Info)); return; }
            var basic = new Foldout { text = string.Format(EditorText.InsBaseInfo, sequenceTarget.Title), value = true, viewDataKey = "timeline-basic-info" };
            basic.Q<Toggle>().style.fontSize = 18; basic.Q<Toggle>().style.unityFontStyleAndWeight = FontStyle.Bold; root.Add(basic);
            var properties = new PropertyFieldsView(presenter);
            basic.Add(new Label("Length: " + presenter.SequenceLength));
            properties.DrawFields(basic, sequenceTarget.Fields, refresh, 0);
            basic.contentContainer.style.marginLeft = 0;
            SetPropertyLabelWidths(basic);
            var panel = new VisualElement(); panel.style.marginTop = 4; root.Add(panel); properties.Populate(panel, selectionTarget, refresh);
            SetPropertyLabelWidths(panel);
            basic.SetEnabled(!selectionTarget.Locked);
            panel.SetEnabled(!selectionTarget.Locked);
        }
        // 只对顶层字段分配标题宽度，向量分量保留原生紧凑标签和输入空间。
        private static void SetPropertyLabelWidths(VisualElement root)
        {
            foreach (var label in root.Query<Label>().ToList().Where(l => l.ClassListContains("unity-base-field__label")))
            {
                if (label.parent.ClassListContains("unity-composite-field__field")) continue;
                label.style.minWidth = label.style.width = 150;
            }
        }
    }
}
