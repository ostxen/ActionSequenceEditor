using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Serialization
{
    public interface IActionDocumentFormat
    {
        string Id { get; }
        string DisplayName { get; }
        string Extension { get; }
        bool CanRead { get; }
        bool CanWrite { get; }
        bool CanExport { get; }
        byte[] Serialize(Asset document, DocumentFormatOptions options, DocumentFormatContext context);
        Asset Deserialize(byte[] bytes, DocumentFormatContext context);
    }

    public readonly struct DocumentFormatOptions
    {
        public bool Compressed { get; }
        public DocumentFormatOptions(bool compressed) { Compressed = compressed; }
    }

    public interface IDocumentReferenceResolver
    {
        DocumentReference Identify(Object value, Type declaredType);
        Object Resolve(DocumentReference reference, Type declaredType);
    }

    public readonly struct DocumentReference
    {
        public string Guid { get; }
        public long LocalFileId { get; }
        public DocumentReference(string guid, long localFileId) { Guid = guid; LocalFileId = localFileId; }
    }

    /// <summary>Exact identities from explicitly trusted, already loaded types. Never loads an assembly.</summary>
    public sealed class DocumentFormatContext
    {
        private readonly Dictionary<string, Type> types;
        public IDocumentReferenceResolver References { get; }
        public DocumentFormatContext(IEnumerable<Type> trustedTypes, IDocumentReferenceResolver references = null)
        {
            References = references;
            types = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in trustedTypes ?? throw new ArgumentNullException(nameof(trustedTypes)))
            {
                if (type == null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters || !type.IsSerializable) continue;
                var id = Identity(type);
                if (types.TryGetValue(id, out var existing) && existing != type) throw new InvalidDataException("Conflicting trusted type: " + id);
                types[id] = type;
            }
        }
        public static string Identity(Type type) => type.FullName + ", " + type.Assembly.GetName().Name;
        public Type ResolveType(string identity, Type declaration, string path)
        {
            if (identity == null || !types.TryGetValue(identity, out var type) || !declaration.IsAssignableFrom(type))
                throw new InvalidDataException(path + ": unknown or incompatible type identity.");
            return type;
        }
        public void RequireType(Type type, string path) => ResolveType(Identity(type), type, path);
    }
}
