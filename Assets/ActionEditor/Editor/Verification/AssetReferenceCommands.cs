using NBC.ActionEditor.Model;
using System;
using System.IO;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Infrastructure.Serialization;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Verification
{
    // 验证主资源与子资源的持久身份及严格解析边界。
    public static class AssetReferenceCommands
    {
        private const string Root = "Assets/ActionEditorReferenceVerification";
        [Serializable] private sealed class ReferenceAsset : ActionSequence { public Texture2D Main, Sub; }
        [CliGroupCommand("actioneditor.verification", "Isolated main/subasset reference persistence. Only creates a new fixed fixture folder; always cleans owned fixtures. No current-session changes.")]
        public static object ReferenceProbe([CliArg("confirm", "Apply fixed isolated fixture.")] bool confirm = false,
            [CliArg("dry-run", "Read-only preview.")] bool dryRun = false)
        {
            VerificationGuard.RequireReady();
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture exists, refusing overwrite.");
            if (dryRun) return new { root = Root, targets = new[] { Root + "/References.asset", Root + "/Document.json" },
                operations = new[] { "create main and subasset Texture2D", "write unique-format JSON", "unload and resolve GUID/localFileId", "reject missing/type-mismatched reference", "delete only owned new fixture folder" }, changesCurrentSession = false };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var owns = false;
            try
            {
                owns = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorReferenceVerification"));
                if (!owns) throw new IOException("Fixture folder creation failed.");
                var main = new Texture2D(2, 2) { name = "Main" }; var sub = new Texture2D(1, 1) { name = "Sub" };
                AssetDatabase.CreateAsset(main, Root + "/References.asset"); AssetDatabase.AddObjectToAsset(sub, main);
                EditorUtility.SetDirty(main); EditorUtility.SetDirty(sub); AssetDatabase.SaveAssetIfDirty(main);
                AssetDatabase.ImportAsset(Root + "/References.asset", ImportAssetOptions.ForceSynchronousImport);
                var context = SequenceFormats.CreateContext(typeof(ReferenceAsset)); var format = new SequenceJsonFormat();
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
    }
}
