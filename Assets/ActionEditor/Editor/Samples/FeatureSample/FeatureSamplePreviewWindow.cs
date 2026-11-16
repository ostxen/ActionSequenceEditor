using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Samples
{
    // 绘制样例Presenter的值帧，不遍历或修改作者图。
    public sealed class FeatureSamplePreviewWindow : EditorWindow
    {
        private Vector2 scroll;
        private Presentation.Presenters.FeatureSamplePresenter presenter;
        // 预览由发起扩展显式绑定，两个独立宿主不会借用彼此文档。
        public void Bind(EditorExtensionContext context) => presenter = Composition.FeatureSampleComposition.Create(context);
        public static void ShowFor(EditorExtensionContext context)
        {
            var window = CreateInstance<FeatureSamplePreviewWindow>();
            window.titleContent = new GUIContent(EditorText.PreviewTitle);
            window.Bind(context); window.Show();
        }
        private void OnDisable() => presenter = null;
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(EditorText.PreviewHint, MessageType.Info);
            var state = presenter?.Capture();
            if (state == null || !state.Available)
            {
                EditorGUILayout.LabelField(EditorText.OpenSampleFirst);
                return;
            }
            EditorGUILayout.LabelField(EditorText.Playhead, state.Time.ToString("0.00") + " / " + state.Length.ToString("0.00") + " 秒");
            var draw = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(draw, state.Background);
            for (var i = 0; i < state.Intervals.Count; i++)
            {
                var interval = state.Intervals[i];
                var x = draw.x + 15 + interval.Position * Mathf.Max(0, draw.width - 70);
                EditorGUI.DrawRect(new Rect(x, draw.y + 12 + i % 3 * 25, 35, 20), interval.Tint);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(EditorText.CurrentIntervals, EditorStyles.boldLabel);
            foreach (var interval in state.Intervals)
                EditorGUILayout.LabelField(interval.Caption, string.Format(EditorText.PreviewInterval, interval.Progress.ToString("P0"), interval.Offset));
            EditorGUILayout.LabelField(EditorText.CurrentLoops, EditorStyles.boldLabel);
            foreach (var loop in state.Loops)
                EditorGUILayout.LabelField(loop.Caption, string.Format(EditorText.PreviewLoop, loop.Iteration, loop.Time.ToString("0.00")));
            EditorGUILayout.LabelField(EditorText.PreviousMarkers, EditorStyles.boldLabel);
            foreach (var marker in state.Markers)
                EditorGUILayout.LabelField(marker.Time.ToString("0.00") + " 秒", marker.Message + " (#" + marker.Code + ")");
            EditorGUILayout.EndScrollView();
        }
    }
}
