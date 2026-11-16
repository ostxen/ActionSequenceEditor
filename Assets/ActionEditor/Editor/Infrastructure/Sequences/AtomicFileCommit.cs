using System;
using System.IO;

namespace NBC.ActionEditor.Infrastructure.Sequences
{
    // 隔离原子提交所需的文件操作，允许定点注入失败。
    public interface ICommitFiles
    {
        bool Exists(string path);
        void Write(string path, byte[] bytes);
        byte[] Read(string path);
        void Replace(string staged, string target);
        void Move(string staged, string target);
        void Delete(string path);
    }

    // 校验暂存字节后一次提交，失败时只清理本次暂存文件。
    public sealed class AtomicFileCommit
    {
        private readonly ICommitFiles files;
        public AtomicFileCommit(ICommitFiles files = null) { this.files = files ?? new PhysicalCommitFiles(); }

        // 仅在目标版本仍匹配时替换；创建模式绝不覆盖已存在目标。
        public void Save(string target, Func<byte[]> serialize, Action<byte[]> validate, byte[] expectedCurrent = null, bool createOnly = false)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Missing target.", nameof(target));
            var bytes = serialize();
            if (bytes == null || bytes.Length == 0) throw new InvalidDataException("Empty serialization is not a valid document.");
            validate(bytes);
            var exists = files.Exists(target);
            if (createOnly && exists) throw new IOException("Create target already exists.");
            if (expectedCurrent != null && (!exists || !Equal(files.Read(target), expectedCurrent)))
                throw new IOException("Document changed on disk; refusing to overwrite external edits.");
            var staged = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                files.Write(staged, bytes);
                var written = files.Read(staged);
                if (!Equal(bytes, written)) throw new IOException("Staged document differs from serialized bytes.");
                validate(written);
                // 暂存期间可能发生外部写入，提交前再次核对同一版本。
                if (createOnly && files.Exists(target)) throw new IOException("Create target appeared during save.");
                if (expectedCurrent != null && (!files.Exists(target) || !Equal(files.Read(target), expectedCurrent)))
                    throw new IOException("Document changed during save.");
                if (exists)
                {
                    try { files.Replace(staged, target); }
                    catch (IOException e)
                    {
                        throw new IOException("原子替换文档失败（HRESULT 0x" + e.HResult.ToString("X8")
                            + "）。请检查文件占用或权限；提交结果需核对，未自动重试。", e);
                    }
                }
                else files.Move(staged, target);
            }
            finally
            {
                if (files.Exists(staged)) files.Delete(staged);
            }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
