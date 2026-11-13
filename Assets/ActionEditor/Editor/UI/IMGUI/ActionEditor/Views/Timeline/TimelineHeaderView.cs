using UnityEditor;
using UnityEngine;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor
{
    public class TimelineHeaderView : ViewBase
    {
        private PlaybackPresenter Playback => ((ActionEditorWindow)Window).Playback;
        private GUIStyle _customToolbarButtonStyle;
        private HeaderGUI documentHeader;
        protected override void OnInit() => documentHeader = new HeaderGUI(((ActionEditorWindow)Window).Binding);

        public override void OnDraw()
        {
            DrawPlayControl();
            DrawPlayHeader();
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
                ((ActionEditorWindow)Window).Documents.Invoke(DocumentIntent.Close);
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