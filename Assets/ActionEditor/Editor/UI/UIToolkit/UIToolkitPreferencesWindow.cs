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

namespace NBC.ActionEditor.Workbench
{
    // 原生偏好控件只提交意图；失败保留待重试草稿，不改共享偏好。
    public sealed class UIToolkitPreferencesWindow : EditorWindow, ISettingsView
    {
        private SettingsPresenter presenter;
        private SettingsValue state;
        private Label error;
        private string errorMessage;
        private static readonly float[] Steps = { .001f, .01f, .1f };
        private static readonly int[] Rates = { 24, 25, 30, 60 };
        public void Bind(SettingsSession session)
        { presenter?.Dispose(); presenter = Composition.SettingsComposition.Create(this, session); }
        public void Render(SettingsValue value) { state = value; Draw(); }
        public void ShowError(string message)
        {
            errorMessage = message;
            if (error != null) error.text = message ?? string.Empty;
        }
        private void OnDisable() { presenter?.Dispose(); presenter = null; }
        public static void ShowFor(EditorWindow owner)
        {
            var window = CreateInstance<UIToolkitPreferencesWindow>();
            window.ShowAsDropDown(new Rect(owner.position.xMax - 405, owner.position.y + 20, 400, 5), new Vector2(400, 310));
        }

        public void CreateGUI()
        {
            if (presenter == null) presenter = Composition.SettingsComposition.Create(this);
            else Draw();
        }
        private void Draw()
        {
            var root = rootVisualElement; root.Clear(); root.style.paddingLeft = root.style.paddingRight = 5;
            var title = new Label(Lan.PreferencesTitle); title.style.fontSize = 22; title.style.unityFontStyleAndWeight = FontStyle.Bold; root.Add(title);
            VisualElement Box()
            {
                var box = new VisualElement(); box.style.marginTop = 4; box.style.paddingLeft = box.style.paddingRight = 4;
                box.style.paddingTop = box.style.paddingBottom = 3; box.style.backgroundColor = new Color(0, 0, 0, .15f); root.Add(box); return box;
            }
            var language = new PopupField<string>("Language", Lan.AllLanguages.Keys.ToList(), Math.Max(0, Lan.AllLanguages.Keys.ToList().IndexOf(Lan.Language)));
            language.RegisterValueChangedCallback(e => { Lan.SetLanguage(e.newValue); CreateGUI(); }); Box().Add(language);
            var timing = Box(); var mode = new EnumField(Lan.PreferencesTimeStepMode, state.TimeStepMode); timing.Add(mode);
            mode.RegisterValueChangedCallback(e => presenter.Change(mode: (TimeStepMode)e.newValue));
            if (state.TimeStepMode == TimeStepMode.Seconds)
            {
                var step = new PopupField<float>(Lan.PreferencesSnapInterval, Steps.ToList(), Math.Max(0, Array.IndexOf(Steps, state.SnapInterval)));
                step.RegisterValueChangedCallback(e => presenter.Change(snap: e.newValue)); timing.Add(step);
            }
            else
            {
                var fps = new PopupField<int>(Lan.PreferencesFrameRate, Rates.ToList(), Math.Max(0, Array.IndexOf(Rates, state.FrameRate)));
                fps.RegisterValueChangedCallback(e => presenter.Change(fps: e.newValue)); timing.Add(fps);
            }
            var input = Box(); var magnet = new Toggle(Lan.PreferencesMagnetSnapping) { value = state.MagnetSnapping, tooltip = Lan.PreferencesMagnetSnappingTips };
            magnet.RegisterValueChangedCallback(e => presenter.Change(magnet: e.newValue)); input.Add(magnet);
            var zoom = new Toggle(Lan.PreferencesScrollWheelZooms) { value = state.ScrollWheelZooms, tooltip = Lan.PreferencesScrollWheelZoomsTips };
            zoom.RegisterValueChangedCallback(e => presenter.Change(wheel: e.newValue)); input.Add(zoom);
            var save = Box(); var pathRow = new VisualElement(); pathRow.style.flexDirection = FlexDirection.Row; save.Add(pathRow);
            var path = new TextField(Lan.PreferencesSavePath) { value = state.SavePath, isDelayed = true }; path.style.flexGrow = 1;
            path.RegisterValueChangedCallback(e => presenter.Change(path: e.newValue)); pathRow.Add(path);
            pathRow.Add(new Button(() =>
            {
                var folder = EditorUtility.OpenFolderPanel(Lan.SelectFolder, state.SavePath, "");
                if (!string.IsNullOrEmpty(folder)) path.value = FileUtil.GetProjectRelativePath(folder);
            }) { text = "…" });
            var interval = new SliderInt(Lan.PreferencesAutoSaveTime, 5, 120) { value = state.AutoSaveSeconds, showInputField = true };
            interval.RegisterValueChangedCallback(e => presenter.Change(autoSave: e.newValue)); save.Add(interval);
            error = new Label(errorMessage ?? string.Empty) { name = "settings-error" }; root.Add(error);
            root.Add(new Button(() => UnityEngine.Application.OpenURL("https://nobug.cn/book/414447506088261")) { text = Lan.PreferencesHelpDoc });
        }
    }
}
