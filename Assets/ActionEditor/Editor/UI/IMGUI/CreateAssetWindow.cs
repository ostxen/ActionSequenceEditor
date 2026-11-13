using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public class CreateAssetWindow : PopupWindowContent
    {
        private static Rect _myRect;
        private Type[] types = Array.Empty<Type>();
        private int selectedType;
        private string createName = "NewAction";
        private string error;
        private bool nativeOwner;

        public static void Show()
        {
            var rect = new Rect(20, 25, 440, 240);
            _myRect = rect;
            PopupWindow.Show(new Rect(rect.x, rect.y, 0, 0), new CreateAssetWindow { nativeOwner = EditorWindow.focusedWindow is NBC.ActionEditor.Workbench.UIToolkitActionEditorWindow });
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(_myRect.width, _myRect.height);
        }

        public override void OnOpen()
        {
            Prefs.InitializeAssetTypes();
            types = ReflectionTools.GetImplementationsOf(typeof(Asset)).Distinct()
                .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) != null
                    && !t.Assembly.GetName().Name.EndsWith(".Tests", StringComparison.Ordinal))
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
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
                types.Select(t => new GUIContent(types.Count(other => Prefs.GetAssetTypeName(other) == Prefs.GetAssetTypeName(t)) > 1
                    ? Prefs.GetAssetTypeName(t) + " (" + t.FullName + ")" : Prefs.GetAssetTypeName(t), t.FullName)).ToArray());
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
            if (!ActionEditorHost.ConfirmDocumentSwitch()) return;
            if (types.Length == 0) throw new InvalidOperationException(Lan.NoResourceTypes);
            var name = createName?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..")
                throw new ArgumentException(Lan.InvalidResourceName);
            var path = EditorUtility.SaveFilePanelInProject(Lan.CreateAsset, name, "json", Lan.NewPathHint);
            if (string.IsNullOrEmpty(path)) return;
            path = DocumentCreationService.ValidateNewPath(path);
            var type = types[selectedType];
            if (!EditorUtility.DisplayDialog(Lan.CreateAsset, string.Format(Lan.CreateConfirmation, Prefs.GetAssetTypeName(type), path), Lan.CreateAssetConfirm, Lan.TipsCancel)) return;
            CreateAndOpen(type, path, nativeOwner);
            editorWindow?.Close();
        }

        public static void CreateAndOpen(Type type, string path, bool nativeOwner)
        {
            var created = DocumentCreationService.Create(type, path);
            ActionEditorHost.OnObjectPickerConfig(created);
            if (!nativeOwner) ActionEditorWindow.OpenDirectorWindow();
            App.Refresh(); App.Repaint();
        }

    }
}