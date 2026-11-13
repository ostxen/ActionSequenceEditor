using NBC.ActionEditor.Domain;
using System;
using System.Linq;
using NBC.ActionEditor.Composition;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 创建弹窗只收集原生控件输入，目录和提交由显式文档绑定提供。
    public class CreateAssetWindow : PopupWindowContent
    {
        private static Rect _myRect;
        private Type[] types = Array.Empty<Type>();
        private int selectedType;
        private string createName = "NewAction";
        private string error;
        private DocumentDialogComposition binding;

        public static void Show(ActionEditorContext context)
        {
            var rect = new Rect(20, 25, 440, 240);
            _myRect = rect;
            PopupWindow.Show(new Rect(rect.x, rect.y, 0, 0), new CreateAssetWindow { binding = new DocumentDialogComposition(context) });
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(_myRect.width, _myRect.height);
        }

        public override void OnOpen()
        {
            types = binding.CreationTypes();
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField(Lan.CreateAsset, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Lan.CreateSafety, MessageType.Info);
            if (types.Length == 0)
            {
                EditorGUILayout.HelpBox(Lan.NoResourceTypes, MessageType.Warning);
                return;
            }
            selectedType = EditorGUILayout.Popup(new GUIContent(Lan.CrateAssetType), selectedType,
                types.Select(t => new GUIContent(types.Count(other => binding.TypeName(other) == binding.TypeName(t)) > 1
                    ? binding.TypeName(t) + " (" + t.FullName + ")" : binding.TypeName(t), t.FullName)).ToArray());
            createName = EditorGUILayout.TextField(Lan.CrateAssetName, createName);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button(Lan.CreateAssetConfirm))
            {
                try { CreateConfirm(); }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }

        void CreateConfirm()
        {
            if (!binding.ConfirmSwitch()) return;
            if (types.Length == 0) throw new InvalidOperationException(Lan.NoResourceTypes);
            var name = binding.ValidateName(createName);
            var path = EditorUtility.SaveFilePanelInProject(Lan.CreateAsset, name, "json", Lan.NewPathHint);
            if (string.IsNullOrEmpty(path)) return;
            path = binding.ValidateNewPath(path);
            var type = types[selectedType];
            if (!EditorUtility.DisplayDialog(Lan.CreateAsset, string.Format(Lan.CreateConfirmation, binding.TypeName(type), path), Lan.CreateAssetConfirm, Lan.TipsCancel)) return;
            binding.CreateAndOpen(type, path);
            editorWindow?.Close();
        }

    }
}