using System;
using System.Linq;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Views.UIToolkit
{
    public sealed class SequenceExportWindow : DialogWindowBase
    {
        private ISequenceDialogCatalog catalog;
        private Func<string> getSourceName;
        private Func<string, string, bool> tryExport;
        public static void ShowFor(ISequenceDialogCatalog catalog, Func<string> getSourceName, Func<string, string, bool> tryExport)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (getSourceName == null) throw new ArgumentNullException(nameof(getSourceName));
            if (tryExport == null) throw new ArgumentNullException(nameof(tryExport));
            var window = CreateInstance<SequenceExportWindow>(); window.catalog = catalog; window.getSourceName = getSourceName; window.tryExport = tryExport;
            window.minSize = new Vector2(360, 160); window.ShowUtility();
        }
        public void CreateGUI()
        {
            if (tryExport == null) { LostBinding(); return; }
            Begin(EditorText.Export);
            rootVisualElement.Add(new HelpBox(EditorText.ExportHint, HelpBoxMessageType.Info));
            var formatIds = catalog.ExportIds();
            if (formatIds.Length == 0) { rootVisualElement.Add(new HelpBox(EditorText.NoExportFormats, HelpBoxMessageType.Warning)); return; }
            var choice = new DropdownField(EditorText.FileFormat, catalog.ExportLabels().ToList(), 0) { name = "export-format" };
            rootVisualElement.Add(choice);
            Finish(EditorText.Export, () =>
            {
                var formatId = formatIds[choice.index];
                var targetPath = EditorUtility.SaveFilePanelInProject(EditorText.Export, getSourceName() + "-Export", catalog.ExportExtension(formatId), EditorText.NewPathHint);
                if (ExportTo(formatId, targetPath)) Close();
            });
        }
        public bool ExportTo(string formatId, string targetPath) => tryExport(targetPath, formatId);
        private void OnDisable() { tryExport = null; getSourceName = null; catalog = null; }
    }
}
