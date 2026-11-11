using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Pipeline.Commands;
using UnityEditor;

namespace NBC.ActionEditor.Validation
{
    // Fixed RF16-C manifest. No caller-supplied paths, code, overwrite or deletion.
    public static class Rf17MoveCommands
    {
        private const string Root = "Assets/ActionEditor/";
        private sealed class Move
        {
            public readonly string source, destination, guid;
            public Move(string source, string destination, string guid)
            { this.source = Root + source; this.destination = Root + destination; this.guid = guid; }
        }
        private static readonly Move[] Moves = {
            new Move("Editor/ActionEditorContext.cs", "Editor/Core/Documents/ActionEditorContext.cs", "a640ca322a3675c459435be002c15456"),
            new Move("Editor/App.cs", "Editor/Core/Documents/App.cs", "388d9541706214b549412603b072872b"),
            new Move("Editor/Def/PrefsConst.cs", "Editor/Preferences/PrefsConst.cs", "fd3fba45da7f428f925335ec345164f5"),
            new Move("Editor/Def/Styles.cs", "Editor/UI/Styles.cs", "e001fc02d4424aaab245be5232f6eede"),
            new Move("Editor/Events/Interfaces/IDragBeginHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IDragBeginHandler.cs", "251afc4d5d23422bb070ab91613db9e1"),
            new Move("Editor/Events/Interfaces/IDragEndHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IDragEndHandler.cs", "b1285635d86e4a2889cf0b4d1ccecc43"),
            new Move("Editor/Events/Interfaces/IPointerClickHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IPointerClickHandler.cs", "2b8aff9712d84e6797f3ebab73c17ecb"),
            new Move("Editor/Events/Interfaces/IPointerDownHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IPointerDownHandler.cs", "9f9d78191bcd4c4cad6119d752c25784"),
            new Move("Editor/Events/Interfaces/IPointerDragHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IPointerDragHandler.cs", "672622fbf2354c0597396c57cc3d84ce"),
            new Move("Editor/Events/Interfaces/IPointerEnterHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IPointerEnterHandler.cs", "7a9293adafb84c1789f22a06c2c1ada5"),
            new Move("Editor/Events/Interfaces/IPointerExitHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IPointerExitHandler.cs", "08484bba1373447aa8c58afb23028b48"),
            new Move("Editor/Events/Interfaces/IPointerUpHandler.cs", "Editor/UI/IMGUI/Events/Interfaces/IPointerUpHandler.cs", "276bcb99d9484ab1ab4f1c09dd6a2374"),
            new Move("Editor/Events/PointerEventData.cs", "Editor/UI/IMGUI/Events/PointerEventData.cs", "03cc65a8cf0245bcaeb2d9d2dd845015"),
            new Move("Editor/GUIS/ActionEditor/ActionEditorWindow.cs", "Editor/UI/IMGUI/ActionEditor/ActionEditorWindow.cs", "7990bd1db6c440818370e6970f8d2878"),
            new Move("Editor/GUIS/ActionEditor/Views/Base/ItemViewBase.cs", "Editor/UI/IMGUI/ActionEditor/Views/Base/ItemViewBase.cs", "574d358ac3144790b070d3732da8b78d"),
            new Move("Editor/GUIS/ActionEditor/Views/Base/ViewBase.cs", "Editor/UI/IMGUI/ActionEditor/Views/Base/ViewBase.cs", "23c7bfac023a4db68e26fe4c87c0df7e"),
            new Move("Editor/GUIS/ActionEditor/Views/SplitterView.cs", "Editor/UI/IMGUI/ActionEditor/Views/SplitterView.cs", "30d605475588454cb4d9d8c27773ba32"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/ClipDragger.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/ClipDragger.cs", "12bd2da2cc7a4f5d9878aedaa4300de5"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/ClipDraw/BasicClipDraw.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/ClipDraw/BasicClipDraw.cs", "8b82fdb4c58e495d9a3a6702e6ccd00a"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/ClipDraw/ClipDrawer.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/ClipDraw/ClipDrawer.cs", "0fee1b9f90e042da9c9d5eb06d858631"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/ClipDraw/SignalClipDraw.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/ClipDraw/SignalClipDraw.cs", "ea343dc3a2ea4d06b50a8c143520ed2b"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/ClipDraw/SimpleClipDraw.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/ClipDraw/SimpleClipDraw.cs", "778e6bed42084851bd36e57680ad3c92"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/TimelineBottomView.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/TimelineBottomView.cs", "676923ca299c41df89ec985cb81f4ed9"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/TimelineHeaderView.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/TimelineHeaderView.cs", "68ddb2a77a4943be95a315e2cf5e7f82"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/TimelineMiddleView.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/TimelineMiddleView.cs", "2e97c5bb04a34c51bb689aad70f74edd"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/TimelinePointerView.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/TimelinePointerView.cs", "c9762e7ccbdf47b8a46444b4688623dd"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/TimelineTrackItemView.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/TimelineTrackItemView.cs", "9740a024375b47daacb3040efc8ad96d"),
            new Move("Editor/GUIS/ActionEditor/Views/Timeline/TimelineView.cs", "Editor/UI/IMGUI/ActionEditor/Views/Timeline/TimelineView.cs", "29fd0ea77ca6478d9ab79107c1fb2ebb"),
            new Move("Editor/GUIS/ActionEditor/Views/WelcomeView.cs", "Editor/UI/IMGUI/ActionEditor/Views/WelcomeView.cs", "c029ce4cec81431a99240d6447a2259a"),
            new Move("Editor/GUIS/CreateAssetWindow.cs", "Editor/UI/IMGUI/CreateAssetWindow.cs", "1a368b48e50f45e797eb364d1bff797f"),
            new Move("Editor/GUIS/Customized/CustomizedDrawTools.cs", "Editor/UI/IMGUI/Customized/CustomizedDrawTools.cs", "e824e3e530dd49dd8dfe95b7e73af024"),
            new Move("Editor/GUIS/Customized/EditorCustomFactory.cs", "Editor/UI/Extensions/EditorCustomFactory.cs", "5fbdd871215844508b90184a82e29cec"),
            new Move("Editor/GUIS/Customized/Header/HeaderBase.cs", "Editor/UI/Extensions/HeaderBase.cs", "e2faf2ee3510498884548206b4aebbc3"),
            new Move("Editor/GUIS/Customized/Header/HeaderGUI.cs", "Editor/UI/IMGUI/Customized/Header/HeaderGUI.cs", "44fe374f16874682881cb81e7e8ec16f"),
            new Move("Editor/GUIS/Customized/ICustomized.cs", "Editor/UI/IMGUI/Customized/ICustomized.cs", "565b99ec09a74ec4aa2cba0241d3ce75"),
            new Move("Editor/GUIS/Customized/Inspectors/Com/ActionClipInspector.cs", "Editor/UI/IMGUI/Customized/Inspectors/Com/ActionClipInspector.cs", "1d4a2bbcab364421fbac5f6dbe2fd18d"),
            new Move("Editor/GUIS/Customized/Inspectors/Com/AssetInspector.cs", "Editor/UI/IMGUI/Customized/Inspectors/Com/AssetInspector.cs", "1d716975a9a545de861fefb88b2c17a6"),
            new Move("Editor/GUIS/Customized/Inspectors/Com/GroupInspector.cs", "Editor/UI/IMGUI/Customized/Inspectors/Com/GroupInspector.cs", "8f1af75f4fa95496783a80d12b1c1f60"),
            new Move("Editor/GUIS/Customized/Inspectors/Com/TrackInspector.cs", "Editor/UI/IMGUI/Customized/Inspectors/Com/TrackInspector.cs", "f20270083fd0145dd98d035c67165718"),
            new Move("Editor/GUIS/Customized/Inspectors/InspectorPreviewAssetInspector.cs", "Editor/UI/IMGUI/Customized/Inspectors/InspectorPreviewAssetInspector.cs", "39e44d10f51f434b86781f9d221d81bc"),
            new Move("Editor/GUIS/Customized/Inspectors/InspectorsBase.cs", "Editor/UI/IMGUI/Customized/Inspectors/InspectorsBase.cs", "736d8f5e613eb47f2b943824a35ea106"),
            new Move("Editor/GUIS/ObjectSelectorWindow.cs", "Editor/UI/IMGUI/ObjectSelectorWindow.cs", "f7f5f91a03634e98ac7aff43da23fb15"),
            new Move("Editor/GUIS/PreferencesWindow.cs", "Editor/UI/IMGUI/PreferencesWindow.cs", "f476c6fc2de944008b2cbae4c9b2c155"),
            new Move("Editor/GUIS/RenameItemWindow.cs", "Editor/UI/IMGUI/RenameItemWindow.cs", "58a9ae359aabde84fac0ca5be5a4ffb9"),
            new Move("Editor/Storage/DocumentSession.cs", "Editor/Experiments/AssetWorkbench/DocumentSession.cs", "04daf273971b0734896ac641876f1882"),
            new Move("Editor/Storage/DocumentStore.cs", "Editor/Experiments/AssetWorkbench/DocumentStore.cs", "5d2f8a8751f12bf43bdf6ecc83f38dfc"),
            new Move("Editor/Storage/DocumentTransaction.cs", "Editor/Experiments/AssetWorkbench/DocumentTransaction.cs", "6322d368ea8df1643b82a8879cb8d40b"),
            new Move("Editor/Storage/JsonDocumentCodec.cs", "Editor/Serialization/JsonDocumentCodec.cs", "0a4c86a687eab9944a10be8e1631f77b"),
            new Move("Editor/Storage/JsonDocumentSession.cs", "Editor/Core/Documents/JsonDocumentSession.cs", "c2f2193dab98b9541a50c6367710e2e3"),
            new Move("Editor/Storage/JsonEditCommand.cs", "Editor/Core/Editing/JsonEditCommand.cs", "02fe688c43a45ac40ae01cd4c9815ecc"),
            new Move("Editor/Storage/JsonEditCommands.cs", "Editor/Core/Editing/JsonEditCommands.cs", "198b1605b33fc5d41b997539cd37e14b"),
            new Move("Editor/Storage/JsonPropertyEditing.cs", "Editor/Core/Editing/JsonPropertyEditing.cs", "5786662a108b0d5439fa0877d289123c"),
            new Move("Editor/Tools/ActionEditorTypeCatalog.cs", "Editor/Extensions/ActionEditorTypeCatalog.cs", "eeafbed3a81157d4598cd1360d127537"),
            new Move("Editor/Tools/DrawTools.cs", "Editor/UI/IMGUI/Controls/DrawTools.cs", "f980afac9e9644d7828b36a7f21bd55b"),
            new Move("Editor/Tools/EditorTools.cs", "Editor/UI/IMGUI/Controls/EditorTools.cs", "d453b9693b504b378a526c45545e082f"),
            new Move("Editor/Tools/Extensions/DirectableAssetExtensions.cs", "Editor/Core/Timeline/DirectableAssetExtensions.cs", "f14e07d86d9e4228a9b42d4b3e16f65e"),
            new Move("Editor/Tools/Extensions/EditorWindowExtensions.cs", "Editor/UI/IMGUI/Controls/Extensions/EditorWindowExtensions.cs", "54cef818c40c4bad979d5c76bd29af01"),
            new Move("Editor/Tools/Extensions/FieldInfoExtensions.cs", "Editor/Core/Properties/FieldInfoExtensions.cs", "0d41784df0c94c93b1726c7e86ef12b6"),
            new Move("Editor/Tools/Extensions/RectExtensions.cs", "Editor/UI/IMGUI/Controls/Extensions/RectExtensions.cs", "a5198ce2f30c49d28840867988d3417d"),
            new Move("Editor/Tools/Extensions/TrackExtends.cs", "Editor/UI/DirectableAppearance.cs", "5ec48bf41a864cefa3fb754b75a8ed6c"),
            new Move("Editor/Tools/JsonFieldDescription.cs", "Editor/Core/Properties/JsonFieldDescription.cs", "24fa94a40f752a142bddcde4c236fd8e"),
            new Move("Editor/Tools/NBLayout.cs", "Editor/UI/IMGUI/Controls/NBLayout.cs", "9ba2896878e543a78dacea8c16c523ea"),
            new Move("Editor/Tools/PreviewResizer.cs", "Editor/UI/IMGUI/Controls/PreviewResizer.cs", "83b9fcb9b0d24298867fa65ca872b240"),
            new Move("Editor/Tools/TimelineCandidates.cs", "Editor/Core/Timeline/TimelineCandidates.cs", "948cc05cf2351844d97181138af198b9"),
            new Move("Editor/Tools/TimelineGeometry.cs", "Editor/Core/Timeline/TimelineGeometry.cs", "6e7624dde141b2c4ab8515fc6ce160dd"),
            new Move("Editor/Tools/TimelineNavigation.cs", "Editor/Core/Timeline/TimelineNavigation.cs", "6102006f0f5b79c48a051c52dcd62c9a"),
            new Move("Editor/Workbench/ActionWorkbench.cs", "Editor/Experiments/AssetWorkbench/ActionWorkbench.cs", "216a627fd6fd47041bf0f5f1937c7c18"),
            new Move("Editor/Workbench/EventPropertyContext.cs", "Editor/Experiments/AssetWorkbench/EventPropertyContext.cs", "95a06179797c03d4a9d6c89aacc67c33"),
            new Move("Editor/Workbench/EventRegistry.cs", "Editor/Experiments/AssetWorkbench/EventRegistry.cs", "2928be964e82ccc4c82d2a36e8b58e87"),
            new Move("Editor/Workbench/JsonActionWorkbench.cs", "Editor/UI/UIToolkit/JsonActionWorkbench.cs", "bd2bd6eded8ebf14687952b313d079d9"),
            new Move("Editor/Workbench/JsonPreferencesWindow.cs", "Editor/UI/UIToolkit/JsonPreferencesWindow.cs", "69c0eced166ee5a45ad033187ae1070e"),
            new Move("Editor/Workbench/JsonPropertyView.cs", "Editor/UI/UIToolkit/JsonPropertyView.cs", "947256fa11135cc43b04ba402a1f03c9"),
            new Move("Editor/Workbench/JsonTimelineCanvas.cs", "Editor/UI/UIToolkit/JsonTimelineCanvas.cs", "f7548d331bc116c4ba695eb4b55bdf0f"),
            new Move("Editor/Workbench/TimelineCanvas.cs", "Editor/Experiments/AssetWorkbench/TimelineCanvas.cs", "77924aa4ff2b9af4285a05b9c60861cf"),
            new Move("Editor/Workbench/TimelineInteraction.cs", "Editor/Experiments/AssetWorkbench/TimelineInteraction.cs", "2aa803c56ad9b514aa6d1abaf66f5d2b"),
            new Move("Editor/Workbench/WorkbenchInspectorTarget.cs", "Editor/Experiments/AssetWorkbench/WorkbenchInspectorTarget.cs", "8f41182c50a1c024ea1f5829f012dfed"),
            new Move("Runtime/Const/Prefs.cs", "Editor/Preferences/Prefs.cs", "d45e1464763294b70a06ff81bc069161"),
            new Move("Runtime/Model/ActionDocument.cs", "Runtime/Experiments/AssetWorkbench/ActionDocument.cs", "0bcdb82417348fc4b8dc220708b24e5d"),
            new Move("Runtime/Model/DocumentValidation.cs", "Runtime/Experiments/AssetWorkbench/DocumentValidation.cs", "4fbd81b2963b270468acaeb88ee9ac8b"),
        };

        [CliGroupCommand("actioneditor.validation", "Move only the 79 RF17 manifest scripts with AssetDatabase; preserve GUID and bytes, create only required folders, regenerate real Unity projects. No overwrite, deletion or user data writes. Not Unity Undoable.")]
        public static object Rf17Move(
            [CliArg("confirm", "Apply the reviewed fixed manifest.")] bool confirm = false,
            [CliArg("dry-run", "Read-only validation and exact preview.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Requires ready EditMode.");
            if (Moves.Length != 79 || Moves.Select(m => m.source).Distinct().Count() != 79
                || Moves.Select(m => m.destination).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 79)
                throw new InvalidOperationException("Invalid fixed manifest.");
            foreach (var move in Moves)
            {
                if (!File.Exists(move.source) || !File.Exists(move.source + ".meta")
                    || AssetDatabase.AssetPathToGUID(move.source) != move.guid)
                    throw new IOException("Source identity changed: " + move.source);
                if (File.Exists(move.destination) || Directory.Exists(move.destination) || File.Exists(move.destination + ".meta"))
                    throw new IOException("Destination exists: " + move.destination);
                var folder = Path.GetDirectoryName(move.destination).Replace('\\', '/');
                while (!AssetDatabase.IsValidFolder(folder))
                {
                    if (File.Exists(folder) || Directory.Exists(folder) || File.Exists(folder + ".meta"))
                        throw new IOException("Unregistered/conflicting folder: " + folder);
                    folder = Path.GetDirectoryName(folder).Replace('\\', '/');
                }
            }
            var before = Moves.Select(m => new { m.source, m.destination, m.guid, sha256 = Hash(m.source), metaSha256 = Hash(m.source + ".meta") }).ToArray();
            var folders = Moves.SelectMany(m => MissingFolders(Path.GetDirectoryName(m.destination).Replace('\\', '/')))
                .Distinct().OrderBy(f => f.Count(c => c == '/')).ThenBy(f => f, StringComparer.Ordinal).ToArray();
            if (dryRun) return new { schema = 1, count = Moves.Length, moves = before, createFolders = folders,
                regenerateProjects = true, touchesUserDocuments = false, deletes = false, overwrite = false };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            foreach (var folder in folders)
            {
                var guid = AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
                if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(folder)) throw new IOException("Folder creation failed: " + folder);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var move in Moves)
                {
                    var error = AssetDatabase.ValidateMoveAsset(move.source, move.destination);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(move.source + ": " + error);
                    error = AssetDatabase.MoveAsset(move.source, move.destination);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(move.source + ": " + error);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            foreach (var item in before)
                if (File.Exists(item.source) || File.Exists(item.source + ".meta")
                    || AssetDatabase.AssetPathToGUID(item.destination) != item.guid
                    || Hash(item.destination) != item.sha256 || Hash(item.destination + ".meta") != item.metaSha256)
                    throw new IOException("Post-move identity/byte verification failed: " + item.destination);
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            return new { status = "passed", count = Moves.Length, moves = before, createdFolders = folders,
                guidAndBytesPreserved = true, projectsRegenerated = true };
        }
        private static string[] MissingFolders(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return Array.Empty<string>();
            return MissingFolders(Path.GetDirectoryName(folder).Replace('\\', '/')).Concat(new[] { folder }).ToArray();
        }
        private static string Hash(string path)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
