using System;
using System.Linq;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public sealed class DocumentExportWindow : EditorWindow
    {
        private int selected;
        private string error;
        public static void ShowForCurrent()
        {
            if (App.AssetData == null) throw new InvalidOperationException("没有打开的资源。");
            if (App.Session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再导出。");
            var window = CreateInstance<DocumentExportWindow>();
            window.titleContent = new GUIContent("导出"); window.minSize = new Vector2(360, 130); window.ShowUtility();
        }
        private void OnGUI()
        {
            var formats = DocumentFormats.Registry.Exportable;
            EditorGUILayout.LabelField("导出", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("导出独立副本，不改变当前来源或未保存的修改。仅创建新文件。", MessageType.Info);
            if (formats.Count == 0) { EditorGUILayout.HelpBox("没有已实现的导出格式。", MessageType.Warning); return; }
            selected = EditorGUILayout.Popup("文件格式", Mathf.Clamp(selected, 0, formats.Count - 1), formats.Select(f => f.DisplayName).ToArray());
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button("导出"))
            {
                try
                {
                    var format = formats[selected];
                    var target = EditorUtility.SaveFilePanelInProject("导出", (App.TextAsset != null ? App.TextAsset.name : "Action") + "-Export", format.Extension.TrimStart('.'), "选择新的独立文件，不覆盖源文件。");
                    if (ExportTo(format.Id, target)) Close();
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }
        public static bool ExportTo(string formatId, string target) => new DocumentExportService().Export(App.Session, formatId, target);
    }
}
