using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI
{
    // 文档工具栏只展示逐窗绑定，并将按钮意图交给对应Presenter。
    public sealed class SequenceToolbar
    {
        private readonly Composition.WindowBinding binding;
        public SequenceToolbar(Composition.WindowBinding binding) { this.binding = binding; }
        public void OnGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            DrawSourcePicker();
            DrawSequenceExtensionHeader();
            GUILayout.FlexibleSpace();

            DrawToolbarRight();

            GUILayout.EndHorizontal();
        }

        private void DrawSequenceExtensionHeader()
        {
            if (!binding.Sequence.HasSequence) return;
            var sequenceHeader = EditorExtensionFactory.CreateHeader(binding.Sequence.SequenceType);
            sequenceHeader?.OnGUI(binding.Extensions);
        }

        private void DrawSourcePicker()
        {
            var sourceName = binding.SourceName;
            var size = GUI.skin.label.CalcSize(new GUIContent(sourceName));
            var width = size.x + 8;
            if (width < 80) width = 80;
            if (GUILayout.Button($"[{sourceName}]", EditorStyles.toolbarDropDown, GUILayout.Width(width)))
            {
                EditorApplication.delayCall += () =>
                {
                    binding.Sequence.Invoke(Presentation.Contracts.SequenceIntent.Open);
                };
            }
        }


        private void DrawToolbarRight()
        {
            var color = GUI.color;
            try
            {
                GUI.color = Color.white.WithAlpha(0.3f);
                GUILayout.Label(string.Format(EditorText.HeaderLastSaveTime, binding.LastSave.ToString("HH:mm:ss")), EditorStyles.miniLabel);
            }
            finally { GUI.color = color; }

            if (GUILayout.Button(new GUIContent(Styles.SaveIcon, EditorText.SaveMenuHint), EditorStyles.toolbarButton,
                    GUILayout.Width(26)))
            {
                EditorApplication.delayCall += () => binding.Sequence.Invoke(Presentation.Contracts.SequenceIntent.Save);
            }
            if (Event.current.type == EventType.ContextClick && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                binding.PopulateSaveMenu((label, action) => menu.AddItem(new GUIContent(label), false, () => action()));
                menu.ShowAsContext(); Event.current.Use();
            }

            EditorGUI.BeginChangeCheck();
            var magnet = GUILayout.Toggle(binding.CurrentSettings.MagnetSnapping,
                new GUIContent(Styles.MagnetIcon, EditorText.OpenMagnetSnappingTips),
                EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck()) binding.Settings.Change(magnetSnapping: magnet);
            if (!string.IsNullOrEmpty(binding.SettingsError)) GUILayout.Label(new GUIContent("!", binding.SettingsError), EditorStyles.miniLabel);

            if (GUILayout.Button(new GUIContent(Styles.SettingsIcon, EditorText.OpenSettingsTooltip),
                    EditorStyles.toolbarButton, GUILayout.Width(26)))
            {
                SettingsWindow.Show(new Rect(Styles.ScreenWidth - 5 - 400 - Styles.TimelineLeftTotalWidth, 25, 400,
                    Styles.ScreenHeight - 70), binding.CreateSettings);
            }
        }
    }
}