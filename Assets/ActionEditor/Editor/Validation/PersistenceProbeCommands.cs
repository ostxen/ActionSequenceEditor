using System;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Validation
{
    public static class PersistenceProbeCommands
    {
        public const string Root = "Assets/ActionEditorValidationA01";
        private const string DocumentPath = Root + "/Document.asset";
        private const string ResourcePath = Root + "/Resource.asset";

        [CliGroupCommand("actioneditor.validation", "A01 isolated persistence probe. Preview first; creates, edits, saves, reloads and deletes only its fixed new fixture folder.")]
        public static object PersistenceProbe(
            [CliArg("confirm", "Apply the reviewed fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Return exact fixture targets without writing.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Requires a ready EditMode Editor.");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta") || AssetDatabase.IsValidFolder(Root))
                throw new InvalidOperationException("Fixture target already exists; refusing to reuse or overwrite it.");
            if (dryRun)
                return new
                {
                    schema = 1,
                    root = Root,
                    targets = new[] { DocumentPath, ResourcePath },
                    operations = new[] { "create new isolated folder and two assets", "edit two polymorphic events", "Undo/Redo only the new document", "save only new document", "unload and reload document", "verify every fixture field and resource reference", "delete only newly created fixture folder in finally" },
                    touchesExistingAssets = false,
                    changesPackagesOrSettings = false,
                    cleansUpOnFailure = true
                };
            if (!confirm)
                throw new InvalidOperationException("Preview and explicit confirmation are required.");

            PersistenceProbeDocument document = null;
            Texture2D resource = null;
            var ownsFolder = false;
            try
            {
                var folderGuid = AssetDatabase.CreateFolder("Assets", "ActionEditorValidationA01");
                ownsFolder = !string.IsNullOrEmpty(folderGuid);
                Require(ownsFolder, "Could not create isolated fixture folder.");
                resource = new Texture2D(1, 1);
                AssetDatabase.CreateAsset(resource, ResourcePath);
                document = ScriptableObject.CreateInstance<PersistenceProbeDocument>();
                document.DocumentId = "a01-document";
                document.Events.Add(new PersistenceProbeSignal { Id = "signal", Start = 0.25, Resource = resource, Message = "before" });
                document.Events.Add(new PersistenceProbeInterval { Id = "interval", Start = 1.25, Duration = 0.5, Resource = resource, Amount = 7, Offset = new Vector3(1, 2, 3) });
                AssetDatabase.CreateAsset(document, DocumentPath);

                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("ActionEditor A01 isolated persistence probe");
                Undo.RegisterCompleteObjectUndo(document, "A01 edit");
                ((PersistenceProbeSignal)document.Events[0]).Message = "after";
                ((PersistenceProbeInterval)document.Events[1]).Amount = 11;
                EditorUtility.SetDirty(document);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(document, resource, "before", 7);
                Undo.PerformRedo();
                Check(document, resource, "after", 11);
                Undo.ClearUndo(document);
                Undo.IncrementCurrentGroup();

                AssetDatabase.SaveAssetIfDirty(document);
                Resources.UnloadAsset(document);
                document = AssetDatabase.LoadAssetAtPath<PersistenceProbeDocument>(DocumentPath);
                Check(document, resource, "after", 11);
                return new { status = "passed", schema = 1, root = Root, checks = new[] { "two polymorphic types", "time and parameters", "Unity resource references", "Undo", "Redo", "save/unload/reload" }, cleanup = "fixture folder removed before response" };
            }
            finally
            {
                if (document != null)
                {
                    Undo.ClearUndo(document);
                    if (!AssetDatabase.Contains(document)) UnityEngine.Object.DestroyImmediate(document);
                }
                if (resource != null && !AssetDatabase.Contains(resource)) UnityEngine.Object.DestroyImmediate(resource);
                if (ownsFolder && !AssetDatabase.DeleteAsset(Root))
                    throw new IOException("Fixture cleanup failed; inspect " + Root + " before any further operation.");
            }
        }

        private static void Check(PersistenceProbeDocument document, Texture2D resource, string message, int amount)
        {
            Require(document != null && document.Version == 1 && document.DocumentId == "a01-document", "Document metadata changed.");
            Require(document.Events.Count == 2, "Event count changed.");
            var signal = document.Events[0] as PersistenceProbeSignal;
            var interval = document.Events[1] as PersistenceProbeInterval;
            Require(signal != null && interval != null, "Polymorphic types were lost.");
            Require(signal.Id == "signal" && signal.Start == 0.25 && signal.Duration == 0 && signal.Message == message, "Signal fields changed.");
            Require(interval.Id == "interval" && interval.Start == 1.25 && interval.Duration == 0.5 && interval.Amount == amount && interval.Offset == new Vector3(1, 2, 3), "Interval fields changed.");
            Require(signal.Resource == resource && interval.Resource == resource && AssetDatabase.GetAssetPath(resource) == ResourcePath, "Resource reference changed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
