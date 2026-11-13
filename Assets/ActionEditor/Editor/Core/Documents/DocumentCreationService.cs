using NBC.ActionEditor.Infrastructure.Documents;
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
            var store = new JsonDocumentRepository();
            path = store.ValidateNewPath(path);
            var result = App.Context.Lifecycle.Create(type, path);
            if (!result.Succeeded) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Exception).Throw();
            return store.Load(path);
        }
        public static string ValidateNewPath(string path) => new JsonDocumentRepository().ValidateNewPath(path);
    }
}
