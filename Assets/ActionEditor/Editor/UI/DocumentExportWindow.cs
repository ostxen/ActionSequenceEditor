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
            if (App.AssetData == null) throw new InvalidOperationException(Lan.NoDocument);
            if (App.Session.IsEditing) throw new InvalidOperationException(Lan.FinishEditBeforeExport);
            var window = CreateInstance<DocumentExportWindow>();
            window.titleContent = new GUIContent(Lan.Export); window.minSize = new Vector2(360, 130); window.ShowUtility();
        }
        private void OnGUI()
        {
            var formats = DocumentFormats.Registry.Exportable;
            EditorGUILayout.LabelField(Lan.Export, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Lan.ExportHint, MessageType.Info);
            if (formats.Count == 0) { EditorGUILayout.HelpBox(Lan.NoExportFormats, MessageType.Warning); return; }
            selected = EditorGUILayout.Popup(Lan.FileFormat, Mathf.Clamp(selected, 0, formats.Count - 1), formats.Select(f => f.DisplayName).ToArray());
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button(Lan.Export))
            {
                try
                {
                    var format = formats[selected];
                    var target = EditorUtility.SaveFilePanelInProject(Lan.Export, (App.TextAsset != null ? App.TextAsset.name : "Action") + "-Export", format.Extension.TrimStart('.'), Lan.NewPathHint);
                    if (ExportTo(format.Id, target)) Close();
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }
        public static bool ExportTo(string formatId, string target)
        {
            var result = App.Context.Lifecycle.Export(target, formatId);
            if (!result.Succeeded && !result.Cancelled) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Exception).Throw();
            return result.Succeeded;
        }
    }
}
