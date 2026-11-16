using System;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Model;

namespace NBC.ActionEditor.Application.Sequences
{
    // 文档动作统一返回结果，明确取消、失败和已提交但导入失败。
    public readonly struct SequenceOperationResult
    {
        public bool Succeeded { get; }
        public bool Cancelled { get; }
        public bool BytesCommitted { get; }
        public string Error { get; }
        public Exception Exception { get; }
        private SequenceOperationResult(bool succeeded, bool cancelled, bool committed, Exception error)
        { Succeeded = succeeded; Cancelled = cancelled; BytesCommitted = committed; Exception = error; Error = error?.Message; }
        public static SequenceOperationResult Success(bool committed = false) => new SequenceOperationResult(true, false, committed, null);
        public static SequenceOperationResult Cancel() => new SequenceOperationResult(false, true, false, null);
        public static SequenceOperationResult Failure(Exception error, bool committed = false) => new SequenceOperationResult(false, false, committed, error);
    }

    // 协调文档生命周期，不选择路径、不读取资源或操作窗口。
    public sealed class SequenceLifecycle
    {
        private readonly SequenceSession session;
        private readonly ISequenceRepository repository;
        private readonly ISequenceExporter exporter;
        private readonly ISequenceFactory factory;
        public SequenceLifecycle(SequenceSession session, ISequenceRepository repository, ISequenceExporter exporter, ISequenceFactory factory)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        // 新文件的创建与导入先完成，再请求宿主显式打开；失败不切换当前会话。
        public SequenceOperationResult Create(Type type, string target)
        {
            if (string.IsNullOrEmpty(target)) return SequenceOperationResult.Cancel();
            var committed = false;
            try
            {
                if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再创建文档。");
                target = repository.ValidatePath(target);
                repository.Commit(target, factory.Create(type), null);
                committed = true;
                repository.Import(target);
                return SequenceOperationResult.Success(true);
            }
            catch (Exception error) { return SequenceOperationResult.Failure(error, committed); }
        }
        public SequenceOperationResult Open(string path)
        {
            if (string.IsNullOrEmpty(path)) return SequenceOperationResult.Cancel();
            try
            {
                RequireSwitch();
                var source = repository.Read(path);
                RequireSwitch();
                session.Open(source); return SequenceOperationResult.Success();
            }
            catch (Exception error) { return SequenceOperationResult.Failure(error); }
        }
        public SequenceOperationResult Open(SequenceOpenData source)
        {
            try { RequireSwitch(); session.Open(source); return SequenceOperationResult.Success(); }
            catch (Exception error) { return SequenceOperationResult.Failure(error); }
        }
        private void RequireSwitch()
        {
            if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再切换文档。");
        }
        public SequenceOperationResult Save(string target = null)
        {
            var committed = session.CommitVersion;
            try { session.Save(target); return SequenceOperationResult.Success(true); }
            catch (Exception error) { return SequenceOperationResult.Failure(error, session.CommitVersion != committed); }
        }
        public SequenceOperationResult SaveAs(string target)
        {
            if (string.IsNullOrEmpty(target)) return SequenceOperationResult.Cancel();
            try
            {
                target = repository.ValidatePath(target);
                if (string.Equals(target, session.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("另存为必须选择新目标。");
            }
            catch (Exception error) { return SequenceOperationResult.Failure(error); }
            return Save(target);
        }
        public SequenceOperationResult Export(string target, string formatId)
        {
            if (string.IsNullOrEmpty(target)) return SequenceOperationResult.Cancel();
            try
            {
                if (session.Sequence == null) throw new InvalidOperationException("没有打开的文档。");
                if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再导出。");
                exporter.Export(session.Sequence, session.Path, target, formatId);
                return SequenceOperationResult.Success(true);
            }
            catch (Exception error) { return SequenceOperationResult.Failure(error); }
        }
    }
}
