using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Storage;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Validation
{
    public static class Rf18BoundaryCommands
    {
        private const string Source = "Assets/ActionEditor/Runtime/Utility/FullSerializerExtensions.cs";
        private const string Destination = "Assets/ActionEditor/Runtime/Serialization/FullSerializerExtensions.cs";
        private const string Guid = "107863859ba94b9580581de9746adeef";
        [CliGroupCommand("actioneditor.validation", "Fixed RF18 serializer source move, retaining GUID and bytes. No overwrite or deletion. Regenerate Unity solution.")]
        public static object Rf18Move([CliArg("confirm", "Apply fixed move.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            Ready();
            if (!File.Exists(Source) || AssetDatabase.AssetPathToGUID(Source) != Guid || File.Exists(Destination)
                || File.Exists(Destination + ".meta") || !AssetDatabase.IsValidFolder("Assets/ActionEditor/Runtime/Serialization"))
                throw new IOException("Fixed RF18 move state changed.");
            var bytes = Hash(Source); var meta = Hash(Source + ".meta");
            if (dryRun) return new { source = Source, destination = Destination, guid = Guid, sha256 = bytes, metaSha256 = meta, regenerateProjects = true };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var error = AssetDatabase.MoveAsset(Source, Destination);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            if (AssetDatabase.AssetPathToGUID(Destination) != Guid || Hash(Destination) != bytes || Hash(Destination + ".meta") != meta)
                throw new IOException("RF18 move identity mismatch; inspect, do not replay.");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            return new { status = "passed", guid = Guid, bytesPreserved = true, projectsRegenerated = true };
        }
        private const string Root = "Assets/ActionEditorValidationRF18";
        [Serializable] private sealed class ReferenceAsset : Asset { public Texture2D Main, Sub; }
        [CliGroupCommand("actioneditor.validation", "RF18 isolated main/subasset reference persistence. Only creates a new fixed fixture folder; always cleans owned fixtures. No current-session changes.")]
        public static object Rf18ReferenceProbe([CliArg("confirm", "Apply fixed isolated fixture.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            Ready();
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (dryRun) return new { root = Root, targets = new[] { Root + "/References.asset", Root + "/Document.json" },
                operations = new[] { "create main and subasset Texture2D", "write unique-format JSON", "unload and resolve GUID/localFileId", "reject missing/type-mismatched reference", "delete only owned new fixture folder" }, changesCurrentSession = false };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var owns = false;
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorValidationRF18"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                var main = new Texture2D(2, 2) { name = "Main" }; var sub = new Texture2D(1, 1) { name = "Sub" };
                AssetDatabase.CreateAsset(main, Root + "/References.asset"); AssetDatabase.AddObjectToAsset(sub, main);
                EditorUtility.SetDirty(main); EditorUtility.SetDirty(sub); AssetDatabase.SaveAssetIfDirty(main);
                AssetDatabase.ImportAsset(Root + "/References.asset", ImportAssetOptions.ForceSynchronousImport);
                var context = DocumentFormats.CreateContext(typeof(ReferenceAsset)); var format = new ActionDocumentJsonFormat();
                var bytes = format.Serialize(new ReferenceAsset { Main = main, Sub = sub }, default, context);
                File.WriteAllBytes(Root + "/Document.json", bytes);
                var resolver = new AssetReferenceResolver(); var mainId = resolver.Identify(main, typeof(Texture2D)); var subId = resolver.Identify(sub, typeof(Texture2D));
                if (mainId.LocalFileId == subId.LocalFileId || mainId.Guid != subId.Guid) throw new InvalidOperationException("Subasset identity not distinct.");
                Resources.UnloadAsset(sub); Resources.UnloadAsset(main);
                var restored = (ReferenceAsset)format.Deserialize(File.ReadAllBytes(Root + "/Document.json"), context);
                if (resolver.Identify(restored.Main, typeof(Texture2D)).LocalFileId != mainId.LocalFileId
                    || resolver.Identify(restored.Sub, typeof(Texture2D)).LocalFileId != subId.LocalFileId) throw new InvalidOperationException("Reference reload lost identity.");
                var refused = false;
                try { resolver.Resolve(mainId, typeof(AudioClip)); } catch (InvalidDataException) { refused = true; }
                if (!refused) throw new InvalidOperationException("Type mismatch accepted.");
                if (!AssetDatabase.DeleteAsset(Root + "/References.asset")) throw new IOException("Owned fixture deletion failed.");
                refused = false;
                try { format.Deserialize(bytes, context); } catch (InvalidDataException) { refused = true; }
                if (!refused) throw new InvalidOperationException("Missing reference accepted.");
                return new { status = "passed", checks = new[] { "main/subasset GUID+localFileId", "disk bytes reload after unload", "wrong type rejected", "missing reference rejected" } };
            }
            finally { if (owns && !AssetDatabase.DeleteAsset(Root)) throw new IOException("Owned fixture cleanup failed."); }
        }
        [CliGroupCommand("actioneditor.validation", "Regenerate current Unity IDE solution only; no source or settings changes.")]
        public static object SyncProjects([CliArg("confirm", "Regenerate generated project files.")] bool confirm = false,
            [CliArg("dry-run", "Preview only.")] bool dryRun = false)
        {
            Ready();
            if (dryRun) return new { operation = "Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll", scope = "generated solution and project files only" };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll(); return new { status = "passed" };
        }
        private static void Ready()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Requires ready EditMode.");
        }
        private static string Hash(string path)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    }
}
