using System;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Application.Documents
{
    // 文档动作统一返回结果，明确取消、失败和已提交但导入失败。
    public readonly struct DocumentOperationResult
    {
        public bool Succeeded { get; }
        public bool Cancelled { get; }
        public bool BytesCommitted { get; }
        public string Error { get; }
        public Exception Exception { get; }
        private DocumentOperationResult(bool succeeded, bool cancelled, bool committed, Exception error)
        { Succeeded = succeeded; Cancelled = cancelled; BytesCommitted = committed; Exception = error; Error = error?.Message; }
        public static DocumentOperationResult Success(bool committed = false) => new DocumentOperationResult(true, false, committed, null);
        public static DocumentOperationResult Cancel() => new DocumentOperationResult(false, true, false, null);
        public static DocumentOperationResult Failure(Exception error, bool committed = false) => new DocumentOperationResult(false, false, committed, error);
    }

    // 协调文档生命周期，不选择路径、不读取资源或操作窗口。
    public sealed class DocumentLifecycle
    {
        private readonly DocumentSession session;
        private readonly IDocumentRepository repository;
        private readonly IDocumentExporter exporter;
        private readonly IDocumentFactory factory;
        public DocumentLifecycle(DocumentSession session, IDocumentRepository repository, IDocumentExporter exporter, IDocumentFactory factory)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        // 新文件的创建与导入先完成，再请求宿主显式打开；失败不切换当前会话。
        public DocumentOperationResult Create(Type type, string target)
        {
            if (string.IsNullOrEmpty(target)) return DocumentOperationResult.Cancel();
            var committed = false;
            try
            {
                if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再创建文档。");
                target = repository.ValidatePath(target);
                repository.Commit(target, factory.Create(type), null);
                committed = true;
                repository.Import(target);
                return DocumentOperationResult.Success(true);
            }
            catch (Exception error) { return DocumentOperationResult.Failure(error, committed); }
        }
        public DocumentOperationResult Open(string path)
        {
            if (string.IsNullOrEmpty(path)) return DocumentOperationResult.Cancel();
            try { session.Open(repository.Read(path)); return DocumentOperationResult.Success(); }
            catch (Exception error) { return DocumentOperationResult.Failure(error); }
        }
        public DocumentOperationResult Open(DocumentOpenData source)
        {
            try { session.Open(source); return DocumentOperationResult.Success(); }
            catch (Exception error) { return DocumentOperationResult.Failure(error); }
        }
        public DocumentOperationResult Save(string target = null)
        {
            var committed = session.CommitVersion;
            try { session.Save(target); return DocumentOperationResult.Success(true); }
            catch (Exception error) { return DocumentOperationResult.Failure(error, session.CommitVersion != committed); }
        }
        public DocumentOperationResult SaveAs(string target)
        {
            if (string.IsNullOrEmpty(target)) return DocumentOperationResult.Cancel();
            try
            {
                target = repository.ValidatePath(target);
                if (string.Equals(target, session.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("另存为必须选择新目标。");
            }
            catch (Exception error) { return DocumentOperationResult.Failure(error); }
            return Save(target);
        }
        public DocumentOperationResult Export(string target, string formatId)
        {
            if (string.IsNullOrEmpty(target)) return DocumentOperationResult.Cancel();
            try
            {
                if (session.Document == null) throw new InvalidOperationException("没有打开的文档。");
                if (session.IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再导出。");
                exporter.Export(session.Document, session.Path, target, formatId);
                return DocumentOperationResult.Success(true);
            }
            catch (Exception error) { return DocumentOperationResult.Failure(error); }
        }
    }
}
