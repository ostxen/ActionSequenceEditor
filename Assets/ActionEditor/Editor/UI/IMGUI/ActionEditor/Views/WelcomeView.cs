using UnityEngine;

namespace NBC.ActionEditor
{
    public class WelcomeView : ViewBase
    {
        public override void OnDraw()
        {
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            if (Styles.Logo != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(Styles.Logo, GUILayout.Width(128), GUILayout.Height(128)); // 设置图片尺寸
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 30;
            titleStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label("行为时间轴编辑器", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("同一份 JSON，可在 GUILayout 与 UI Toolkit 界面编辑");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUI.backgroundColor = new Color(0.8f, 0.8f, 1, 1f);

            if (DrawCenteredButton("创建 JSON 资源"))
            {
                CreateAssetWindow.Show();
            }

            GUI.backgroundColor = Color.white;

            if (DrawCenteredButton("打开 JSON 资源"))
            {
                ObjectSelectorWindow.ShowObjectPicker<TextAsset>(null, ActionEditorHost.OnObjectPickerConfig, "Assets/", assetPathFilter: App.IsJsonDocumentPath);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Open Workbench 使用 UI Toolkit，数据与本窗口共用");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (DrawCenteredButton(Lan.Seeting))
            {
                PreferencesWindow.Show(new Rect(Styles.ScreenWidth - 5 - 400, 25, 400,
                    Styles.ScreenHeight - 70));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private bool DrawCenteredButton(string buttonText)
        {
            var ret = false;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(buttonText, GUILayout.Width(220), GUILayout.Height(40)))
            {
                ret = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return ret;
        }
    }
}