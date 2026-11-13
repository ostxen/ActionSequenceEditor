using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Presentation.Timeline;

namespace NBC.ActionEditor
{
    // 窗口拥有逐窗视图和工作区，时间轴编辑只转发给共享Presenter。
    public class ActionEditorWindow : EditorWindow
    {
        public ActionEditorContext Context => App.Context;
        public TimelineWorkspace Workspace { get; } = new TimelineWorkspace();
        [MenuItem("NBC/Action Editor/Open Action Editor", false, 0)]
        public static void OpenDirectorWindow()
        {
            GetWindow<ActionEditorWindow>().Show();
        }

        private WelcomeView _welcomeView;
        private TimelineHeaderView header;
        public Views.IMGUI.TimelineSurface Timeline { get; private set; }
        public Presentation.Presenters.TimelinePresenter Presenter => Timeline?.Presenter;
        private double lastTick;
        internal Vector2 ContentOffset { get; private set; }

        #region Init

        void InitializeAll()
        {
            this.ReleaseViews();
            Lan.Load();
            Styles.Load();
            Workspace.LeftWidth = Styles.TimelineLeftWidth;
            Prefs.InitializeAssetTypes();
            App.OnInitialize?.Invoke();
            _welcomeView = this.CreateView<WelcomeView>();
            header = this.CreateView<TimelineHeaderView>();
            Timeline?.Dispose(); Timeline = new Views.IMGUI.TimelineSurface(this, Context, Workspace);
        }

        #endregion

        #region Lifecycle
        void OnEnable()
        {
            Workspace.Bind(Context.Document);
            ActionEditorHost.Window = this;
            App.Views.Attach(this, Repaint, OnDocumentChanged);
            EditorSceneManager.sceneSaving -= OnWillSaveScene;
            EditorSceneManager.sceneSaving += OnWillSaveScene;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            
            minSize = new Vector2(500, 250);
            
            InitializeAll();
            lastTick = EditorApplication.timeSinceStartup;
            titleContent = new GUIContent(Lan.Title);
        }

        void OnDisable()
        {
            CancelOwnEdit();
            Timeline?.Dispose(); Timeline = null;
            App.Views.Detach(this);
            if (ActionEditorHost.Window == this) ActionEditorHost.Window = null;
            this.ReleaseViews();
            App.Stop();
            EditorSceneManager.sceneSaving -= OnWillSaveScene;
            EditorApplication.update -= OnEditorUpdate;
            
            App.OnDisable?.Invoke();
        }

        void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            Presenter?.Tick(now - lastTick); lastTick = now;
            Presenter?.PlaybackState(App.IsPlay, App.IsRange);
            if (App.NeedForceRefresh)
            {
                this.Repaint();
            }
        }

        private void OnDocumentChanged() { Workspace.Bind(Context.Document); hasUnsavedChanges = Context.Session.IsDirty; App.Refresh(); Repaint(); }

        void OnGUI()
        {
            ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
            if (titleContent.text != Lan.Title) titleContent = new GUIContent(Lan.Title);
            try { DrawEditor(); }
            catch { CancelOwnEdit(); throw; }
            finally
            {
                hasUnsavedChanges = App.Session.IsDirty;
            }
        }

        void DrawEditor()
        {
            // 关闭/重载已释放表面后，宿主仍可能补发一次GUI事件。
            if (Timeline == null) return;
            if (App.AssetData == null)
            {
                _welcomeView.OnGUI(this.position);
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
            App.OnGUIEnd();
        }

        public override void SaveChanges() { App.SaveAsset(); if (!App.Session.IsDirty) base.SaveChanges(); }
        public override void DiscardChanges()
        {
            CancelOwnEdit();
            Context.Session.DiscardChanges();
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

        void OnWillSaveScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
        }

        #endregion

    }
}