using NBC.ActionEditor.Views.IMGUI.Controls;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using SettingsValue = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Views.IMGUI
{
    // 原生弹窗只绘制偏好快照，经Presenter提交实际变化。
    public class PreferencesWindow : PopupWindowContent, ISettingsView
    {
        private static Rect _myRect;
        private bool firstPass = true;
        private SettingsPresenter presenter;
        private SettingsValue state;
        private string error;
        private static readonly float[] Steps = { .001f, .01f, .1f };
        private static readonly int[] Rates = { 24, 25, 30, 60 };
        public void Bind(SettingsSession session)
        { presenter?.Dispose(); presenter = Composition.SettingsComposition.Create(this, session); }
        public void Render(SettingsValue value) { state = value; editorWindow?.Repaint(); }
        public void ShowError(string message) { error = message; editorWindow?.Repaint(); }
        public override void OnOpen() { }
        public override void OnClose() { presenter?.Dispose(); presenter = null; }

        public static void Show(Rect rect, System.Func<ISettingsView, SettingsPresenter> create)
        {
            _myRect = rect;
            var window = new PreferencesWindow(); window.presenter = create(window);
            PopupWindow.Show(new Rect(rect.x, rect.y, 0, 0), window);
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(_myRect.width, _myRect.height);
        }

        public override void OnGUI(Rect rect)
        {
            var color = GUI.color;
            try { DrawPreferences(); }
            finally { GUI.color = color; }
        }

        // 控件只提交用户变化，首次绘制不改写偏好。
        private void DrawPreferences()
        {
            if (presenter == null) return;
            GUILayout.BeginVertical("box");
            GUI.color = new Color(0, 0, 0, 0.3f);

            GUILayout.BeginHorizontal(Styles.HeaderBoxStyle);
            GUI.color = Color.white;
            GUILayout.Label($"<size=22><b>{Lan.PreferencesTitle}</b></size>");
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            GUILayout.BeginVertical("box");
            var lan = EditorTools.CleanPopup<string>("Language", Lan.Language,
                Lan.AllLanguages.Keys.ToList());
            if (lan != Lan.Language)
            {
                Lan.SetLanguage(lan);
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");
            var mode = (TimeStepMode)EditorGUILayout.EnumPopup(Lan.PreferencesTimeStepMode, state.TimeStepMode);
            if (mode != state.TimeStepMode) presenter.Change(mode: mode);
            if (state.TimeStepMode == TimeStepMode.Seconds)
            {
                var step = EditorTools.CleanPopup<float>(Lan.PreferencesSnapInterval, state.SnapInterval, Steps.ToList());
                if (step != state.SnapInterval) presenter.Change(snap: step);
            }
            else
            {
                var fps = EditorTools.CleanPopup<int>(Lan.PreferencesFrameRate, state.FrameRate, Rates.ToList());
                if (fps != state.FrameRate) presenter.Change(fps: fps);
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");
            var magnet =
                EditorGUILayout.Toggle(new GUIContent(Lan.PreferencesMagnetSnapping, Lan.PreferencesMagnetSnappingTips),
                    state.MagnetSnapping);
            if (magnet != state.MagnetSnapping) presenter.Change(magnet: magnet);
            var wheel =
                EditorGUILayout.Toggle(
                    new GUIContent(Lan.PreferencesScrollWheelZooms, Lan.PreferencesScrollWheelZoomsTips),
                    state.ScrollWheelZooms);
            if (wheel != state.ScrollWheelZooms) presenter.Change(wheel: wheel);
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");

            var path = EditorTools.GUILayoutGetFolderPath(Lan.PreferencesSavePath, Lan.PreferencesSavePathTips, state.SavePath, true);
            if (path != state.SavePath) presenter.Change(path: path);

            EditorGUI.BeginChangeCheck();
            var interval = EditorGUILayout.IntSlider(
                new GUIContent(Lan.PreferencesAutoSaveTime, Lan.PreferencesAutoSaveTimeTips), state.AutoSaveSeconds, 5,
                120);
            if (EditorGUI.EndChangeCheck()) presenter.Change(autoSave: interval);
            GUILayout.EndVertical();


            GUILayout.EndVertical();

            DrawHelpButton();
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);

            if (firstPass || Event.current.type == EventType.Repaint)
            {
                firstPass = false;
                _myRect.height = GUILayoutUtility.GetLastRect().yMax + 5;
            }
        }

        protected virtual void DrawHelpButton()
        {
            if (GUILayout.Button(Lan.PreferencesHelpDoc))
            {
                UnityEngine.Application.OpenURL("https://nobug.cn/book/414447506088261");
            }
        }
    }
}