using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Presenters;
using ActionSequenceEditor.Presentation.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Views.UIToolkit
{
    // 时间轴容器只绑定值快照、原生控件和输入适配，不拥有作者手势状态。
    public sealed class TimelineCanvas : VisualElement, ITimelineView, IDisposable
    {
        private readonly ActionSequenceEditorWindow owner;
        private readonly VisualElement ruler, insertion, headers, labels, welcome, guides;
        private readonly VisualElement[] guideLines = new VisualElement[2];
        private readonly Button addGroup;
        private readonly TimelineControls controls;
        private TimelineInputAdapter input;
        private bool disposed;
        public TimelinePresenter Presenter { get; private set; }
        public TimelineRenderState State { get; private set; }
        public int PointerId => input?.PointerId ?? -1;
        public float Left => State?.ContentLeft ?? owner.LeftWidth + 9;
        public float Scale => State == null ? 1 : TimelineRenderer.Scale(State);
        public float X(float time) => State == null ? Left : TimelineRenderer.X(State, time);
        public float TimeAt(float x) => State == null ? 0 : State.Minimum + (x - Left) / Scale;
        public TimelineCanvas(ActionSequenceEditorWindow owner)
        {
            this.owner = owner; name = "timeline-timeline"; focusable = true;
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/TimelineCanvas.uss"));
            style.flexGrow = 1; style.minWidth = 0; style.overflow = Overflow.Hidden;
            generateVisualContent += context => TimelineRenderer.Body(context, State, contentRect);
            ruler = new VisualElement { name = "timeline-ruler", pickingMode = PickingMode.Ignore };
            ruler.style.position = Position.Absolute; ruler.style.top = 20; ruler.style.right = 0; ruler.style.height = 20;
            ruler.generateVisualContent += context => TimelineRenderer.Ruler(context, State, ruler.contentRect);
            welcome = new VisualElement { name = "timeline-welcome" }; welcome.style.position = Position.Absolute;
            welcome.style.left = welcome.style.right = welcome.style.top = welcome.style.bottom = 0;
            welcome.style.alignItems = Align.Center; welcome.style.justifyContent = Justify.Center;
            if (Styles.Logo != null) { var logo = new Image { image = Styles.Logo }; logo.style.width = logo.style.height = 128; welcome.Add(logo); }
            var title = new Label(EditorText.Title); title.style.fontSize = 30; title.style.unityFontStyleAndWeight = FontStyle.Bold; welcome.Add(title);
            var hint = new Label(EditorText.WelcomeHint); hint.style.marginTop = 20; welcome.Add(hint);
            var create = new Button(owner.CreateDocument) { text = EditorText.CreateSequence };
            var open = new Button(owner.OpenDocument) { text = EditorText.OpenSequence };
            foreach (var button in new[] { create, open }) { button.style.width = 220; button.style.height = 40; welcome.Add(button); }
            create.style.backgroundColor = new Color(.56f, .56f, .63f); welcome.Add(new Label(EditorText.WelcomeHelp));
            var preferences = new Button(owner.ShowSettings) { text = EditorText.Settings }; preferences.style.width = 220; preferences.style.height = 40; welcome.Add(preferences);
            Add(welcome);
            headers = new VisualElement { name = "timeline-row-headers", pickingMode = PickingMode.Ignore };
            headers.style.position = Position.Absolute; headers.style.left = headers.style.top = 0; Add(headers);
            labels = new VisualElement { pickingMode = PickingMode.Ignore }; labels.style.position = Position.Absolute;
            labels.style.top = labels.style.right = labels.style.bottom = 0; labels.style.overflow = Overflow.Hidden; Add(labels);
            controls = new TimelineControls(headers, labels, ShowHeaderMenu);
            addGroup = new Button(ShowGroupMenu) { text = EditorText.GroupAdd, name = "timeline-add-group" };
            addGroup.style.position = Position.Absolute; addGroup.style.left = 15; addGroup.style.height = 24;
            addGroup.style.marginLeft = addGroup.style.marginRight = addGroup.style.marginTop = addGroup.style.marginBottom = 0; Add(addGroup);
            insertion = new VisualElement { name = "timeline-insertion-overlay", pickingMode = PickingMode.Ignore };
            insertion.style.position = Position.Absolute; insertion.style.left = insertion.style.right = insertion.style.top = insertion.style.bottom = 0;
            insertion.generateVisualContent += context => TimelineRenderer.Insertion(context, State?.Insertion ?? default); Add(insertion);
            guides = new VisualElement { name = "timeline-drag-guides", pickingMode = PickingMode.Ignore };
            guides.style.position = Position.Absolute; guides.style.top = 20; guides.style.bottom = 16; guides.style.right = 0; guides.style.overflow = Overflow.Hidden;
            for (var i = 0; i < guideLines.Length; i++)
            {
                var line = new VisualElement { name = "timeline-drag-guide-" + i, pickingMode = PickingMode.Ignore };
                line.style.position = Position.Absolute; line.style.top = line.style.bottom = 0; line.style.width = 1;
                line.generateVisualContent += context =>
                {
                    var painter = context.painter2D; var height = line.contentRect.height;
                    if (line.ClassListContains("timeline-guide-solid")) TimelineRenderer.Line(painter, Vector2.zero, new Vector2(0, height), Color.white);
                    else for (var y = 0f; y < height; y += 10) TimelineRenderer.Line(painter, new Vector2(0, y), new Vector2(0, Math.Min(y + 5, height)), new Color(.48f, .48f, .48f));
                };
                var label = new Label { pickingMode = PickingMode.Ignore }; label.style.position = Position.Absolute; label.style.top = 0;
                label.style.height = 18; label.style.fontSize = 12; label.style.color = Color.white; label.style.backgroundColor = new Color(0, 0, 0, .8f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.marginLeft = label.style.marginTop = 0;
                line.Add(label); guideLines[i] = line; guides.Add(line);
            }
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 1 || Presenter == null || Presenter.Mode != TimelineGestureMode.Idle) return;
                var point = this.WorldToLocal(evt.position);
                ShowMenu(Presenter.Menu(point.x, point.y), new Rect(evt.position.x, evt.position.y, 0, 0));
                evt.StopPropagation();
            });
            RegisterCallback<GeometryChangedEvent>(_ => Resize());
        }
        public void Bind(TimelinePresenter presenter)
        {
            Presenter = presenter; input = new TimelineInputAdapter(this, owner.rootVisualElement, presenter);
            Resize(); presenter.Refresh();
        }
        private void Resize()
        {
            if (Presenter == null || disposed || float.IsNaN(contentRect.width) || float.IsNaN(contentRect.height)
                || float.IsNaN(owner.rootVisualElement.contentRect.width) || contentRect.width <= 0 || contentRect.height <= 0) return;
            Presenter.Resize(new TimelineSize(contentRect.width, contentRect.height, 28, Math.Max(1, owner.rootVisualElement.contentRect.width), 9));
        }
        public void Render(TimelineRenderState state)
        {
            State = state;
            welcome.style.display = state.HasSequence ? DisplayStyle.None : DisplayStyle.Flex;
            ruler.style.display = addGroup.style.display = state.HasSequence ? DisplayStyle.Flex : DisplayStyle.None;
            if (Presenter != null) controls.Render(state, Presenter);
            addGroup.style.top = state.Rows.Count * state.Size.RowHeight - state.ScrollY + 2;
            addGroup.style.width = Mathf.Max(60, state.LeftWidth - 30);
            ruler.style.left = guides.style.left = state.ContentLeft;
            guides.style.display = state.Guides.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < guideLines.Length; i++)
            {
                var line = guideLines[i];
                if (i >= state.Guides.Count) { line.style.display = DisplayStyle.None; continue; }
                var guide = state.Guides[i]; line.style.display = guide.Visible ? DisplayStyle.Flex : DisplayStyle.None;
                line.style.left = X(guide.Time) - Left; line.EnableInClassList("timeline-guide-solid", guide.Solid);
                var label = line.Q<Label>(); label.text = guide.Time.ToString("0.00");
                var width = label.MeasureTextSize(label.text, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x + 5;
                label.style.width = width; label.style.left = -width * .5f; line.MarkDirtyRepaint();
            }
            EnableInClassList("timeline-resize-horizontal", state.ResizeCursor);
            MarkDirtyRepaint(); ruler.MarkDirtyRepaint(); insertion.MarkDirtyRepaint();
        }
        public void SetCapture(bool captured) => input?.Capture(captured);
        public void ShowError(string message) => owner.ShowTimelineError(message);
        public void AttachRuler() { owner.rootVisualElement.Add(ruler); owner.rootVisualElement.Add(guides); }
        public void RefreshVisualState() => Presenter?.Refresh();
        public void SyncAddGroup() => Presenter?.Refresh();
        public void SyncRuler() { ruler.style.left = Left; ruler.MarkDirtyRepaint(); }
        public Rect ClipRect(IDirectable item, int row)
        {
            var key = Presenter.KeyFor(item); var clip = State.Clips.First(value => value.Key == key);
            return TimelineRenderer.Rect(clip.Bounds);
        }
        public void Cancel() { if (!disposed) Presenter?.Cancel(); }
        public void Tick(double deltaSeconds) { if (!disposed) Presenter?.Tick(deltaSeconds); }
        public void ShowGroupMenu() => ShowMenu(Presenter.GroupMenu(), addGroup.worldBound);
        private void ShowHeaderMenu(int key)
        {
            var row = State.Rows.Single(value => value.Key == key);
            ShowMenu(Presenter.Menu(30, row.Bounds.Y + row.Bounds.Height * .5f),
                new Rect(this.LocalToWorld(new Vector2(State.LeftWidth - 18, row.Bounds.Y + row.Bounds.Height)), Vector2.zero));
        }
        private void ShowMenu(IReadOnlyList<TimelineMenuEntry> entries, Rect anchor)
        {
            if (Presenter.Mode != TimelineGestureMode.Idle) return;
            TimelineDropdown.Show(entries, this, anchor, entry => Presenter.Invoke(entry));
        }
        public void Dispose()
        {
            if (disposed) return;
            Presenter?.Dispose(); input?.Dispose(); disposed = true;
            ruler.RemoveFromHierarchy(); guides.RemoveFromHierarchy(); State = null;
        }
    }
}
