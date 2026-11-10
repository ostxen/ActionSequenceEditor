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
            if (!App.ConfirmDocumentSwitch()) return;
            if (types.Length == 0) throw new InvalidOperationException("没有可创建的旧版 Asset 类型。");
            var name = createName?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..")
                throw new ArgumentException("请输入有效文件名，不要包含路径。");
            var path = EditorUtility.SaveFilePanelInProject("创建旧版 ActionEditor 资源", name, "json", "选择新 JSON 的位置，不会覆盖已有文件。");
            if (string.IsNullOrEmpty(path)) return;
            path = ValidateNewPath(path);
            var type = types[selectedType];
            if (!EditorUtility.DisplayDialog("确认创建旧版资源", "类型：" + type.FullName + "\n新文件：" + path
                + "\n仅创建此文件并在原版时间轴打开，不覆盖已有资源。", "创建并打开", "取消")) return;
            CreateLegacyAsset(type, path, !nativeOwner);
            editorWindow?.Close();
        }

        public static TextAsset CreateLegacyAsset(Type type, string path, bool openOriginal = true)
        {
            path = ValidateNewPath(path);
            if (type == null || !typeof(Asset).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters
                || type.GetConstructor(Type.EmptyTypes) == null) throw new ArgumentException("无效的旧版 Asset 类型。");
            var asset = (Asset)Activator.CreateInstance(type);
            asset.Init(); asset.OnBeforeSerialize();
            var bytes = new UTF8Encoding(false).GetBytes(Json.Serialize(asset));
            void Validate(byte[] data)
            {
                var loaded = Json.Deserialize(typeof(Asset), Encoding.UTF8.GetString(data)) as Asset;
                if (loaded == null || loaded.GetType() != type) throw new InvalidDataException("旧版 JSON 读回校验失败，未创建资源。");
                loaded.Init();
            }
            Validate(bytes);
            var files = new CommitFiles();
            var staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                files.Write(staged, bytes);
                var readback = files.Read(staged);
                if (!bytes.SequenceEqual(readback)) throw new IOException("暂存文件与原内容不一致。");
                Validate(readback);
                ValidateNewPath(path);
                // Move is create-only: a concurrently created target is never replaced.
                files.Move(staged, path);
            }
            finally { if (files.Exists(staged)) files.Delete(staged); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (textAsset == null) throw new IOException("JSON 已创建，但 Unity 导入失败。请检查目标文件，不要重复创建。");
            App.OnObjectPickerConfig(textAsset);
            if (openOriginal)
                ActionEditorWindow.OpenDirectorWindow();
            App.Refresh(); App.Repaint();
            return textAsset;
        }

        private static string ValidateNewPath(string path)
        {
            path = path?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.Contains("..") || path.Contains(":")
                || !Directory.Exists(Path.GetDirectoryName(path))) throw new ArgumentException("请选择 Assets 下已有文件夹中的新 .json 文件。");
            if (path.StartsWith("Assets/CombatEditor/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/ScriptableObjects/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请选择原始 Combat 资源目录以外的位置。");
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new IOException("目标或对应 meta 已存在，拒绝覆盖。请使用新文件名。");
            return path;
        }
    }
}