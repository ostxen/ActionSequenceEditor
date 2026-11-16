using NBC.ActionEditor.Model;
using System;
using System.IO;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Infrastructure.Serialization;

namespace NBC.ActionEditor.Infrastructure.Sequences
{
    // 导出隔离副本，可靠创建新目标，不修改共享会话。
    public sealed class SequenceExportService : ISequenceExporter
    {
        private readonly ICommitFiles files;
        private readonly SequenceFormatRegistry registry;
        public SequenceExportService(ICommitFiles files = null, SequenceFormatRegistry registry = null)
        { this.files = files ?? new PhysicalCommitFiles(); this.registry = registry ?? SequenceFormats.Registry; }

        public void Export(ActionSequence document, string sourcePath, string target, string formatId)
        {
            var format = registry.ById(formatId);
            if (!format.CanExport || !format.CanWrite) throw new InvalidOperationException(EditorText.UnsupportedExport);
            var store = new SequenceFileRepository(files);
            target = SequenceFileRepository.ValidatePath(target, format);
            if (!string.IsNullOrEmpty(sourcePath) && string.Equals(target, SequenceFileRepository.ValidatePath(sourcePath), StringComparison.OrdinalIgnoreCase))
                throw new IOException(EditorText.ExportSourceTarget);
            store.ValidateNewPath(target, format);
            var snapshot = SequenceCopy.Capture(document, true);
            var expected = JsonValueConverter.Capture(snapshot, typeof(ActionSequence), SequenceFormats.Context).ContentKey();
            new AtomicFileCommit(files).Save(target, () => format.Serialize(snapshot, new SequenceFormatOptions(false), SequenceFormats.Context), bytes =>
            {
                if (format.CanRead)
                {
                    var loaded = format.Deserialize(bytes, SequenceFormats.Context);
                    if (loaded == null || loaded.GetType() != snapshot.GetType()
                        || JsonValueConverter.Capture(loaded, typeof(ActionSequence), SequenceFormats.Context).ContentKey() != expected)
                        throw new InvalidDataException(EditorText.ExportReadbackFailed);
                }
                store.ValidateNewPath(target, format);
            }, createOnly: true);
        }
    }
}
