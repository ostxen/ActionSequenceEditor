using System;
using System.IO;
using System.Linq;
using System.Text;
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
            PopupWindow.Show(new Rect(rect.x, rect.y, 0, 0), new CreateAssetWindow { nativeOwner = EditorWindow.focusedWindow is NBC.ActionEditor.Workbench.JsonActionWorkbench });
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(_myRect.width, _myRect.height);
        }

        public override void OnOpen()
        {
            Prefs.InitializeAssetTypes();
            types = Prefs.AssetTypes.Values.Distinct()
                .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) != null
                    && !t.Assembly.GetName().Name.EndsWith(".Tests", StringComparison.Ordinal))
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField("创建 ActionEditor JSON 资源", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("新建 JSON 可由 GUILayout 与 UI Toolkit 两种界面共同编辑、保存。只创建新文件，不覆盖已有资源。", MessageType.Info);
            if (types.Length == 0)
            {
                EditorGUILayout.HelpBox("没有可创建的旧版 Asset 类型，请确认项目中的旧业务扩展已编译。", MessageType.Warning);
                return;
            }
            selectedType = EditorGUILayout.Popup("资源类型", selectedType,
                types.Select(t => Prefs.GetAssetTypeName(t) + " (" + t.FullName + ")").ToArray());
            createName = EditorGUILayout.TextField("文件名", createName);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button("选择保存位置并创建旧版 JSON"))
            {
                try { CreateConfirm(); }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }

        void CreateConfirm()
        {
            if (!ActionEditorHost.ConfirmDocumentSwitch()) return;
            if (types.Length == 0) throw new InvalidOperationException("没有可创建的旧版 Asset 类型。");
            var name = createName?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..")
                throw new ArgumentException("请输入有效文件名，不要包含路径。");
            var path = EditorUtility.SaveFilePanelInProject("创建旧版 ActionEditor 资源", name, "json", "选择新 JSON 的位置，不会覆盖已有文件。");
            if (string.IsNullOrEmpty(path)) return;
            path = DocumentCreationService.ValidateNewPath(path);
            var type = types[selectedType];
            if (!EditorUtility.DisplayDialog("确认创建旧版资源", "类型：" + type.FullName + "\n新文件：" + path
                + "\n仅创建此文件并在原版时间轴打开，不覆盖已有资源。", "创建并打开", "取消")) return;
            var created = DocumentCreationService.Create(type, path);
            ActionEditorHost.OnObjectPickerConfig(created);
            if (!nativeOwner) ActionEditorWindow.OpenDirectorWindow();
            App.Refresh(); App.Repaint();
            editorWindow?.Close();
        }

    }
}