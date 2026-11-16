using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using SettingsValue = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Views.UIToolkit
{
    // 原生偏好控件只提交意图；失败保留待重试草稿，不改共享偏好。
    public sealed class SettingsWindow : EditorWindow, ISettingsView
    {
        private SettingsPresenter presenter;
        private SettingsValue state;
        private Label error;
        private string errorMessage;
        private static readonly float[] Steps = { .001f, .01f, .1f };
        private static readonly int[] Rates = { 24, 25, 30, 60 };
        public void Bind(SettingsSession session)
        { presenter?.Dispose(); presenter = Composition.SettingsPresenterComposition.Create(this, session); }
        public void Render(SettingsValue value) { state = value; Draw(); }
        public void ShowError(string message)
        {
            errorMessage = message;
            if (error != null) error.text = message ?? string.Empty;
        }
        private void OnDisable() { presenter?.Dispose(); presenter = null; }
        public static void ShowFor(EditorWindow owner, Func<ISettingsView, SettingsPresenter> create)
        {
            var window = CreateInstance<SettingsWindow>();
            window.presenter = create(window);
            window.ShowAsDropDown(new Rect(owner.position.xMax - 405, owner.position.y + 20, 400, 5), new Vector2(400, 310));
        }

        public void CreateGUI()
        {
            if (presenter != null) Draw();
        }
        private void Draw()
        {
            var root = rootVisualElement; root.Clear(); root.style.paddingLeft = root.style.paddingRight = 5;
            var title = new Label(EditorText.SettingsTitle); title.style.fontSize = 22; title.style.unityFontStyleAndWeight = FontStyle.Bold; root.Add(title);
            VisualElement Box()
            {
                var box = new VisualElement(); box.style.marginTop = 4; box.style.paddingLeft = box.style.paddingRight = 4;
                box.style.paddingTop = box.style.paddingBottom = 3; box.style.backgroundColor = new Color(0, 0, 0, .15f); root.Add(box); return box;
            }
            var language = new PopupField<string>("Language", EditorText.AllLanguages.Keys.ToList(), Math.Max(0, EditorText.AllLanguages.Keys.ToList().IndexOf(EditorText.Language)));
            language.RegisterValueChangedCallback(e => { EditorText.SetLanguage(e.newValue); CreateGUI(); }); Box().Add(language);
            var timing = Box(); var mode = new EnumField(EditorText.SettingsTimeStepMode, state.TimeStepMode); timing.Add(mode);
            mode.RegisterValueChangedCallback(e => presenter.Change(timeStepMode: (TimeStepMode)e.newValue));
            if (state.TimeStepMode == TimeStepMode.Seconds)
            {
                var step = new PopupField<float>(EditorText.SettingsSnapInterval, Steps.ToList(), Math.Max(0, Array.IndexOf(Steps, state.SnapInterval)));
                step.RegisterValueChangedCallback(e => presenter.Change(snapIntervalSeconds: e.newValue)); timing.Add(step);
            }
            else
            {
                var fps = new PopupField<int>(EditorText.SettingsFrameRate, Rates.ToList(), Math.Max(0, Array.IndexOf(Rates, state.FrameRate)));
                fps.RegisterValueChangedCallback(e => presenter.Change(frameRate: e.newValue)); timing.Add(fps);
            }
            var input = Box(); var magnet = new Toggle(EditorText.SettingsMagnetSnapping) { value = state.MagnetSnapping, tooltip = EditorText.SettingsMagnetSnappingTooltip };
            magnet.RegisterValueChangedCallback(e => presenter.Change(magnetSnapping: e.newValue)); input.Add(magnet);
            var zoom = new Toggle(EditorText.SettingsScrollWheelZooms) { value = state.ScrollWheelZooms, tooltip = EditorText.SettingsScrollWheelZoomsTooltip };
            zoom.RegisterValueChangedCallback(e => presenter.Change(scrollWheelZooms: e.newValue)); input.Add(zoom);
            var save = Box(); var pathRow = new VisualElement(); pathRow.style.flexDirection = FlexDirection.Row; save.Add(pathRow);
            var path = new TextField(EditorText.SettingsSavePath) { value = state.DefaultSaveDirectory, isDelayed = true }; path.style.flexGrow = 1;
            path.RegisterValueChangedCallback(e => presenter.Change(defaultSaveDirectory: e.newValue)); pathRow.Add(path);
            pathRow.Add(new Button(() =>
            {
                var folder = EditorUtility.OpenFolderPanel(EditorText.SelectFolder, state.DefaultSaveDirectory, "");
                if (!string.IsNullOrEmpty(folder)) path.value = FileUtil.GetProjectRelativePath(folder);
            }) { text = "…" });
            var interval = new SliderInt(EditorText.SettingsAutoSaveInterval, 5, 120) { value = state.AutoSaveIntervalSeconds, showInputField = true };
            interval.RegisterValueChangedCallback(e => presenter.Change(autoSaveIntervalSeconds: e.newValue)); save.Add(interval);
            error = new Label(errorMessage ?? string.Empty) { name = "settings-error" }; root.Add(error);
            root.Add(new Button(() => UnityEngine.Application.OpenURL("https://nobug.cn/book/414447506088261")) { text = EditorText.SettingsHelpDocument });
        }
    }
}
