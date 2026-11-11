using System;
using System.IO;
using System.Linq;
using System.Text;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public static class DocumentCreationService
    {
        public static TextAsset Create(Type type, string path)
        {
            path = ValidateNewPath(path);
            if (type == null || !typeof(Asset).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters
                || type.GetConstructor(Type.EmptyTypes) == null) throw new ArgumentException("无效的旧版 Asset 类型。");
            var asset = (Asset)Activator.CreateInstance(type);
            asset.Init();
            var bytes = new UTF8Encoding(false).GetBytes(JsonDocumentCodec.CreateSaveSnapshot(asset));
            void Validate(byte[] data)
            {
                var loaded = JsonDocumentCodec.Decode(Encoding.UTF8.GetString(data));
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
            return textAsset;
        }

        public static string ValidateNewPath(string path)
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
