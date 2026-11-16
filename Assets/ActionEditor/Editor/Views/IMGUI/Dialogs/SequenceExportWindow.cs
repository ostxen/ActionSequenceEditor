using System;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 导出窗口保留显式文档绑定，不在点击时重新取得全局会话。
    public sealed class SequenceExportWindow : EditorWindow
    {
        private int selectedFormatIndex;
        private string error;
        private ISequenceDialogCatalog catalog;
        private Func<string> getSourceName;
        private Func<string, string, bool> tryExport;
        public static void ShowFor(ISequenceDialogCatalog catalog, Func<string> getSourceName, Func<string, string, bool> tryExport)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var window = CreateInstance<SequenceExportWindow>();
            window.catalog = catalog;
            window.getSourceName = getSourceName ?? throw new ArgumentNullException(nameof(getSourceName));
            window.tryExport = tryExport ?? throw new ArgumentNullException(nameof(tryExport));
            window.titleContent = new GUIContent(EditorText.Export); window.minSize = new Vector2(360, 130); window.ShowUtility();
        }
        private void OnGUI()
        {
            if (tryExport == null) { EditorGUILayout.HelpBox(EditorText.NoSelection, MessageType.Info); return; }
            var formatIds = catalog.ExportIds();
            EditorGUILayout.LabelField(EditorText.Export, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(EditorText.ExportHint, MessageType.Info);
            if (formatIds.Length == 0) { EditorGUILayout.HelpBox(EditorText.NoExportFormats, MessageType.Warning); return; }
            selectedFormatIndex = EditorGUILayout.Popup(EditorText.FileFormat, Mathf.Clamp(selectedFormatIndex, 0, formatIds.Length - 1), catalog.ExportLabels());
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button(EditorText.Export))
            {
                try
                {
                    var formatId = formatIds[selectedFormatIndex];
                    var targetPath = EditorUtility.SaveFilePanelInProject(EditorText.Export, getSourceName() + "-Export", catalog.ExportExtension(formatId), EditorText.NewPathHint);
                    if (ExportTo(formatId, targetPath)) Close();
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }
        public bool ExportTo(string formatId, string targetPath)
        {
            return tryExport(targetPath, formatId);
        }
    }
}
