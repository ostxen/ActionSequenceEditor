using System;
using System.Linq;
using ActionSequenceEditor.Presentation.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Views.UIToolkit
{
    public sealed class SequenceCreationWindow : DialogWindowBase
    {
        private ISequenceDialogCatalog catalog;
        private Func<Type, string, bool> tryCreate;
        private Type[] types;
        public static void ShowFor(ISequenceDialogCatalog catalog, Func<Type, string, bool> tryCreate)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (tryCreate == null) throw new ArgumentNullException(nameof(tryCreate));
            var window = CreateInstance<SequenceCreationWindow>();
            window.catalog = catalog; window.tryCreate = tryCreate;
            window.minSize = new Vector2(440, 240); window.ShowUtility();
        }
        public void CreateGUI()
        {
            if (tryCreate == null) { LostBinding(); return; }
            Begin(EditorText.CreateSequence);
            rootVisualElement.Add(new HelpBox(EditorText.CreateSafety, HelpBoxMessageType.Info));
            types = catalog.CreationTypes();
            if (types.Length == 0) { rootVisualElement.Add(new HelpBox(EditorText.NoResourceTypes, HelpBoxMessageType.Warning)); return; }
            var names = types.Select(type => types.Count(other => SequenceCreationPrompt.TypeName(other) == SequenceCreationPrompt.TypeName(type)) > 1
                ? SequenceCreationPrompt.TypeName(type) + " (" + type.FullName + ")" : SequenceCreationPrompt.TypeName(type)).ToList();
            var choice = new DropdownField(EditorText.CreationTypeLabel, names, 0) { name = "document-type" };
            choice.tooltip = types[0].FullName;
            choice.RegisterValueChangedCallback(_ => choice.tooltip = types[choice.index].FullName);
            var name = new TextField(EditorText.CreationNameLabel) { value = "NewAction", name = "document-name" };
            rootVisualElement.Add(choice); rootVisualElement.Add(name);
            Finish(EditorText.CreateAssetConfirm, () =>
            {
                if (Submit(types[choice.index], name.value)) Close();
            });
        }
        internal bool Submit(Type type, string name, Func<string, string> choosePath = null, Func<Type, string, bool> confirm = null)
            => SequenceCreationPrompt.Submit(catalog, type, name, tryCreate, choosePath, confirm);
        private void OnDisable() { tryCreate = null; catalog = null; types = null; }
    }
}
