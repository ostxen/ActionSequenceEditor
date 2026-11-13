using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Pipeline.Commands;
using UnityEditor;

namespace NBC.ActionEditor.Validation
{
    // 按固定清单归位应用会话与存储源码，保留资源身份并拒绝覆盖。
    public static class ApplicationLayoutCommands
    {
        private const string Root = "Assets/ActionEditor/Editor/";

        private sealed class Move
        {
            public readonly string Source;
            public readonly string Destination;
            public readonly string Guid;

            public Move(string source, string destination, string guid)
            {
                Source = source.StartsWith("Assets/", StringComparison.Ordinal) ? source : Root + source;
                Destination = destination.StartsWith("Assets/", StringComparison.Ordinal) ? destination : Root + destination;
                Guid = guid;
            }
        }

        private static readonly Move[] Moves =
        {
            new Move("Core/Documents/JsonDocumentSession.cs", "Application/Documents/DocumentSession.cs", "c2f2193dab98b9541a50c6367710e2e3"),
            new Move("Storage/JsonDocumentStore.cs", "Infrastructure/Documents/JsonDocumentRepository.cs", "ea268efaef662614588a830855b6edef"),
            new Move("Storage/ReliableFileCommit.cs", "Infrastructure/Documents/AtomicFileCommit.cs", "4897c0833f9449d4a8e76f601514b5d6"),
            new Move("Serialization/DocumentExportService.cs", "Infrastructure/Documents/DocumentExportService.cs", "2cd0dde7293a590439d2276a69f7cb45")
        };

        // 预览和应用使用同一份身份、字节及目标校验。
        [CliGroupCommand("actioneditor.validation", "Move the fixed application layout manifest, preserve GUID and bytes, and regenerate projects. No overwrite, deletion or document writes.")]
        public static object ArrangeApplicationLayout(
            [CliArg("confirm", "Apply the reviewed manifest.")] bool confirm = false,
            [CliArg("dry-run", "Inspect the exact fixed scope.")] bool dryRun = false)
            => MoveManifest(Moves, confirm, dryRun);

        // 只归位当前样例、唯一教程及业务字段描述，固定清单不接受外部路径。
        [CliGroupCommand("actioneditor.validation", "Move the fixed FeatureSample sources, sole tutorial and runtime field metadata. Preserve GUID and bytes; no user data or overwrite.")]
        public static object ArrangeFeatureLayout(
            [CliArg("confirm", "Apply the reviewed manifest.")] bool confirm = false,
            [CliArg("dry-run", "Inspect the exact fixed scope.")] bool dryRun = false)
            => MoveManifest(new[]
            {
                new Move("Assets/ActionEditor/Runtime/Samples/LegacyExperience/LegacyExperienceAsset.cs", "Assets/ActionEditor/Runtime/Samples/FeatureSample/FeatureSampleDocument.cs", "85564f260125ced47ad7a93282cf1f6c"),
                new Move("Samples/LegacyExperiencePreview.cs", "Samples/FeatureSamplePreview.cs", "8057cd80c2f7e7649be696ad53a648e8"),
                new Move("Assets/ActionEditor/Samples/LegacyExperience/README.md", "Assets/ActionEditor/Samples/FeatureSample/README.md", "30542892b6cdb57489102ba2218a9a47"),
                new Move("Core/Properties/JsonFieldDescription.cs", "Assets/ActionEditor/Runtime/Metadata/PropertyDescription.cs", "24fa94a40f752a142bddcde4c236fd8e")
            }, confirm, dryRun);

        // 清单来自封闭命令，预览与应用均重验身份、目标和字节。
        private static object MoveManifest(Move[] moves, bool confirm, bool dryRun)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("需要空闲的编辑模式。");

            foreach (var move in moves)
            {
                if (!File.Exists(move.Source) || !File.Exists(move.Source + ".meta")
                    || AssetDatabase.AssetPathToGUID(move.Source) != move.Guid)
                    throw new IOException("源身份不符：" + move.Source);
                if (File.Exists(move.Destination) || Directory.Exists(move.Destination) || File.Exists(move.Destination + ".meta"))
                    throw new IOException("目标已存在：" + move.Destination);
            }

            var folders = moves.SelectMany(m => MissingFolders(Parent(m.Destination))).Distinct()
                .OrderBy(f => f.Count(c => c == '/')).ThenBy(f => f, StringComparer.Ordinal).ToArray();
            var before = moves.Select(m => new
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
                foreach (var move in moves)
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
            return new { status = "passed", count = moves.Length, moves = before, createdFolders = folders, guidAndBytesPreserved = true, projectsRegenerated = true };
        }

        private static string Parent(string path) => Path.GetDirectoryName(path).Replace('\\', '/');

        // 固定退役已由共享Presenter替代的时间轴源码，不接受调用者路径。
        [CliGroupCommand("actioneditor.validation", "Delete eleven retired IMGUI timeline sources after caller migration. Preserve toolbar and user resources.")]
        public static object RetireImguiTimelineSources(
            [CliArg("confirm", "Apply the reviewed manifest.")] bool confirm = false,
            [CliArg("dry-run", "Inspect the exact fixed scope.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("需要空闲的编辑模式。");
            const string views = Root + "UI/IMGUI/ActionEditor/Views/";
            var sources = new[]
            {
                new { path = views + "Timeline/TimelineView.cs", guid = "29fd0ea77ca6478d9ab79107c1fb2ebb" },
                new { path = views + "Timeline/TimelineMiddleView.cs", guid = "2e97c5bb04a34c51bb689aad70f74edd" },
                new { path = views + "Timeline/TimelinePointerView.cs", guid = "c9762e7ccbdf47b8a46444b4688623dd" },
                new { path = views + "Timeline/TimelineTrackItemView.cs", guid = "9740a024375b47daacb3040efc8ad96d" },
                new { path = views + "Timeline/TimelineBottomView.cs", guid = "676923ca299c41df89ec985cb81f4ed9" },
                new { path = views + "Timeline/ClipDragger.cs", guid = "12bd2da2cc7a4f5d9878aedaa4300de5" },
                new { path = views + "Timeline/ClipDraw/ClipDrawer.cs", guid = "0fee1b9f90e042da9c9d5eb06d858631" },
                new { path = views + "Timeline/ClipDraw/BasicClipDraw.cs", guid = "8b82fdb4c58e495d9a3a6702e6ccd00a" },
                new { path = views + "Timeline/ClipDraw/SimpleClipDraw.cs", guid = "778e6bed42084851bd36e57680ad3c92" },
                new { path = views + "Timeline/ClipDraw/SignalClipDraw.cs", guid = "ea343dc3a2ea4d06b50a8c143520ed2b" },
                new { path = views + "SplitterView.cs", guid = "30d605475588454cb4d9d8c27773ba32" }
            };
            foreach (var source in sources)
                if (!File.Exists(source.path) || !File.Exists(source.path + ".meta") || AssetDatabase.AssetPathToGUID(source.path) != source.guid)
                    throw new IOException("退役身份不符：" + source.path);
            var manifest = sources.Select(source => new { source.path, source.guid, sha256 = Hash(source.path), metaSha256 = Hash(source.path + ".meta") }).ToArray();
            if (dryRun) return new { deletes = manifest, touchesUserDocuments = false, regenerateProjects = true };
            if (!confirm) throw new InvalidOperationException("需要预览和真实审批。");
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var source in sources)
                    if (!AssetDatabase.DeleteAsset(source.path) || File.Exists(source.path) || File.Exists(source.path + ".meta"))
                        throw new IOException("退役失败：" + source.path);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            return new { status = "passed", count = manifest.Length, deleted = manifest, projectsRegenerated = true };
        }

        // 调用闭包已切换后，仅退役三份固定旧编辑实现，不接受外部清单。
        [CliGroupCommand("actioneditor.validation", "Delete three retired editing sources after caller migration. No user documents or arbitrary paths.")]
        public static object RetireEditingSources(
            [CliArg("confirm", "Apply the reviewed manifest.")] bool confirm = false,
            [CliArg("dry-run", "Inspect the exact fixed scope.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("需要空闲的编辑模式。");
            var sources = new[]
            {
                new { path = Root + "Core/Editing/JsonEditCommand.cs", guid = "02fe688c43a45ac40ae01cd4c9815ecc" },
                new { path = Root + "Core/Editing/JsonEditCommands.cs", guid = "198b1605b33fc5d41b997539cd37e14b" },
                new { path = Root + "Core/Editing/JsonPropertyEditing.cs", guid = "5786662a108b0d5439fa0877d289123c" }
            };
            foreach (var source in sources)
                if (!File.Exists(source.path) || !File.Exists(source.path + ".meta") || AssetDatabase.AssetPathToGUID(source.path) != source.guid)
                    throw new IOException("退役身份不符：" + source.path);
            var manifest = sources.Select(source => new { source.path, source.guid, sha256 = Hash(source.path), metaSha256 = Hash(source.path + ".meta") }).ToArray();
            if (dryRun) return new { deletes = manifest, touchesUserDocuments = false, regenerateProjects = true };
            if (!confirm) throw new InvalidOperationException("需要预览和真实审批。");
            foreach (var source in sources)
                if (!AssetDatabase.DeleteAsset(source.path) || File.Exists(source.path) || File.Exists(source.path + ".meta"))
                    throw new IOException("退役失败：" + source.path);
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            return new { status = "passed", count = manifest.Length, deleted = manifest, projectsRegenerated = true };
        }

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
