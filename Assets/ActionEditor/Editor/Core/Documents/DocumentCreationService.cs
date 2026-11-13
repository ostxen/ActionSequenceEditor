using NBC.ActionEditor.Domain;
using System;
using NBC.ActionEditor.Storage;
using UnityEngine;

namespace NBC.ActionEditor
{
    public static class DocumentCreationService
    {
        public static TextAsset Create(Type type, string path)
        {
            var store = new JsonDocumentStore();
            path = store.ValidateNewPath(path);
            if (type == null || !typeof(ActionDocument).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters
                || type.GetConstructor(Type.EmptyTypes) == null) throw new ArgumentException("无效的资源类型。");
            DocumentFormats.Context.RequireType(type, "$.document");
            var asset = (ActionDocument)Activator.CreateInstance(type);
            store.Commit(path, asset, null);
            store.Import(path);
            return store.Load(path);
        }
        public static string ValidateNewPath(string path) => new JsonDocumentStore().ValidateNewPath(path);
    }
}
