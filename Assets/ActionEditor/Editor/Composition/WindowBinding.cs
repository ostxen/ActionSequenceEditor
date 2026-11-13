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
        private bool disposed;
        public ActionEditorContext Context { get; }
        public DocumentPresenter Documents { get; }
        public PlaybackPresenter Playback { get; }
        public PropertyPresenter Properties { get; }
        public SettingsPresenter Settings { get; }
        public EditorSettings Preferences { get; private set; }
        public string SettingsError { get; private set; }
        public string DocumentName => Context.Source != null ? Context.Source.name : string.Empty;
        public DateTime LastSave => lastSave();

        public WindowBinding(object owner, Action repaint, Action changed)
            : this(owner, repaint, changed, App.Context, App.Views, Prefs.Session, () => App.LastSaveTime) { }

        // 会话与视图注册表显式配对，隔离宿主也可复用相同释放路径。
        public WindowBinding(object owner, Action repaint, Action changed, ActionEditorContext context,
            ActionEditorViews views, SettingsSession settings, Func<DateTime> lastSave)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.lastSave = lastSave ?? throw new ArgumentNullException(nameof(lastSave));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Documents = DocumentPresenterComposition.Create(Context);
            Playback = PlaybackComposition.Create(Context, settings);
            Properties = new PropertyPresenter(context.Properties, context.Structure, context.Clips, () => settings.Current, context.Selection);
            Settings = SettingsComposition.Create(this, settings);
            views.Attach(owner, repaint, changed);
            Context.Changed += OnChanged;
        }
        private void OnChanged() { if (!disposed) { changed(); repaint(); } }
        public void Render(EditorSettings value) { Preferences = value; repaint(); }
        public void ShowError(string message) { SettingsError = message; repaint(); }
        public void Select(params IDirectable[] items) { Context.Selection.Set(items, false); views.Repaint(); }
        public void SetTick(Action tick) => views.SetTick(owner, tick);
        public void PopulateSaveMenu(Action<string, Action> add)
        {
            add(Lan.Save, () => Documents.Invoke(DocumentIntent.Save));
            add(Lan.SaveAs, () => Documents.Invoke(DocumentIntent.SaveAs));
            add(Lan.Export, () => Documents.Invoke(DocumentIntent.Export));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Context.Changed -= OnChanged;
            Properties.CancelDraft(); Settings.Dispose(); views.Detach(owner);
            // 共享播放只有最后一个视图离开时才停止，不能中断仍打开的另一窗口。
            if (views.Count == 0) Context.Playback.Stop();
        }
    }
}
