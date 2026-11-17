using System;
using System.Linq;
using ActionSequenceEditor.Model;
using ActionSequenceEditor.Presentation.Contracts;
using UnityEditor;

namespace ActionSequenceEditor.Views
{
    // 两种控件共用输入校验和确认顺序，不复制创建规则。
    internal static class SequenceCreationPrompt
    {
        public static string TypeName(Type type) => type == typeof(Samples.FeatureSample.FeatureSampleSequence) ? EditorText.FeatureSample
            : type.GetCustomAttributes(typeof(NameAttribute), false).OfType<NameAttribute>().FirstOrDefault()?.name ?? type.Name.SplitCamelCase();

        public static bool Submit(ISequenceDialogCatalog catalog, Type type, string requestedName, Func<Type, string, bool> tryCreate,
            Func<string, string> chooseNewPath = null, Func<Type, string, bool> confirmCreation = null)
        {
            string validatedName;
            try { validatedName = catalog.ValidateName(requestedName); }
            catch (ArgumentException) { throw new ArgumentException(EditorText.InvalidResourceName); }
            var path = chooseNewPath != null ? chooseNewPath(validatedName)
                : EditorUtility.SaveFilePanelInProject(EditorText.CreateSequence, validatedName, "json", EditorText.NewPathHint);
            if (string.IsNullOrEmpty(path)) return false;
            path = catalog.ValidateNewPath(path);
            var creationConfirmed = confirmCreation != null ? confirmCreation(type, path) : EditorUtility.DisplayDialog(EditorText.CreateSequence,
                string.Format(EditorText.CreateConfirmation, TypeName(type), path), EditorText.CreateAssetConfirm, EditorText.TipsCancel);
            return creationConfirmed && tryCreate(type, path);
        }
    }
}
