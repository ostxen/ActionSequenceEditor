using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI.Controls
{
    public static class EditorTools
    {
        public static void BoldSeparator()
        {
            var tex = Styles.WhiteTexture;
            var lastRect = GUILayoutUtility.GetLastRect();
            GUILayout.Space(14);
            GUI.color = new Color(0, 0, 0, 0.25f);
            GUI.DrawTexture(new Rect(0, lastRect.yMax + 6, Screen.width, 4), tex);
            GUI.DrawTexture(new Rect(0, lastRect.yMax + 6, Screen.width, 1), tex);
            GUI.DrawTexture(new Rect(0, lastRect.yMax + 9, Screen.width, 1), tex);
            GUI.color = Color.white;
        }

        public static string GUILayoutGetFilePath(string title, string des, string path, bool textReadonly = false)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = !textReadonly;
            var str = EditorGUILayout.TextField(new GUIContent(title, des), path);
            GUI.enabled = true;
            if (GUILayout.Button(Lan.Select, GUILayout.Width(40)))
            {
                var select_path =
                    EditorUtility.OpenFilePanel(Lan.SelectFile,
                        "Assets/", "");
                if (string.IsNullOrEmpty(select_path)) return str;
                int asset_start_index = select_path.IndexOf("Assets", StringComparison.Ordinal);
                if (asset_start_index > -1)
                {
                    select_path = select_path.Substring(asset_start_index,
                        select_path.Length - asset_start_index);
                }

                str = select_path;
            }

            GUILayout.EndHorizontal();
            return str;
        }

        public static string GUILayoutGetFolderPath(string title, string des, string path, bool textReadonly = false)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = !textReadonly;
            var str = EditorGUILayout.TextField(new GUIContent(title, des), path);
            GUI.enabled = true;
            if (GUILayout.Button(Lan.Select, GUILayout.Width(40)))
            {
                var select_path =
                    EditorUtility.OpenFolderPanel(Lan.SelectFolder,
                        "Assets/", "");
                if (string.IsNullOrEmpty(select_path)) return str;
                int asset_start_index = select_path.IndexOf("Assets", StringComparison.Ordinal);
                if (asset_start_index > -1)
                {
                    select_path = select_path.Substring(asset_start_index,
                        select_path.Length - asset_start_index);
                }

                str = select_path;
            }

            GUILayout.EndHorizontal();
            return str;
        }

        ///Generic Popup for selection of any element within a list
        public static T Popup<T>(string prefix, T selected, List<T> options, params GUILayoutOption[] GUIOptions)
        {
            return Popup<T>(null, prefix, selected, options, GUIOptions);
        }

        public static T Popup<T>(Rect? rect, string prefix, T selected, List<T> options,
            params GUILayoutOption[] GUIOptions)
        {
            var index = 0;
            if (options.Contains(selected))
            {
                index = options.IndexOf(selected) + 1;
            }

            var stringedOptions = new List<string>();
            if (options.Count == 0)
            {
                stringedOptions.Add("NONE AVAILABLE");
            }
            else
            {
                stringedOptions.Add("NONE");
                stringedOptions.AddRange(options.Select(o => o != null ? o.ToString() : "NONE"));
            }

            GUI.enabled = stringedOptions.Count > 1;
            if (!string.IsNullOrEmpty(prefix))
            {
                if (rect == null)
                {
                    index = EditorGUILayout.Popup(prefix, index, stringedOptions.ToArray(), GUIOptions);
                }
                else
                {
                    index = EditorGUI.Popup(rect.Value, prefix, index, stringedOptions.ToArray());
                }
            }
            else
            {
                if (rect == null)
                {
                    index = EditorGUILayout.Popup(index, stringedOptions.ToArray(), GUIOptions);
                }
                else
                {
                    index = EditorGUI.Popup(rect.Value, index, stringedOptions.ToArray());
                }
            }

            GUI.enabled = true;

            return index == 0 ? default(T) : options[index - 1];
        }

        /// <summary>
        /// 用于选择列表中任何元素而不添加NONE的通用弹出窗口
        /// </summary>
        public static T CleanPopup<T>(string prefix, T selected, List<T> options, params GUILayoutOption[] GUIOptions)
        {
            var index = -1;
            if (options.Contains(selected))
            {
                index = options.IndexOf(selected);
            }

            var stringedOptions = options.Select(o => o != null ? o.ToString() : "NONE");

            GUI.enabled = options.Count > 0;
            if (!string.IsNullOrEmpty(prefix))
                index = EditorGUILayout.Popup(prefix, index, stringedOptions.ToArray(), GUIOptions);
            else index = EditorGUILayout.Popup(index, stringedOptions.ToArray(), GUIOptions);
            GUI.enabled = true;

            return index == -1 ? default(T) : options[index];
        }

    }
}