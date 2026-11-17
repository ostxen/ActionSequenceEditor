using UnityEditor;
using UnityEngine;
using ActionSequenceEditor.Presentation.Timeline;

namespace ActionSequenceEditor.Views.IMGUI
{
    // 窗口拥有逐窗视图和工作区，时间轴编辑只转发给共享Presenter。
    public class ActionSequenceEditorWindow : EditorWindow
    {
        internal Composition.WindowBinding Binding { get; private set; }
        public TimelineWorkspace Workspace { get; } = new TimelineWorkspace();
        [MenuItem("ActionSequenceEditor/IMGUI", false, 0)]
        public static void Open()
        {
            GetWindow<ActionSequenceEditorWindow>().Show();
        }

        private WelcomeView _welcomeView;
        private EditorToolbar header;
        public TimelineSurface Timeline { get; private set; }
        public Presentation.Presenters.TimelinePresenter Presenter => Timeline?.Presenter;
        public Presentation.Presenters.PlaybackPresenter Playback => Binding.Playback;
        public Presentation.Presenters.SequencePresenter Sequence => Binding.Sequence;
        private double lastTick;
        internal Vector2 ContentOffset { get; private set; }

        // 独立宿主显式提供会话，复用同一窗口入口而不替换全局作者图。
        public void Bind(System.Func<object, System.Action, System.Action, Composition.WindowBinding> createBinding)
        {
            OnDisable();
            try
            {
                Binding = createBinding(this, Repaint, OnSequenceChanged);
                InitializeWindow();
            }
            catch { OnDisable(); throw; }
        }

        #region Init

        void InitializeAll()
        {
            EditorText.Load();
            Styles.Load();
            Workspace.LeftWidth = Styles.TimelineLeftWidth;
            _welcomeView = new WelcomeView(Sequence, rect => SettingsWindow.Show(rect, Binding.CreateSettings));
            header = new EditorToolbar(Binding, Workspace);
            Timeline?.Dispose(); Timeline = new TimelineSurface(this, Binding, Workspace);
        }

        #endregion

        #region Lifecycle
        void OnEnable()
        {
            OnDisable();
            try { Binding = ActionSequenceEditorHost.CreateBinding(this, Repaint, OnSequenceChanged); InitializeWindow(); }
            catch { OnDisable(); throw; }
        }

        // 初始化失败也走同一释放路径，不能遗留逐窗更新或属性订阅。
        private void InitializeWindow()
        {
            Binding.SetTick(OnEditorUpdate);

            minSize = new Vector2(500, 250);

            InitializeAll();
            lastTick = EditorApplication.timeSinceStartup;
            titleContent = new GUIContent(EditorText.Title);
        }

        void OnDisable()
        {
            var surface = Timeline; Timeline = null;
            var previous = Binding; Binding = null;
            try { surface?.Dispose(); }
            finally
            {
                try { previous?.Dispose(); }
                finally
                {
                    _welcomeView = null; header = null;
                }
            }
        }

        void OnEditorUpdate()
        {
            if (Binding == null) return;
            var now = EditorApplication.timeSinceStartup;
            Presenter?.Tick(now - lastTick); lastTick = now;
            Presenter?.SetPlaybackState(Playback.Playing, Playback.RangePlaybackEnabled);
        }

        private void OnSequenceChanged() { if (Binding == null) return; hasUnsavedChanges = Sequence.Dirty; Repaint(); }

        void OnGUI()
        {
            ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
            if (titleContent.text != EditorText.Title) titleContent = new GUIContent(EditorText.Title);
            try { DrawEditor(); }
            catch { CancelOwnEdit(); throw; }
            finally
            {
                if (Binding != null) hasUnsavedChanges = Sequence.Dirty;
            }
        }

        void DrawEditor()
        {
            // 关闭/重载已释放表面后，宿主仍可能补发一次GUI事件。
            if (Timeline == null) return;
            if (!Sequence.HasSequence)
            {
                _welcomeView.OnGUI();
                return;
            }

            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }

            Timeline.Draw(new Rect(0, 0, position.width, position.height), () =>
            {
                GUILayout.BeginArea(new Rect(0, 0, position.width, 40), EditorStyles.helpBox);
                try { header.OnGUI(new Rect(0, 0, position.width, 40)); }
                finally { GUILayout.EndArea(); }
            });
        }

        public override void SaveChanges() { Sequence.Invoke(Presentation.Contracts.SequenceIntent.Save); if (!Sequence.Dirty) base.SaveChanges(); }
        public override void DiscardChanges()
        {
            CancelOwnEdit();
            Binding?.Sequence.Discard();
            base.DiscardChanges();
        }

        void OnLostFocus()
        {
            CancelOwnEdit();
        }

        internal void CancelOwnEdit()
        {
            Presenter?.Cancel();
        }

        #endregion

    }
}