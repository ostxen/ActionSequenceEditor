using ActionSequenceEditor.Model;
using ActionSequenceEditor.Presentation.Timeline;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Presenters;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Views.UIToolkit
{
    // 原生窗口装配逐窗控件与Presenter，只持有自身工作区和捕获。
    public sealed class ActionSequenceEditorWindow : EditorWindow, IEditorDialogHost
    {
        public IEditorDialogs Dialogs { get; } = new EditorDialogAdapter();
        public const float HeaderHeight = 40, RowHeight = 28, BottomHeight = 16;
        public float LeftWidth { get => Workspace.LeftWidth; set => Workspace.LeftWidth = value; }
        public float ScrollY { get => Workspace.ScrollY; set => Workspace.ScrollY = value; }
        public TimelineCanvas Canvas { get; private set; }
        private EditorToolbar header;
        private TimelineViewport viewport;
        private Label error;
        private UIToolkitInspectorTarget target;
        private readonly SelectionPropertiesPanel properties = new SelectionPropertiesPanel();
        private IVisualElementScheduledItem refreshItem;
        private double lastTick;
        public string LastError { get; private set; }
        private Composition.WindowBinding binding;
        public PlaybackPresenter Playback => binding.Playback;
        public SequencePresenter Sequence => binding.Sequence;
        public TimelineWorkspace Workspace { get; } = new TimelineWorkspace();

        // 显式独立宿主与主入口共用控件和SaveChanges，不通过全局会话中转。
        public void Bind(Func<object, Action, Action, Composition.WindowBinding> createBinding)
        {
            OnDisable();
            try
            {
                binding = createBinding(this, Repaint, Refresh);
                InitializeWindow(); CreateGUI();
            }
            catch { OnDisable(); throw; }
        }

        [MenuItem("ActionSequenceEditor/UI Toolkit", false, -10)]
        public static void Open() => GetWindow<ActionSequenceEditorWindow>().Show();
        private void OnEnable()
        {
            OnDisable();
            try { binding = ActionSequenceEditorHost.CreateBinding(this, Repaint, Refresh); InitializeWindow(); }
            catch { OnDisable(); throw; }
        }
        // 创建Inspector代理失败时仍释放本窗口已经取得的绑定。
        private void InitializeWindow()
        {
            EditorText.Load();
            Styles.Load();
            LeftWidth = Styles.TimelineLeftWidth;
            titleContent = new GUIContent(EditorText.Title + " · UI Toolkit"); minSize = new Vector2(500, 250);
            if (target != null) DestroyImmediate(target);
            target = CreateInstance<UIToolkitInspectorTarget>(); target.hideFlags = HideFlags.HideAndDontSave; target.Owner = this;
            properties.Bind(binding.Properties, Refresh);
            binding.SetTick(Tick);
            lastTick = EditorApplication.timeSinceStartup;
        }
        private void OnDisable()
        {
            var previous = binding; binding = null;
            try { ReleaseVisuals(); }
            finally
            {
                try { previous?.Dispose(); }
                finally
                {
                    properties.Dispose();
                    if (target != null) DestroyImmediate(target); target = null;
                }
            }
        }
        private void ReleaseVisuals()
        {
            refreshItem?.Pause(); refreshItem = null;
            var canvas = Canvas; Canvas = null;
            header = null; viewport = null; error = null;
            try { canvas?.Dispose(); }
            finally { rootVisualElement.Clear(); }
        }
        private void OnLostFocus() => Canvas?.Cancel();
        private void Tick()
        {
            if (binding == null) return;
            if (EditorApplication.timeSinceStartup - lastTick < 0.03) return;
            var delta = EditorApplication.timeSinceStartup - lastTick;
            lastTick = EditorApplication.timeSinceStartup;
            Canvas?.Tick(delta);
            Canvas?.Presenter.SetPlaybackState(Playback.Playing, Playback.RangePlaybackEnabled);
            if (Sequence.HasSequence) SyncView();
        }
        public void CreateGUI()
        {
            if (binding == null) return;
            try { BuildGUI(); }
            catch { OnDisable(); throw; }
        }
        private void BuildGUI()
        {
            titleContent = new GUIContent(EditorText.Title + " · UI Toolkit");
            ReleaseVisuals();
            rootVisualElement.style.backgroundColor = new Color(.2f, .2f, .2f);
            header = new EditorToolbar(binding, Run, ShowTimelineError, ShowSettings);
            rootVisualElement.Add(header);
            Canvas = new TimelineCanvas(this);
            Canvas.Bind(binding.CreateTimeline(Workspace, Canvas));
            viewport = new TimelineViewport(Canvas, Workspace, SyncView);
            rootVisualElement.Add(viewport.Body); rootVisualElement.Add(viewport.Footer);
            error = new Label { name = "timeline-error" }; error.style.display = DisplayStyle.None; rootVisualElement.Add(error);
            Canvas.AttachRuler(); Canvas.RegisterCallback<GeometryChangedEvent>(_ => SyncView()); Refresh();
        }
        public void Seek(float time) { Playback.Seek(time); Canvas?.MarkDirtyRepaint(); }
        public void CreateDocument() => Sequence.Invoke(SequenceIntent.Create);
        public void OpenDocument() => Sequence.Invoke(SequenceIntent.Open);
        public void ShowSettings() => SettingsWindow.ShowFor(this, binding.CreateSettings);
        public void ShowTimelineError(string message)
        {
            LastError = message;
            if (error == null) return;
            error.text = message ?? ""; error.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public void Run(Action action)
        {
            try { action(); LastError = null; if (error != null) error.style.display = DisplayStyle.None; }
            catch (ExitGUIException) { throw; }
            catch (Exception e) { LastError = e.Message; if (error != null) { error.text = e.Message; error.style.display = DisplayStyle.Flex; } else Debug.LogException(e); }
        }
        public void Refresh()
        {
            Canvas?.Presenter.Refresh();
            if (Canvas == null || refreshItem != null) return;
            refreshItem = rootVisualElement.schedule.Execute(() =>
            {
                refreshItem = null; if (Canvas == null || binding == null) return;
                hasUnsavedChanges = Sequence.Dirty;
                header.RefreshSequence();
                Canvas.RefreshVisualState(); SyncView();
                properties.Refresh();
            });
        }
        public void SyncView()
        {
            if (header == null || Canvas?.State == null || viewport == null) return;
            header.style.display = Sequence.HasSequence ? DisplayStyle.Flex : DisplayStyle.None;
            header.Sync(LeftWidth);
            viewport.Sync();
        }
        public void Select(params IDirectable[] items) { binding.Select(items); ShowProperties(); }
        public void ShowProperties() { Selection.activeObject = target; Refresh(); }
        public VisualElement CreatePropertyView() => properties.CreateView();
        private void Save()
        {
            Sequence.Invoke(SequenceIntent.Save);
        }
        public override void SaveChanges() { Save(); if (!Sequence.Dirty) base.SaveChanges(); }
        public override void DiscardChanges()
        {
            Canvas?.Cancel();
            binding?.Sequence.Discard();
            base.DiscardChanges();
        }
    }
}
