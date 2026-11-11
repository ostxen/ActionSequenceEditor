using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Storage
{
    /// <summary>JSON file and Unity import boundary; the session owns saved baselines.</summary>
    public sealed class JsonDocumentStore
    {
        public string GetPath(TextAsset text) => text == null ? null : AssetDatabase.GetAssetPath(text);
        public byte[] ReadRevision(string path) => !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllBytes(path) : null;

        public (string Json, byte[] Revision) Commit(string path, Asset document, byte[] expectedCurrent)
        {
            path = ValidatePath(path);
            if (File.Exists(path) && expectedCurrent == null) throw new IOException("另存目标已存在，拒绝覆盖。");
            var serialized = JsonDocumentCodec.CreateSaveSnapshot(document);
            new ReliableFileCommit().Save(path, () => new UTF8Encoding(false).GetBytes(serialized), data =>
            {
                var decoded = JsonDocumentCodec.Decode(Encoding.UTF8.GetString(data));
                if (decoded.GetType() != document.GetType()) throw new InvalidDataException("文档类型读回不一致。");
                var differences = JsonDocumentCodec.ReadbackDifferences(serialized, decoded);
                if (differences.Length != 0) throw new InvalidDataException("JSON 读回内容不一致，拒绝提交。差异字段：" + string.Join(", ", System.Linq.Enumerable.Take(differences, 8)));
            }, expectedCurrent);
            return (serialized, File.ReadAllBytes(path));
        }

        public void Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(path) == null) throw new IOException("JSON 字节已提交，但导入失败，请检查目标，不要重复创建。");
        }

        public static string ValidatePath(string path)
        {
            path = path?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("请先选择 JSON 保存路径。");
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || path.Contains("..") || path.Contains(":") || !Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                throw new ArgumentException("文档必须保存在 Assets 下已有目录的 .json 文件中。");
            if (path.StartsWith("Assets/CombatEditor/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets/ScriptableObjects/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("原始 Combat/ScriptableObjects 资源不在编辑范围内。");
            return path;
        }
    }
}
