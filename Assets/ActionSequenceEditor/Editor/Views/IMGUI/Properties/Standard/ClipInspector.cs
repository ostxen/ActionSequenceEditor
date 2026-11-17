using ActionSequenceEditor.Presentation.Properties;
using ActionSequenceEditor.Model;
using ActionSequenceEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI.Properties.Standard
{
    // 原生片段属性控件只绘制共享快照并上报时间或混合意图。
    [CustomInspectors(typeof(ActionClip), true)]
    public class ClipInspector : PropertyInspectorBase
    {
        private PropertyFieldId ClipId => target.Id;
        public override void OnInspectorGUI() => ShowCommonInspector();
        protected void ShowCommonInspector(bool showBaseInspector = true)
        {
            var state = target.Clip;
            if (!state.Valid) EditorGUILayout.HelpBox("该剪辑无效。 请确保设置了所需的参数。", MessageType.Error);
            DrawTiming(state);
            DrawBlend(state);
            if (showBaseInspector) base.OnInspectorGUI();
        }
        private float TimeField(float value)
        {
            if (Presenter.Settings.TimeStepMode == Application.Settings.TimeStepMode.Frames)
                return (float)EditorGUILayout.DelayedIntField(Mathf.RoundToInt(value * Presenter.Settings.FrameRate), GUILayout.Width(80)) / Presenter.Settings.FrameRate;
            return EditorGUILayout.DelayedFloatField(value, GUILayout.Width(80));
        }
        private void DrawTiming(ClipPropertyState state)
        {
            GUILayout.BeginVertical("box");
            EditorGUI.BeginChangeCheck();
            var start = state.Start; var end = state.End; var length = state.Length;
            if (state.Scalable)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("IN", GUILayout.Width(30)); start = TimeField(start); GUILayout.FlexibleSpace();
                GUILayout.Label("◄"); length = TimeField(length); GUILayout.Label("►"); GUILayout.FlexibleSpace();
                GUILayout.Label("OUT", GUILayout.Width(30)); end = TimeField(end);
                GUILayout.EndHorizontal();
                if (state.ShowRange) EditorGUILayout.MinMaxSlider(ref start, ref end, state.Minimum, state.Maximum);
            }
            else { start = EditorGUILayout.Slider("IN", start, 0, state.PointMaximum); end = start; }
            if (EditorGUI.EndChangeCheck()) Presenter.Timing(ClipId, start, length != state.Length ? start + length : end);
            var warning = PropertyWarningText.Warning(state.Warning);
            if (warning != null) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            GUILayout.EndVertical();
        }
        private void DrawBlend(ClipPropertyState state)
        {
            if (!state.BlendInEnabled && !state.BlendOutEnabled) return;
            GUILayout.BeginHorizontal("box");
            if (state.BlendInEnabled) DrawBlendSlider(true, state.BlendIn, state.BlendInMaximum);
            if (state.BlendOutEnabled) DrawBlendSlider(false, state.BlendOut, state.BlendOutMaximum);
            GUILayout.EndHorizontal();
        }
        private void DrawBlendSlider(bool isBlendIn, float value, float maximum)
        {
            GUILayout.BeginVertical(); GUILayout.Label(isBlendIn ? "Blend In" : "Blend Out");
            EditorGUI.BeginChangeCheck(); var next = EditorGUILayout.Slider(value, 0, maximum);
            if (EditorGUI.EndChangeCheck()) Presenter.Blend(ClipId, isBlendIn, next);
            GUILayout.EndVertical();
        }
    }
}