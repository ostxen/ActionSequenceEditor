using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ActionSequenceEditor.Serialization;

namespace ActionSequenceEditor.Infrastructure.Serialization
{
    // 校验格式注册，并按真实读写或导出能力提供目录。
    public sealed class SequenceFormatRegistry
    {
        private readonly ISequenceFormat[] formats;
        public IReadOnlyList<ISequenceFormat> Editable => Array.AsReadOnly(formats.Where(f => f.CanRead && f.CanWrite).ToArray());
        public IReadOnlyList<ISequenceFormat> Exportable => Array.AsReadOnly(formats.Where(f => f.CanExport && f.CanWrite).ToArray());
        public SequenceFormatRegistry(IEnumerable<ISequenceFormat> candidates)
        {
            var all = candidates?.ToArray() ?? throw new ArgumentNullException(nameof(candidates));
            if (all.Any(f => f == null || string.IsNullOrWhiteSpace(f.Id) || string.IsNullOrWhiteSpace(f.DisplayName)
                || string.IsNullOrWhiteSpace(f.Extension) || !f.Extension.StartsWith(".", StringComparison.Ordinal)))
                throw new InvalidDataException("Invalid document format registration.");
            foreach (var group in all.GroupBy(f => f.Id, StringComparer.Ordinal).Concat(all.GroupBy(f => f.Extension, StringComparer.OrdinalIgnoreCase)))
                if (group.Count() > 1) throw new InvalidDataException("Document format conflict: " + string.Join("; ", group.Select(f => f.GetType().AssemblyQualifiedName)));
            formats = all;
        }
        public ISequenceFormat ById(string id) => formats.SingleOrDefault(f => f.Id == id)
            ?? throw new InvalidDataException("Unregistered document format.");
        public ISequenceFormat EditablePath(string path) => Editable.SingleOrDefault(f => string.Equals(System.IO.Path.GetExtension(path), f.Extension, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("No editable format for file extension.");
    }
}
