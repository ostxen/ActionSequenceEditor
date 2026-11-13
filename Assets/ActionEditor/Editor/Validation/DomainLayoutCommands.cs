using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Pipeline.Commands;
using UnityEditor;

namespace NBC.ActionEditor.Validation
{
    // 按固定清单归位领域源码，保留资源身份并拒绝覆盖。
    public static class DomainLayoutCommands
    {
        private const string Root = "Assets/ActionEditor/";

        private sealed class Move
        {
            public readonly string Source;
            public readonly string Destination;
            public readonly string Guid;

            public Move(string source, string destination, string guid)
            {
                Source = Root + source;
                Destination = Root + destination;
                Guid = guid;
            }
        }

        private static readonly Move[] Moves =
        {
            new Move("Runtime/Assets/Asset.cs", "Runtime/Domain/ActionDocument.cs", "a3d1500c02fb459d85ab07dbe606726d"),
            new Move("Runtime/Assets/Group.cs", "Runtime/Domain/TimelineGroup.cs", "2a2593f3511d46ed951d5afa7c4e8a2f"),
            new Move("Runtime/Assets/Track.cs", "Runtime/Domain/TimelineTrack.cs", "70fe174422714ce69a4eda6ae2a2eaeb"),
            new Move("Runtime/Assets/Clip.cs", "Runtime/Domain/TimelineClip.cs", "b8f51f8184a541a0a08fda9e6aa68b9a"),
            new Move("Runtime/NBC.ActionEditor.asmdef", "Runtime/NBC.ActionEditor.Domain.asmdef", "7d31d11ba62f20b4d89380d87c02f0e4"),
            new Move("Runtime/Utility/CreateUtilities.cs", "Editor/Infrastructure/Assets/CreateUtilities.cs", "377575421f4b40f9a816d0a84cf22a36"),
            new Move("Runtime/Assets/InspectorPreviewAsset.cs", "Editor/Composition/InspectorPreviewAsset.cs", "0d4bd8fc775b40c5bd36f06d2efd0589"),
            new Move("Runtime/Serialization/FullSerializerExtensions.cs", "Runtime/Serialization/DocumentNumber.cs", "107863859ba94b9580581de9746adeef")
        };

        // 预览和应用使用同一份身份、字节及目标校验。
        [CliGroupCommand("actioneditor.validation", "Move the fixed domain layout manifest, preserve GUID and bytes, and regenerate projects. No overwrite, deletion or document writes.")]
        public static object ArrangeDomainLayout(
            [CliArg("confirm", "Apply the reviewed manifest.")] bool confirm = false,
            [CliArg("dry-run", "Inspect the exact fixed scope.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("需要空闲的编辑模式。");

            foreach (var move in Moves)
            {
                if (!File.Exists(move.Source) || !File.Exists(move.Source + ".meta")
                    || AssetDatabase.AssetPathToGUID(move.Source) != move.Guid)
                    throw new IOException("源身份不符：" + move.Source);
                if (File.Exists(move.Destination) || Directory.Exists(move.Destination) || File.Exists(move.Destination + ".meta"))
                    throw new IOException("目标已存在：" + move.Destination);
            }

            var folders = Moves.SelectMany(m => MissingFolders(Parent(m.Destination))).Distinct()
                .OrderBy(f => f.Count(c => c == '/')).ThenBy(f => f, StringComparer.Ordinal).ToArray();
            var before = Moves.Select(m => new
            {
                source = m.Source, destination = m.Destination, guid = m.Guid,
                sha256 = Hash(m.Source), metaSha256 = Hash(m.Source + ".meta")
            }).ToArray();
            if (dryRun) return new { moves = before, createFolders = folders, overwrite = false, deletes = false, touchesUserDocuments = false, regenerateProjects = true };
            if (!confirm) throw new InvalidOperationException("需要预览和真实审批。");

            foreach (var folder in folders)
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Parent(folder), Path.GetFileName(folder)))
                    || !AssetDatabase.IsValidFolder(folder))
                    throw new IOException("目录创建失败：" + folder);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var move in Moves)
                {
                    var error = AssetDatabase.ValidateMoveAsset(move.Source, move.Destination);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    error = AssetDatabase.MoveAsset(move.Source, move.Destination);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            // 移动完成后先核实身份和字节，再请求生成项目。
            foreach (var item in before)
                if (File.Exists(item.source) || File.Exists(item.source + ".meta")
                    || AssetDatabase.AssetPathToGUID(item.destination) != item.guid
                    || Hash(item.destination) != item.sha256 || Hash(item.destination + ".meta") != item.metaSha256)
                    throw new IOException("移动后校验失败：" + item.destination);
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            return new { status = "passed", count = Moves.Length, moves = before, createdFolders = folders, guidAndBytesPreserved = true, projectsRegenerated = true };
        }

        private static string Parent(string path) => Path.GetDirectoryName(path).Replace('\\', '/');

        // 只允许在已登记的资源目录下逐层建立缺失目录。
        private static string[] MissingFolders(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return Array.Empty<string>();
            if (File.Exists(folder) || Directory.Exists(folder) || File.Exists(folder + ".meta"))
                throw new IOException("目录未登记或冲突：" + folder);
            return MissingFolders(Parent(folder)).Concat(new[] { folder }).ToArray();
        }

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
