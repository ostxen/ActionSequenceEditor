using System.IO;

namespace ActionSequenceEditor.Infrastructure.Sequences
{
    // 执行物理文件操作；暂存文件独占创建并刷新至磁盘。
    public sealed class PhysicalCommitFiles : ICommitFiles
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
}
