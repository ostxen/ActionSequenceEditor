using System;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 导出窗口保留显式文档绑定，不在点击时重新取得全局会话。
    public sealed class DocumentExportWindow : EditorWindow
    {
        private int selected;
        private string error;
        private IDocumentDialogCatalog binding;
        private Func<string> documentName;
        private Func<string, string, bool> export;
        public static void ShowFor(IDocumentDialogCatalog catalog, Func<string> documentName, Func<string, string, bool> export)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var window = CreateInstance<DocumentExportWindow>();
            window.binding = catalog;
            window.documentName = documentName ?? throw new ArgumentNullException(nameof(documentName));
            window.export = export ?? throw new ArgumentNullException(nameof(export));
            window.titleContent = new GUIContent(Lan.Export); window.minSize = new Vector2(360, 130); window.ShowUtility();
        }
        private void OnGUI()
        {
            if (export == null) { EditorGUILayout.HelpBox(Lan.NotSelectAsset, MessageType.Info); return; }
            var formats = binding.ExportIds();
            EditorGUILayout.LabelField(Lan.Export, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Lan.ExportHint, MessageType.Info);
            if (formats.Length == 0) { EditorGUILayout.HelpBox(Lan.NoExportFormats, MessageType.Warning); return; }
            selected = EditorGUILayout.Popup(Lan.FileFormat, Mathf.Clamp(selected, 0, formats.Length - 1), binding.ExportLabels());
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button(Lan.Export))
            {
                try
                {
                    var format = formats[selected];
                    var target = EditorUtility.SaveFilePanelInProject(Lan.Export, documentName() + "-Export", binding.ExportExtension(format), Lan.NewPathHint);
                    if (ExportTo(format, target)) Close();
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }
        public bool ExportTo(string formatId, string target)
        {
            return export(target, formatId);
        }
    }
}
