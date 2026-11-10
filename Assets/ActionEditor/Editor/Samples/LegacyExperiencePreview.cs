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
            new UnityEditor.UIElements.ToolbarButton(() => EditorWindow.GetWindow<LegacyExperiencePreview>("旧版样例预览").Show()) { text = "样例状态预览" };

        public override void OnGUI()
        {
            if (GUILayout.Button("样例状态预览", EditorStyles.toolbarButton))
                EditorWindow.GetWindow<LegacyExperiencePreview>("旧版样例预览").Show();
        }
    }

    public sealed class LegacyExperiencePreview : EditorWindow
    {
        private Vector2 scroll;
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("跟随两个界面共用的播放头显示状态。不会创建场景对象，不播放音频，不执行战斗逻辑。修改可保存回同一 JSON。", MessageType.Info);
            if (!(App.AssetData is LegacyExperienceAsset asset))
            {
                EditorGUILayout.LabelField("请先在原版编辑器创建或打开“旧版功能体验样例”。");
                return;
            }
            var time = AssetPlayer.Inst.CurrentTime;
            EditorGUILayout.LabelField("播放头", time.ToString("0.00") + " / " + asset.Length.ToString("0.00") + " 秒");
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
            EditorGUILayout.LabelField("当前区间", EditorStyles.boldLabel);
            foreach (var clip in active)
                EditorGUILayout.LabelField(clip.Caption, "进度 " + Mathf.Clamp01((time - clip.StartTime) / clip.Length).ToString("P0") + " · 偏移 " + clip.Offset);
            EditorGUILayout.LabelField("当前循环", EditorStyles.boldLabel);
            foreach (var clip in clips.OfType<LegacyExperienceLoop>().Where(c => time >= c.StartTime && time < c.EndTime))
            {
                var sourceTime = (time - clip.StartTime - clip.SubClipOffset) * clip.SubClipSpeed;
                EditorGUILayout.LabelField(clip.Caption, "第 " + (Mathf.FloorToInt(sourceTime / clip.SubClipLength) + 1) + " 次 · 相位 " + Mathf.Repeat(sourceTime, clip.SubClipLength).ToString("0.00"));
            }
            EditorGUILayout.LabelField("播放头之前的标记（状态，不是触发记录）", EditorStyles.boldLabel);
            foreach (var clip in clips.OfType<LegacyExperienceMarker>().Where(c => c.StartTime <= time).OrderBy(c => c.StartTime))
                EditorGUILayout.LabelField(clip.StartTime.ToString("0.00") + " 秒", clip.Message + " (#" + clip.EventCode + ")");
            EditorGUILayout.EndScrollView();
        }
    }
}
