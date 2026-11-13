using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Workbench
{
    public sealed class UIToolkitPreferencesWindow : EditorWindow
    {
        public static void ShowFor(EditorWindow owner)
        {
            var window = CreateInstance<UIToolkitPreferencesWindow>();
            window.ShowAsDropDown(new Rect(owner.position.xMax - 405, owner.position.y + 20, 400, 5), new Vector2(400, 310));
        }

        public void CreateGUI()
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
            var timing = Box(); var mode = new EnumField(Lan.PreferencesTimeStepMode, Prefs.timeStepMode); timing.Add(mode);
            mode.RegisterValueChangedCallback(e => { Prefs.timeStepMode = (Prefs.TimeStepMode)e.newValue; CreateGUI(); });
            if (Prefs.timeStepMode == Prefs.TimeStepMode.Seconds)
            {
                var step = new PopupField<float>(Lan.PreferencesSnapInterval, Prefs.snapIntervals.ToList(), Math.Max(0, Array.IndexOf(Prefs.snapIntervals, Prefs.SnapInterval)));
                step.RegisterValueChangedCallback(e => Prefs.SnapInterval = e.newValue); timing.Add(step);
            }
            else
            {
                var fps = new PopupField<int>(Lan.PreferencesFrameRate, Prefs.frameRates.ToList(), Math.Max(0, Array.IndexOf(Prefs.frameRates, Prefs.FrameRate)));
                fps.RegisterValueChangedCallback(e => Prefs.FrameRate = e.newValue); timing.Add(fps);
            }
            var input = Box(); var magnet = new Toggle(Lan.PreferencesMagnetSnapping) { value = Prefs.MagnetSnapping, tooltip = Lan.PreferencesMagnetSnappingTips };
            magnet.RegisterValueChangedCallback(e => Prefs.MagnetSnapping = e.newValue); input.Add(magnet);
            var zoom = new Toggle(Lan.PreferencesScrollWheelZooms) { value = Prefs.scrollWheelZooms, tooltip = Lan.PreferencesScrollWheelZoomsTips };
            zoom.RegisterValueChangedCallback(e => Prefs.scrollWheelZooms = e.newValue); input.Add(zoom);
            var save = Box(); var pathRow = new VisualElement(); pathRow.style.flexDirection = FlexDirection.Row; save.Add(pathRow);
            var path = new TextField(Lan.PreferencesSavePath) { value = Prefs.savePath, isDelayed = true }; path.style.flexGrow = 1;
            path.RegisterValueChangedCallback(e => Prefs.savePath = e.newValue); pathRow.Add(path);
            pathRow.Add(new Button(() =>
            {
                var folder = EditorUtility.OpenFolderPanel(Lan.SelectFolder, Prefs.savePath, "");
                if (!string.IsNullOrEmpty(folder)) path.value = FileUtil.GetProjectRelativePath(folder);
            }) { text = "…" });
            var interval = new SliderInt(Lan.PreferencesAutoSaveTime, 5, 120) { value = Prefs.autoSaveSeconds, showInputField = true };
            interval.RegisterValueChangedCallback(e => Prefs.autoSaveSeconds = e.newValue); save.Add(interval);
            root.Add(new Button(() => Application.OpenURL("https://nobug.cn/book/414447506088261")) { text = Lan.PreferencesHelpDoc });
        }
    }
}
