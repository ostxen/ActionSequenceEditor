using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Serialization;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Storage
{
    public sealed class DocumentFormatRegistry
    {
        private readonly IActionDocumentFormat[] formats;
        public IReadOnlyList<IActionDocumentFormat> Editable => Array.AsReadOnly(formats.Where(f => f.CanRead && f.CanWrite).ToArray());
        public IReadOnlyList<IActionDocumentFormat> Exportable => Array.AsReadOnly(formats.Where(f => f.CanExport && f.CanWrite).ToArray());
        public DocumentFormatRegistry(IEnumerable<IActionDocumentFormat> candidates)
        {
            var all = candidates?.ToArray() ?? throw new ArgumentNullException(nameof(candidates));
            if (all.Any(f => f == null || string.IsNullOrWhiteSpace(f.Id) || string.IsNullOrWhiteSpace(f.DisplayName)
                || string.IsNullOrWhiteSpace(f.Extension) || !f.Extension.StartsWith(".", StringComparison.Ordinal)))
                throw new InvalidDataException("Invalid document format registration.");
            foreach (var group in all.GroupBy(f => f.Id, StringComparer.Ordinal).Concat(all.GroupBy(f => f.Extension, StringComparer.OrdinalIgnoreCase)))
                if (group.Count() > 1) throw new InvalidDataException("Document format conflict: " + string.Join("; ", group.Select(f => f.GetType().AssemblyQualifiedName)));
            formats = all;
        }
        public IActionDocumentFormat ById(string id) => formats.SingleOrDefault(f => f.Id == id)
            ?? throw new InvalidDataException("Unregistered document format.");
        public IActionDocumentFormat EditablePath(string path) => Editable.SingleOrDefault(f => string.Equals(System.IO.Path.GetExtension(path), f.Extension, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("No editable format for file extension.");
    }

    public static class DocumentFormats
    {
        public static DocumentFormatRegistry Registry { get; } = new DocumentFormatRegistry(new IActionDocumentFormat[] { new ActionDocumentJsonFormat() });
        private static DocumentFormatContext production;
        public static DocumentFormatContext Context => production ??= CreateContext();
        public static DocumentFormatContext CreateContext(params Type[] explicitTestTypes)
        {
            var editorOnly = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Where(a => (a.flags & AssemblyFlags.EditorAssembly) != 0).Select(a => a.name), StringComparer.Ordinal);
            var trusted = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (assembly.IsDynamic || editorOnly.Contains(name) || name.EndsWith(".Tests", StringComparison.Ordinal)
                    || name.IndexOf(".Verification", StringComparison.Ordinal) >= 0 || name.EndsWith(".Editor", StringComparison.Ordinal)
                    || name.StartsWith("UnityEditor", StringComparison.Ordinal)) continue;
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException error) { types = error.Types.Where(t => t != null).ToArray(); }
                trusted.AddRange(types.Where(t => t.IsSerializable && !t.IsAbstract && !t.ContainsGenericParameters
                    && t.Namespace != null && !t.Namespace.StartsWith("System", StringComparison.Ordinal)
                    && !t.Namespace.StartsWith("Unity", StringComparison.Ordinal) && !typeof(Object).IsAssignableFrom(t)));
            }
            return new DocumentFormatContext(trusted.Concat(explicitTestTypes), new AssetReferenceResolver());
        }
    }

    public sealed class AssetReferenceResolver : IDocumentReferenceResolver
    {
        public DocumentReference Identify(Object value, Type declaredType)
        {
            if (value == null || !declaredType.IsInstanceOfType(value) || !EditorUtility.IsPersistent(value)
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)
                || !AssetDatabase.GetAssetPath(value).StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidDataException("Non-persistent or out-of-project Unity reference.");
            return new DocumentReference(guid, id);
        }
        public Object Resolve(DocumentReference reference, Type declaredType)
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
