using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI.Controls
{
    // 原生偏好与Inspector的小型布局控件，保留调用者GUI作用域。
    public static class EditorGUIControls
    {
        public static void BoldSeparator()
        {
            var texture = Styles.WhiteTexture;
            var lastRect = GUILayoutUtility.GetLastRect();
            GUILayout.Space(14);
            var color = GUI.color;
            try
            {
                GUI.color = new Color(0, 0, 0, 0.25f);
                GUI.DrawTexture(new Rect(0, lastRect.yMax + 6, Screen.width, 4), texture);
                GUI.DrawTexture(new Rect(0, lastRect.yMax + 6, Screen.width, 1), texture);
                GUI.DrawTexture(new Rect(0, lastRect.yMax + 9, Screen.width, 1), texture);
            }
            finally { GUI.color = color; }
        }

        // 取消系统对话框也结束水平布局；只读文本不会重新启用外层禁用控件。
        public static string GUILayoutGetFolderPath(string title, string description, string path, bool textReadonly = false)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string value;
                using (new EditorGUI.DisabledScope(textReadonly))
                    value = EditorGUILayout.TextField(new GUIContent(title, description), path);
                if (!GUILayout.Button(EditorText.Select, GUILayout.Width(40))) return value;
                var selected = EditorUtility.OpenFolderPanel(EditorText.SelectFolder, "Assets/", "");
                if (string.IsNullOrEmpty(selected)) return value;
                var assetIndex = selected.IndexOf("Assets", StringComparison.Ordinal);
                return assetIndex >= 0 ? selected.Substring(assetIndex) : selected;
            }
        }

        // 选项不添加空值，未知当前值维持未选择状态。
        public static T CleanPopup<T>(string prefix, T selected, List<T> options, params GUILayoutOption[] guiOptions)
        {
            var index = options.IndexOf(selected);
            var labels = options.Select(option => option != null ? option.ToString() : "NONE").ToArray();
            using (new EditorGUI.DisabledScope(options.Count == 0))
                index = string.IsNullOrEmpty(prefix)
                    ? EditorGUILayout.Popup(index, labels, guiOptions)
                    : EditorGUILayout.Popup(prefix, index, labels, guiOptions);
            return index < 0 ? default : options[index];
        }
    }
}