using System.Linq;
using NBC.ActionEditor.Samples.LegacyExperience;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Samples
{
    [CustomHeader(typeof(LegacyExperienceAsset))]
    public sealed class LegacyExperienceHeader : HeaderBase
    {
        public override UnityEngine.UIElements.VisualElement CreateGUI() =>
            new UnityEditor.UIElements.ToolbarButton(() => EditorWindow.GetWindow<LegacyExperiencePreview>(Lan.PreviewTitle).Show()) { text = Lan.PreviewTitle };

        public override void OnGUI()
        {
            if (GUILayout.Button(Lan.PreviewTitle, EditorStyles.toolbarButton))
                EditorWindow.GetWindow<LegacyExperiencePreview>(Lan.PreviewTitle).Show();
        }
    }

    public sealed class LegacyExperiencePreview : EditorWindow
    {
        private Vector2 scroll;
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(Lan.PreviewHint, MessageType.Info);
            if (!(App.AssetData is LegacyExperienceAsset asset))
            {
                EditorGUILayout.LabelField(Lan.OpenSampleFirst);
                return;
            }
            var time = AssetPlayer.Inst.CurrentTime;
            EditorGUILayout.LabelField(Lan.Playhead, time.ToString("0.00") + " / " + asset.Length.ToString("0.00") + " 秒");
            var draw = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(draw, asset.Background);
            var clips = asset.groups.Where(g => g.IsActive).SelectMany(g => g.Tracks).Where(t => t.IsActive).SelectMany(t => t.Clips).ToArray();
            var active = clips.OfType<LegacyExperienceInterval>().Where(c => c.ShowInPreview && time >= c.StartTime && time < c.EndTime).ToArray();
            for (var i = 0; i < active.Length; i++)
            {
                var clip = active[i];
                var progress = Mathf.Clamp01((time - clip.StartTime) / clip.Length);
                var value = clip.Curve == null ? progress : clip.Curve.Evaluate(progress);
                var x = draw.x + 15 + Mathf.Clamp01(value) * Mathf.Max(0, draw.width - 70);
                var tint = clip.Tint; tint.a *= Mathf.Clamp01(clip.GetClipWeight(time - clip.StartTime)) * Mathf.Clamp01(clip.Gain);
                EditorGUI.DrawRect(new Rect(x, draw.y + 12 + i % 3 * 25, 35, 20), tint);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(Lan.CurrentIntervals, EditorStyles.boldLabel);
            foreach (var clip in active)
                EditorGUILayout.LabelField(clip.Caption, string.Format(Lan.PreviewInterval, Mathf.Clamp01((time - clip.StartTime) / clip.Length).ToString("P0"), clip.Offset));
            EditorGUILayout.LabelField(Lan.CurrentLoops, EditorStyles.boldLabel);
            foreach (var clip in clips.OfType<LegacyExperienceLoop>().Where(c => time >= c.StartTime && time < c.EndTime))
            {
                var sourceTime = (time - clip.StartTime - clip.SubClipOffset) * clip.SubClipSpeed;
                EditorGUILayout.LabelField(clip.Caption, string.Format(Lan.PreviewLoop, Mathf.FloorToInt(sourceTime / clip.SubClipLength) + 1, Mathf.Repeat(sourceTime, clip.SubClipLength).ToString("0.00")));
            }
            EditorGUILayout.LabelField(Lan.PreviousMarkers, EditorStyles.boldLabel);
            foreach (var clip in clips.OfType<LegacyExperienceMarker>().Where(c => c.StartTime <= time).OrderBy(c => c.StartTime))
                EditorGUILayout.LabelField(clip.StartTime.ToString("0.00") + " 秒", clip.Message + " (#" + clip.EventCode + ")");
            EditorGUILayout.EndScrollView();
        }
    }
}
