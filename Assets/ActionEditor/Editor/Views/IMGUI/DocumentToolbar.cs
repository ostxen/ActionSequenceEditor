using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI
{
    // 文档工具栏只展示逐窗绑定，并将按钮意图交给对应Presenter。
    public sealed class DocumentToolbar
    {
        private readonly Composition.WindowBinding binding;
        public DocumentToolbar(Composition.WindowBinding binding) { this.binding = binding; }
        public void OnGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            DrawNowAssetName();
            DrawAssetsHeader();
            GUILayout.FlexibleSpace();

            DrawToolbarRight();

            GUILayout.EndHorizontal();
        }

        private void DrawAssetsHeader()
        {
            if (!binding.Documents.HasDocument) return;
            var customAssetHeader = EditorExtensionFactory.GetHeader(binding.Documents.DocumentType);
            customAssetHeader?.OnGUI(binding.Extensions);
        }

        private void DrawNowAssetName()
        {
            var gName = binding.DocumentName;
            var size = GUI.skin.label.CalcSize(new GUIContent(gName));
            var width = size.x + 8;
            if (width < 80) width = 80;
            if (GUILayout.Button($"[{gName}]", EditorStyles.toolbarDropDown, GUILayout.Width(width)))
            {
                EditorApplication.delayCall += () =>
                {
                    binding.Documents.Invoke(Presentation.Contracts.DocumentIntent.Open);
                };
            }
        }


        private void DrawToolbarRight()
        {
            var color = GUI.color;
            try
            {
                GUI.color = Color.white.WithAlpha(0.3f);
                GUILayout.Label(string.Format(Lan.HeaderLastSaveTime, binding.LastSave.ToString("HH:mm:ss")), EditorStyles.miniLabel);
            }
            finally { GUI.color = color; }

            if (GUILayout.Button(new GUIContent(Styles.SaveIcon, Lan.SaveMenuHint), EditorStyles.toolbarButton,
                    GUILayout.Width(26)))
            {
                EditorApplication.delayCall += () => binding.Documents.Invoke(Presentation.Contracts.DocumentIntent.Save);
            }
            if (Event.current.type == EventType.ContextClick && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                binding.PopulateSaveMenu((label, action) => menu.AddItem(new GUIContent(label), false, () => action()));
                menu.ShowAsContext(); Event.current.Use();
            }

            EditorGUI.BeginChangeCheck();
            var magnet = GUILayout.Toggle(binding.Preferences.MagnetSnapping,
                new GUIContent(Styles.MagnetIcon, Lan.OpenMagnetSnappingTips),
                EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck()) binding.Settings.Change(magnet: magnet);
            if (!string.IsNullOrEmpty(binding.SettingsError)) GUILayout.Label(new GUIContent("!", binding.SettingsError), EditorStyles.miniLabel);

            if (GUILayout.Button(new GUIContent(Styles.SettingsIcon, Lan.OpenPreferencesTips),
                    EditorStyles.toolbarButton, GUILayout.Width(26)))
            {
                PreferencesWindow.Show(new Rect(Styles.ScreenWidth - 5 - 400 - Styles.TimelineLeftTotalWidth, 25, 400,
                    Styles.ScreenHeight - 70), binding.CreateSettings);
            }
        }
    }
}