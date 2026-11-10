using System;
using System.IO;

namespace NBC.ActionEditor.Storage
{
    public interface ICommitFiles
    {
        bool Exists(string path);
        void Write(string path, byte[] bytes);
        byte[] Read(string path);
        void Replace(string staged, string target);
        void Move(string staged, string target);
        void Delete(string path);
    }

    public sealed class CommitFiles : ICommitFiles
    {
        public bool Exists(string path) => File.Exists(path);
        public byte[] Read(string path) => File.ReadAllBytes(path);
        public void Write(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        public void Replace(string staged, string target) => File.Replace(staged, target, null);
        public void Move(string staged, string target) => File.Move(staged, target);
        public void Delete(string path) => File.Delete(path);
    }

    public sealed class ReliableFileCommit
    {
        private readonly ICommitFiles files;
        public ReliableFileCommit(ICommitFiles files = null) { this.files = files ?? new CommitFiles(); }

        public void Save(string target, Func<byte[]> serialize, Action<byte[]> validate, byte[] expectedCurrent = null)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Missing target.", nameof(target));
            var bytes = serialize();
            if (bytes == null || bytes.Length == 0) throw new InvalidDataException("Empty serialization is not a valid document.");
            validate(bytes);
            var exists = files.Exists(target);
            if (expectedCurrent != null && (!exists || !Equal(files.Read(target), expectedCurrent)))
                throw new IOException("Document changed on disk; refusing to overwrite external edits.");
            var staged = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                files.Write(staged, bytes);
                var written = files.Read(staged);
                if (!Equal(bytes, written)) throw new IOException("Staged document differs from serialized bytes.");
                validate(written);
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
