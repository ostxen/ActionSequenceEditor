using System;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 逐窗组合对象持有Presenter和订阅，关闭只释放本窗口的绑定。
    public sealed class WindowBinding : IDisposable, ISettingsView
    {
        private readonly object owner;
        private readonly Action repaint;
        private readonly Action changed;
        private readonly ActionEditorViews views;
        private readonly Func<DateTime> lastSave;
        private readonly SettingsSession settings;
        private bool disposed;
        public ActionEditorContext Context { get; }
        public DocumentPresenter Documents { get; }
        public PlaybackPresenter Playback { get; }
        public PropertyPresenter Properties { get; }
        public SettingsPresenter Settings { get; }
        public EditorExtensionContext Extensions { get; }
        public EditorSettings Preferences { get; private set; }
        public string SettingsError { get; private set; }
        public string DocumentName => Context.Source != null ? Context.Source.name : string.Empty;
        public DateTime LastSave => lastSave();

        public WindowBinding(object owner, Action repaint, Action changed)
            : this(owner, repaint, changed, ActionEditorHost.Context, ActionEditorHost.Views, Prefs.Session,
                () => ActionEditorHost.Context.AutoSave.LastSaved) { }

        // 会话与视图注册表显式配对，隔离宿主也可复用相同释放路径。
        public WindowBinding(object owner, Action repaint, Action changed, ActionEditorContext context,
            ActionEditorViews views, SettingsSession settings, Func<DateTime> lastSave, Func<bool, string> chooseSavePath = null)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.lastSave = lastSave ?? throw new ArgumentNullException(nameof(lastSave));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            this.settings = settings;
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Extensions = new EditorExtensionContext(() => context.Document, () => context.Player.CurrentTime);
            Documents = DocumentPresenterComposition.Create(Context, chooseSavePath);
            Playback = PlaybackComposition.Create(Context, settings);
            Properties = PropertyComposition.Create(context, settings);
            Settings = SettingsComposition.Create(this, settings);
            views.Attach(owner, repaint, changed);
            Context.Changed += OnChanged;
        }
        private void OnChanged()
        {
            if (disposed) return;
            try { changed(); }
            finally { repaint(); }
        }
        public void Render(EditorSettings value) { Preferences = value; repaint(); }
        public void ShowError(string message) { SettingsError = message; repaint(); }
        public void Select(params IDirectable[] items) { Context.Selection.Set(items, false); views.Repaint(); }
        public void SetTick(Action tick) => views.SetTick(owner, tick);
        // 时间轴快捷键和交互偏好复用本窗口的依赖，不重新获取全局宿主。
        public TimelinePresenter CreateTimeline(Presentation.Timeline.TimelineWorkspace workspace, ITimelineView view)
            => TimelineComposition.Create(Context, workspace, view, settings, Documents);
        public void PopulateSaveMenu(Action<string, Action> add)
        {
            DocumentPresenterComposition.PopulateSaveMenu(Documents, add);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Context.Changed -= OnChanged;
            try { Properties.CancelDraft(); }
            finally
            {
                try { Settings.Dispose(); }
                finally
                {
                    views.Detach(owner);
                    // 最后视图离开时才停止共享播放；前面的释放异常不能遗留注册。
                    if (views.Count == 0) Context.Playback.Stop();
                }
            }
        }
    }
}
