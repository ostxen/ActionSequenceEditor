using NBC.ActionEditor.Infrastructure.Documents;
using System;
using System.IO;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Extensibility;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Storage
{
    public interface IDocumentStore
    {
        ActionDocument Load(string path);
        void Save(string path, ActionDocument document, byte[] expectedCurrent);
        byte[] ReadRevision(string path);
    }

    public sealed class DocumentStore : IDocumentStore
    {
        private readonly EventRegistry registry;
        public DocumentStore(EventRegistry registry = null) { this.registry = registry ?? EventRegistry.CreateDefault(); }
        public ActionDocument Load(string path)
        {
            path = ValidatePath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Document not found.", path);
            var source = AssetDatabase.LoadAssetAtPath<ActionDocument>(path);
            if (source == null || SerializationUtility.HasManagedReferencesWithMissingTypes(source))
                throw new InvalidDataException("Invalid document or missing managed-reference types.");
            DocumentValidation.RequireValid(source);
            var copy = Object.Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }

        public byte[] ReadRevision(string path) => File.ReadAllBytes(ValidatePath(path));

        public void Save(string path, ActionDocument document, byte[] expectedCurrent)
        {
            path = ValidatePath(path);
            DocumentValidation.RequireValid(document);
            registry.RequireValid(document);
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(document))
                throw new InvalidDataException("Missing event types; saving would lose data.");
            var existed = File.Exists(path);
            if (existed && expectedCurrent == null) throw new IOException("Existing documents require a matching revision.");
            new AtomicFileCommit().Save(path, () => Encode(document), ValidateEncoded, expectedCurrent);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var saved = AssetDatabase.LoadAssetAtPath<ActionDocument>(path);
            if (saved == null) throw new IOException("Document bytes committed, but Unity import failed; inspect target before retrying.");
        }

        private static byte[] Encode(ActionDocument document)
        {
            var temporary = Path.Combine("Temp", "ActionEditor-" + Guid.NewGuid().ToString("N") + ".asset");
            var copy = Object.Instantiate(document);
            try
            {
                copy.hideFlags = HideFlags.None;
                InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { copy }, temporary, true);
                return File.ReadAllBytes(temporary);
            }
            finally
            {
                Object.DestroyImmediate(copy);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void ValidateEncoded(byte[] bytes)
        {
            var temporary = Path.Combine("Temp", "ActionEditor-check-" + Guid.NewGuid().ToString("N") + ".asset");
            Object[] loaded = null;
            try
            {
                File.WriteAllBytes(temporary, bytes);
                loaded = InternalEditorUtility.LoadSerializedFileAndForget(temporary);
                if (loaded.Length != 1 || loaded[0] is not ActionDocument document)
                    throw new InvalidDataException("Serialized document did not reload.");
                DocumentValidation.RequireValid(document);
                if (SerializationUtility.HasManagedReferencesWithMissingTypes(document)) throw new InvalidDataException("Unknown event type.");
            }
            finally
            {
                if (loaded != null) foreach (var item in loaded) if (item != null) Object.DestroyImmediate(item);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static string ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Missing document path.");
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                || path.Contains("..") || path.Contains(":") || !Directory.Exists(Path.GetDirectoryName(path)))
                throw new ArgumentException("Documents require an existing folder under Assets and an .asset extension.");
            if (path.StartsWith("Assets/CombatEditor/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets/ScriptableObjects/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Combat reference assets are outside this workbench's authoring scope.");
            return path;
        }
    }
}
