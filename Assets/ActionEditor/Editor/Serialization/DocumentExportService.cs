using NBC.ActionEditor.Domain;
using System;
using System.IO;
using NBC.ActionEditor.Serialization;

namespace NBC.ActionEditor.Storage
{
    public sealed class DocumentExportService
    {
        private readonly ICommitFiles files;
        private readonly DocumentFormatRegistry registry;
        public DocumentExportService(ICommitFiles files = null, DocumentFormatRegistry registry = null)
        { this.files = files ?? new CommitFiles(); this.registry = registry ?? DocumentFormats.Registry; }

        public bool Export(JsonDocumentSession session, string formatId, string target)
        {
            if (session == null || session.Document == null) throw new InvalidOperationException(Lan.NoDocument);
            if (session.IsEditing) throw new InvalidOperationException(Lan.FinishEditBeforeExport);
            var format = registry.ById(formatId);
            if (!format.CanExport || !format.CanWrite) throw new InvalidOperationException(Lan.UnsupportedExport);
            if (string.IsNullOrEmpty(target)) return false;
            var store = new JsonDocumentStore(files);
            target = JsonDocumentStore.ValidatePath(target, format);
            if (!string.IsNullOrEmpty(session.Path) && string.Equals(target, JsonDocumentStore.ValidatePath(session.Path), StringComparison.OrdinalIgnoreCase))
                throw new IOException(Lan.ExportSourceTarget);
            store.ValidateNewPath(target, format);
            var snapshot = DocumentSnapshots.Capture(session.Document, true);
            var expected = DocumentValueConverter.Capture(snapshot, typeof(ActionDocument), DocumentFormats.Context).ContentKey();
            new ReliableFileCommit(files).Save(target, () => format.Serialize(snapshot, new DocumentFormatOptions(false), DocumentFormats.Context), bytes =>
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
            return true;
        }
    }
}
