using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Serialization
{
    // 定义格式转换能力，不承担文件提交或编辑会话职责。
    public interface IActionDocumentFormat
    {
        string Id { get; }
        string DisplayName { get; }
        string Extension { get; }
        bool CanRead { get; }
        bool CanWrite { get; }
        bool CanExport { get; }
        byte[] Serialize(ActionDocument document, DocumentFormatOptions options, DocumentFormatContext context);
        ActionDocument Deserialize(byte[] bytes, DocumentFormatContext context);
    }

    // 传递输出选项，不改变文档内容或格式能力。
    public readonly struct DocumentFormatOptions
    {
        public bool Compressed { get; }
        public DocumentFormatOptions(bool compressed) { Compressed = compressed; }
    }

    // 将借入的Unity资源与持久身份互换，不承担资源所有权。
    public interface IDocumentReferenceResolver
    {
        DocumentReference Identify(Object value, Type declaredType);
        Object Resolve(DocumentReference reference, Type declaredType);
    }

    // 用GUID与文件内标识区分主资源和子资源。
    public readonly struct DocumentReference
    {
        public string Guid { get; }
        public long LocalFileId { get; }
        public DocumentReference(string guid, long localFileId) { Guid = guid; LocalFileId = localFileId; }
    }

    // 仅解析显式信任且已加载的精确类型身份，不加载外部程序集。
    public sealed class DocumentFormatContext
    {
        private readonly Dictionary<string, Type> types;
        private readonly Dictionary<Type, string> identities;
        public IDocumentReferenceResolver References { get; }
        public DocumentFormatContext(IEnumerable<Type> trustedTypes, IDocumentReferenceResolver references = null)
        {
            References = references;
            types = new Dictionary<string, Type>(StringComparer.Ordinal);
            identities = new Dictionary<Type, string>();
            foreach (var type in trustedTypes ?? throw new ArgumentNullException(nameof(trustedTypes)))
            {
                if (type == null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters || !type.IsSerializable) continue;
                var id = Identity(type);
                if (types.TryGetValue(id, out var existing) && existing != type) throw new InvalidDataException("Conflicting trusted type: " + id);
                types[id] = type;
                identities[type] = id;
            }
        }
        public static string Identity(Type type) => type.FullName + ", " + type.Assembly.GetName().Name;
        public Type ResolveType(string identity, Type declaration, string path)
        {
            if (identity == null || !types.TryGetValue(identity, out var type) || !declaration.IsAssignableFrom(type))
                throw new InvalidDataException(path + ": unknown or incompatible type identity.");
            return type;
        }
        // 类型身份在受信任目录建立时计算一次；逐字段捕获不重复读取程序集元数据。
        internal string RequireIdentity(Type type, string path)
        {
            if (type == null || !identities.TryGetValue(type, out var identity))
                throw new InvalidDataException(path + ": unknown or incompatible type identity.");
            return identity;
        }
        public void RequireType(Type type, string path) => RequireIdentity(type, path);
    }
}
