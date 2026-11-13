using NBC.ActionEditor.Domain;
using System;
using System.IO;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Storage;

namespace NBC.ActionEditor.Infrastructure.Documents
{
    // 导出隔离副本，可靠创建新目标，不修改共享会话。
    public sealed class DocumentExportService : IDocumentExporter
    {
        private readonly ICommitFiles files;
        private readonly DocumentFormatRegistry registry;
        public DocumentExportService(ICommitFiles files = null, DocumentFormatRegistry registry = null)
        { this.files = files ?? new CommitFiles(); this.registry = registry ?? DocumentFormats.Registry; }

        public void Export(ActionDocument document, string sourcePath, string target, string formatId)
        {
            var format = registry.ById(formatId);
            if (!format.CanExport || !format.CanWrite) throw new InvalidOperationException(Lan.UnsupportedExport);
            var store = new JsonDocumentRepository(files);
            target = JsonDocumentRepository.ValidatePath(target, format);
            if (!string.IsNullOrEmpty(sourcePath) && string.Equals(target, JsonDocumentRepository.ValidatePath(sourcePath), StringComparison.OrdinalIgnoreCase))
                throw new IOException(Lan.ExportSourceTarget);
            store.ValidateNewPath(target, format);
            var snapshot = DocumentSnapshots.Capture(document, true);
            var expected = DocumentValueConverter.Capture(snapshot, typeof(ActionDocument), DocumentFormats.Context).ContentKey();
            new AtomicFileCommit(files).Save(target, () => format.Serialize(snapshot, new DocumentFormatOptions(false), DocumentFormats.Context), bytes =>
            {
                if (format.CanRead)
                {
                    var loaded = format.Deserialize(bytes, DocumentFormats.Context);
                    if (loaded == null || loaded.GetType() != snapshot.GetType()
                        || DocumentValueConverter.Capture(loaded, typeof(ActionDocument), DocumentFormats.Context).ContentKey() != expected)
                        throw new InvalidDataException(Lan.ExportReadbackFailed);
                }
                store.ValidateNewPath(target, format);
            }, createOnly: true);
        }
    }
}
