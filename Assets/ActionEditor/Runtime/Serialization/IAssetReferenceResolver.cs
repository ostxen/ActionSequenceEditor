using System;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Serialization
{
    // 将借入的Unity资源与持久身份互换，不承担资源所有权。
    public interface IAssetReferenceResolver
    {
        AssetReference Identify(Object value, Type declaredType);
        Object Resolve(AssetReference reference, Type declaredType);
    }

    // 用GUID与文件内标识区分主资源和子资源。
    public readonly struct AssetReference
    {
        public string Guid { get; }
        public long LocalFileId { get; }
        public AssetReference(string guid, long localFileId) { Guid = guid; LocalFileId = localFileId; }
    }
}
