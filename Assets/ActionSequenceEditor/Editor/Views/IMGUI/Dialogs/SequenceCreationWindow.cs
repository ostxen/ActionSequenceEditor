using ActionSequenceEditor.Model;
using System;
using System.Linq;
using ActionSequenceEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI
{
    // 创建弹窗只收集原生控件输入，目录和提交由显式文档绑定提供。
    public class SequenceCreationWindow : PopupWindowContent
    {
        private static Rect _myRect;
        private Type[] types = Array.Empty<Type>();
        private int selectedTypeIndex;
        private string requestedName = "NewAction";
        private string error;
        private ISequenceDialogCatalog catalog;
        private Func<Type, string, bool> tryCreate;

        public static void Show(ISequenceDialogCatalog catalog, Func<Type, string, bool> tryCreate)
        {
            var rect = new Rect(20, 25, 440, 240);
            _myRect = rect;
            PopupWindow.Show(new Rect(rect.x, rect.y, 0, 0), new SequenceCreationWindow
            { catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)), tryCreate = tryCreate ?? throw new ArgumentNullException(nameof(tryCreate)) });
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(_myRect.width, _myRect.height);
        }

        public override void OnOpen()
        {
            types = catalog.CreationTypes();
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField(EditorText.CreateSequence, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(EditorText.CreateSafety, MessageType.Info);
            if (types.Length == 0)
            {
                EditorGUILayout.HelpBox(EditorText.NoResourceTypes, MessageType.Warning);
                return;
            }
            selectedTypeIndex = EditorGUILayout.Popup(new GUIContent(EditorText.CreationTypeLabel), selectedTypeIndex,
                types.Select(t => new GUIContent(types.Count(other => TypeName(other) == TypeName(t)) > 1
                    ? TypeName(t) + " (" + t.FullName + ")" : TypeName(t), t.FullName)).ToArray());
            requestedName = EditorGUILayout.TextField(EditorText.CreationNameLabel, requestedName);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button(EditorText.CreateAssetConfirm))
            {
                try { SubmitCreation(); }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }

        void SubmitCreation()
        {
            if (types.Length == 0) throw new InvalidOperationException(EditorText.NoResourceTypes);
            var type = types[selectedTypeIndex];
            if (SequenceCreationPrompt.Submit(catalog, type, requestedName, tryCreate)) editorWindow?.Close();
        }

        public static string TypeName(Type type) => SequenceCreationPrompt.TypeName(type);
    }
}