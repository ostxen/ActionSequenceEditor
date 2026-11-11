using System;
using System.IO;
using System.Linq;
using System.Text;
using NBC.ActionEditor.Serialization;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Storage
{
    /// <summary>One path, byte commit and import boundary for editable documents.</summary>
    public sealed class JsonDocumentStore
    {
        private readonly ICommitFiles files;
        private readonly Action<string> import;
        public JsonDocumentStore(ICommitFiles files = null, Action<string> import = null)
        {
            this.files = files ?? new CommitFiles();
            this.import = import ?? (path => AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate));
        }
        public string GetPath(TextAsset text)
        {
            var path = text == null ? null : AssetDatabase.GetAssetPath(text);
            return string.IsNullOrEmpty(path) ? null : ValidatePath(path);
        }
        public TextAsset Load(string path) => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        public byte[] ReadRevision(string path) => !string.IsNullOrEmpty(path) && files.Exists(path) ? files.Read(path) : null;

        public (string Json, byte[] Revision) Commit(string path, Asset document, byte[] expectedCurrent)
        {
            path = ValidatePath(path);
            if (expectedCurrent == null) ValidateNewPath(path);
            var serialized = JsonDocumentCodec.CreateSaveSnapshot(document);
            new ReliableFileCommit(files).Save(path, () => new UTF8Encoding(false).GetBytes(serialized), data =>
            {
                var decoded = JsonDocumentCodec.Decode(new UTF8Encoding(false, true).GetString(data));
                if (decoded.GetType() != document.GetType()) throw new InvalidDataException("文档类型读回不一致。");
                var differences = JsonDocumentCodec.ReadbackDifferences(serialized, decoded);
                if (differences.Length != 0) throw new InvalidDataException("JSON 读回内容不一致，拒绝提交。差异字段：" + string.Join(", ", differences.Take(8)));
                if (expectedCurrent == null) ValidateNewPath(path);
            }, expectedCurrent, createOnly: expectedCurrent == null);
            // The exact bytes just committed are the revision. Do not read a later external edit as our baseline.
            return (serialized, new UTF8Encoding(false).GetBytes(serialized));
        }
        public void Import(string path)
        {
            try
            {
                import(path);
                if (Load(path) == null) throw new IOException("Unity 未返回文档资源。");
            }
            catch (Exception error)
            { throw new IOException("文档字节已提交，但 Unity 导入失败。请检查目标，不要重复创建。", error); }
        }
        public string ValidateNewPath(string path, IActionDocumentFormat format = null)
        {
            path = ValidatePath(path, format);
            if (files.Exists(path) || Directory.Exists(path) || files.Exists(path + ".meta"))
                throw new IOException("目标或对应 meta 已存在，拒绝覆盖。请使用新文件名。");
            return path;
        }
        public static string ValidatePath(string path, IActionDocumentFormat format = null)
        {
            path = path?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("请先选择保存路径。");
            var parts = path.Split('/');
            if (parts.Length < 2 || parts[0] != "Assets" || parts.Any(p => p.Length == 0 || p == ".." || p.EndsWith(".", StringComparison.Ordinal) && p != "."
                || p.EndsWith(" ", StringComparison.Ordinal) || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException("文档必须保存在 Assets 下已有目录中。");
            path = string.Join("/", parts.Where(p => p != "."));
            if (format == null) DocumentFormats.Registry.EditablePath(path);
            else if (!string.Equals(Path.GetExtension(path), format.Extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("目标扩展名与文件格式不符。");
            if (!Directory.Exists(Path.GetDirectoryName(path))) throw new ArgumentException("请选择已有目录。");
            if (path.StartsWith("Assets/CombatEditor/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets/ScriptableObjects/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("原始 Combat/ScriptableObjects 资源不在编辑范围内。");
            for (var directory = Path.GetDirectoryName(path); !string.IsNullOrEmpty(directory); directory = Path.GetDirectoryName(directory))
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("不允许链接目录作为文档目标。");
            return path;
        }
    }
}
