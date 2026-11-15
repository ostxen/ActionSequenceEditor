using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI
{
    // 欢迎页只呈现文档意图，由所属窗口显式持有。
    public sealed class WelcomeView
    {
        private readonly Presentation.Presenters.DocumentPresenter documents;
        private readonly System.Action<Rect> showSettings;
        public WelcomeView(Presentation.Presenters.DocumentPresenter documents, System.Action<Rect> showSettings)
        { this.documents = documents; this.showSettings = showSettings; }
        public void OnGUI()
        {
            var background = GUI.backgroundColor;
            try { Draw(); }
            finally { GUI.backgroundColor = background; }
        }
        private void Draw()
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
            GUILayout.Label(Lan.Title, titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(Lan.WelcomeHint);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUI.backgroundColor = new Color(0.8f, 0.8f, 1, 1f);

            if (DrawCenteredButton(Lan.CreateAsset))
            {
                documents.Invoke(Presentation.Contracts.DocumentIntent.Create);
            }

            GUI.backgroundColor = Color.white;

            if (DrawCenteredButton(Lan.SelectAsset))
            {
                documents.Invoke(Presentation.Contracts.DocumentIntent.Open);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(Lan.WelcomeHelp);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (DrawCenteredButton(Lan.Seeting))
            {
                showSettings(new Rect(Styles.ScreenWidth - 5 - 400, 25, 400,
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