using NBC.ActionEditor.Samples.FeatureSample;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Samples
{
    [CustomHeader(typeof(FeatureSampleDocument))]
    public sealed class FeatureSampleHeader : HeaderBase
    {
        public override void OnGUI()
        {
            if (GUILayout.Button(Lan.PreviewTitle, EditorStyles.toolbarButton))
                EditorWindow.GetWindow<FeatureSamplePreview>(Lan.PreviewTitle).Show();
        }
    }

    // 样例的Toolkit工具栏适配独立声明，不借用IMGUI实现。
    [ToolkitHeader(typeof(FeatureSampleDocument))]
    public sealed class FeatureSampleToolkitHeader : ToolkitHeaderView
    {
        public override UnityEngine.UIElements.VisualElement CreateGUI() =>
            new UnityEditor.UIElements.ToolbarButton(() => EditorWindow.GetWindow<FeatureSamplePreview>(Lan.PreviewTitle).Show()) { text = Lan.PreviewTitle };
    }

    public sealed class FeatureSamplePreview : EditorWindow
    {
        private Vector2 scroll;
        private Presentation.Presenters.FeatureSamplePresenter presenter;
        private void OnEnable() => presenter = Composition.FeatureSampleComposition.Create();
        private void OnDisable() => presenter = null;
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(Lan.PreviewHint, MessageType.Info);
            var state = presenter?.Capture();
            if (state == null || !state.Available)
            {
                EditorGUILayout.LabelField(Lan.OpenSampleFirst);
                return;
            }
            EditorGUILayout.LabelField(Lan.Playhead, state.Time.ToString("0.00") + " / " + state.Length.ToString("0.00") + " 秒");
            var draw = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(draw, state.Background);
            for (var i = 0; i < state.Intervals.Count; i++)
            {
                var interval = state.Intervals[i];
                var x = draw.x + 15 + interval.Position * Mathf.Max(0, draw.width - 70);
                EditorGUI.DrawRect(new Rect(x, draw.y + 12 + i % 3 * 25, 35, 20), interval.Tint);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(Lan.CurrentIntervals, EditorStyles.boldLabel);
            foreach (var interval in state.Intervals)
                EditorGUILayout.LabelField(interval.Caption, string.Format(Lan.PreviewInterval, interval.Progress.ToString("P0"), interval.Offset));
            EditorGUILayout.LabelField(Lan.CurrentLoops, EditorStyles.boldLabel);
            foreach (var loop in state.Loops)
                EditorGUILayout.LabelField(loop.Caption, string.Format(Lan.PreviewLoop, loop.Iteration, loop.Time.ToString("0.00")));
            EditorGUILayout.LabelField(Lan.PreviousMarkers, EditorStyles.boldLabel);
            foreach (var marker in state.Markers)
                EditorGUILayout.LabelField(marker.Time.ToString("0.00") + " 秒", marker.Message + " (#" + marker.Code + ")");
            EditorGUILayout.EndScrollView();
        }
    }
}
