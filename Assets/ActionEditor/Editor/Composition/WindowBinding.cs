using System;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;

namespace NBC.ActionEditor.Composition
{
    // 逐窗绑定仅拥有Presenter、视图注册和释放，不接收共享作者服务集合。
    public sealed class WindowBinding : IDisposable, ISettingsView
    {
        private readonly object owner;
        private readonly Action repaint, changed, stopPlayback;
        private readonly ActionEditorViews views;
        private readonly Func<DateTime> lastSave;
        private readonly Func<string> documentName;
        private readonly Func<TimelineWorkspace, ITimelineView, TimelinePresenter> createTimeline;
        private readonly Action<IDirectable[]> select;
        private readonly SettingsSession settings;
        private bool disposed;
        public DocumentPresenter Documents { get; }
        public PlaybackPresenter Playback { get; }
        public PropertyPresenter Properties { get; }
        public SettingsPresenter Settings { get; }
        public EditorExtensionContext Extensions { get; }
        public EditorSettings Preferences { get; private set; }
        public string SettingsError { get; private set; }
        public string DocumentName => documentName();
        public DateTime LastSave => lastSave();
        public WindowBinding(object owner, Action repaint, Action changed, ActionEditorViews views,
            DocumentPresenter documents, PlaybackPresenter playback, PropertyPresenter properties, SettingsSession settings,
            EditorExtensionContext extensions, Func<string> documentName, Func<DateTime> lastSave,
            Func<TimelineWorkspace, ITimelineView, TimelinePresenter> createTimeline, Action<IDirectable[]> select, Action stopPlayback)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.lastSave = lastSave ?? throw new ArgumentNullException(nameof(lastSave));
            this.documentName = documentName ?? throw new ArgumentNullException(nameof(documentName));
            this.createTimeline = createTimeline ?? throw new ArgumentNullException(nameof(createTimeline));
            this.select = select ?? throw new ArgumentNullException(nameof(select));
            this.stopPlayback = stopPlayback ?? throw new ArgumentNullException(nameof(stopPlayback));
            this.settings = settings;
            Documents = documents; Playback = playback; Properties = properties; Extensions = extensions;
            Settings = SettingsComposition.Create(this, settings);
            views.Attach(owner, repaint, changed);
            Documents.Changed += OnChanged;
        }
        private void OnChanged()
        {
            if (disposed) return;
            try { changed(); }
            finally { repaint(); }
        }
        public void Render(EditorSettings value) { Preferences = value; repaint(); }
        public void ShowError(string message) { SettingsError = message; repaint(); }
        public void Select(params IDirectable[] items) { select(items); views.Repaint(); }
        public void SetTick(Action tick) => views.SetTick(owner, tick);
        public SettingsPresenter CreateSettings(ISettingsView view) => SettingsComposition.Create(view, settings);
        public TimelinePresenter CreateTimeline(TimelineWorkspace workspace, ITimelineView view) => createTimeline(workspace, view);
        public void PopulateSaveMenu(Action<string, Action> add) => DocumentPresenterComposition.PopulateSaveMenu(Documents, add);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Documents.Changed -= OnChanged;
            try { Properties.CancelDraft(); }
            finally
            {
                try { Settings.Dispose(); }
                finally { views.Detach(owner); if (views.Count == 0) stopPlayback(); }
            }
        }
    }
}
