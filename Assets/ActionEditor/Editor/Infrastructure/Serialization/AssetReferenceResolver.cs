using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Serialization;
using UnityEditor;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Infrastructure.Serialization
{
    // 通过GUID及localFileId解析持久资源，不拥有或销毁引用目标。
    public sealed class AssetReferenceResolver : IAssetReferenceResolver
    {
        public AssetReference Identify(Object value, Type declaredType)
        {
            if (value == null || !declaredType.IsInstanceOfType(value) || !EditorUtility.IsPersistent(value)
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)
                || !AssetDatabase.GetAssetPath(value).StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidDataException("Non-persistent or out-of-project Unity reference.");
            return new AssetReference(guid, id);
        }
        public Object Resolve(AssetReference reference, Type declaredType)
        {
            var path = AssetDatabase.GUIDToAssetPath(reference.Guid);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidDataException("Missing project Unity reference.");
            var result = AssetDatabase.LoadAllAssetsAtPath(path).SingleOrDefault(value => value != null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)
                && guid == reference.Guid && id == reference.LocalFileId);
            if (result == null || !declaredType.IsInstanceOfType(result)) throw new InvalidDataException("Missing or incompatible Unity reference.");
            return result;
        }
    }
}
