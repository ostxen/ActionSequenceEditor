using System;
using System.Collections.Generic;
using System.IO;

namespace NBC.ActionEditor.Serialization
{
    // 仅解析显式信任且已加载的精确类型身份，不加载外部程序集。
    public sealed class SequenceFormatContext
    {
        private readonly Dictionary<string, Type> types;
        private readonly Dictionary<Type, string> identities;
        public IAssetReferenceResolver References { get; }
        public SequenceFormatContext(IEnumerable<Type> trustedTypes, IAssetReferenceResolver references = null)
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
