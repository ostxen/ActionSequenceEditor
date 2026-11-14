using UnityEditor;
using UnityEngine;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Views.IMGUI
{
    // 工具栏只绘制绑定的文档与播放Presenter，不加入全局视图注册表。
    public sealed class TimelineHeaderView
    {
        private readonly Composition.WindowBinding binding;
        private readonly Presentation.Timeline.TimelineWorkspace Workspace;
        private Rect Position;
        private PlaybackPresenter Playback => binding.Playback;
        private GUIStyle _customToolbarButtonStyle;
        private DocumentToolbar documentHeader;
        public TimelineHeaderView(Composition.WindowBinding binding, Presentation.Timeline.TimelineWorkspace workspace)
        { this.binding = binding; Workspace = workspace; documentHeader = new DocumentToolbar(binding); }

        public void OnGUI(Rect rect)
        {
            Position = rect;
            var color = GUI.color;
            try { DrawPlayControl(); DrawPlayHeader(); }
            finally { GUI.color = color; }
        }

        #region Play control

        private float _buttonWidth = 30;

        private void DrawPlayControl()
        {
            if (_customToolbarButtonStyle == null)
            {
                _customToolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
                {
                    fixedHeight = Styles.PlayControlHeight
                };
            }

            var rect = new Rect(0, 0, Workspace.LeftWidth, Styles.PlayControlHeight);
            GUILayout.BeginArea(rect);

            _buttonWidth = rect.width / 8;

            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (DrawButton(Styles.BackIcon, Lan.BackMenuTips))
            {
                binding.Documents.Invoke(DocumentIntent.Close);
            }

            if (DrawButton(Styles.FirstFrameIcon, Lan.FirstFrame))
            {
                Playback.Invoke(PlaybackIntent.First);
            }

            if (DrawButton(Styles.PrevFrameIcon, Lan.StepBackwardTips))
            {
                Playback.Invoke(PlaybackIntent.Previous);
            }

            EditorGUI.BeginChangeCheck();

            DrawToggle(Playback.Playing, Playback.Playing ? Styles.StopIcon : Styles.PlayIcon,
                Playback.Playing ? Lan.StopTips : Lan.PlayTips);
            if (EditorGUI.EndChangeCheck())
            {
                Playback.Invoke(PlaybackIntent.TogglePlay);
            }

            EditorGUI.BeginChangeCheck();
            DrawToggle(Playback.Paused, Styles.PauseIcon, Lan.PauseTips);
            if (EditorGUI.EndChangeCheck())
            {
                Playback.Invoke(PlaybackIntent.TogglePause);
            }

            if (DrawButton(Styles.NextFrameIcon, Lan.StepForwardTips))
            {
                Playback.Invoke(PlaybackIntent.Next);
            }

            if (DrawButton(Styles.LastFrameIcon, Lan.PlayForwardTips))
            {
                Playback.Invoke(PlaybackIntent.Last);
            }

            EditorGUI.BeginChangeCheck();
            DrawToggle(Playback.Range, Styles.RangeIcon, Lan.StepBackwardTips);
            if (EditorGUI.EndChangeCheck()) Playback.Invoke(PlaybackIntent.ToggleRange);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private bool DrawButton(Texture image, string tooltip)
        {
            return GUILayout.Button(new GUIContent(image, tooltip),
                _customToolbarButtonStyle, GUILayout.Width(_buttonWidth));
        }

        private bool DrawToggle(bool value, Texture image, string tooltip)
        {
            return GUILayout.Toggle(value, new GUIContent(image, tooltip), _customToolbarButtonStyle,
                GUILayout.Width(_buttonWidth));
        }

        #endregion

        #region Header

        private Rect _headerRect;

        private void DrawPlayHeader()
        {
            var gap = Workspace.LeftWidth + Styles.SplitterWidth;
            _headerRect = new Rect(Position.x + gap, Position.y,
                Position.width - gap,
                Position.height - Styles.HeaderHeight);

            GUILayout.BeginArea(_headerRect);
            documentHeader.OnGUI();
            GUILayout.EndArea();


            GUI.color = Color.black.WithAlpha(0.2f);
            GUI.DrawTexture(new Rect(_headerRect.x, _headerRect.y + _headerRect.height, _headerRect.width, 1),
                Styles.WhiteTexture);
        }

        #endregion
    }
}