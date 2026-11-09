using System;
using System.IO;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Validation
{
    public static class SessionProbeCommands
    {
        private const string Root = "Assets/ActionEditorValidationA05";

        [CliGroupCommand("actioneditor.validation", "A05 fixed isolated native document session validation. Preview and human approval required.")]
        public static object SessionProbe(
            [CliArg("confirm", "Apply reviewed fixture lifecycle.")] bool confirm = false,
            [CliArg("dry-run", "Preview without creating assets.")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Requires ready EditMode.");
            if (Directory.Exists(Root) || File.Exists(Root) || File.Exists(Root + ".meta")) throw new IOException("Fixture path exists; refusing overwrite.");
            if (dryRun) return new
            {
                schema = 1, root = Root,
                targets = new[] { Root + "/A.asset", Root + "/B.asset", Root + "/Resource.asset" },
                operations = new[] { "create fixed new fixture folder", "native session create/edit/save/reopen A and B", "resource-reference and parameter roundtrip", "failed switch preserves A", "repeat save with revision checks", "close and dispose sessions", "delete only newly created fixture folder" },
                touchesExistingAssets = false, changesPackagesOrSettings = false
            };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            var ownsFolder = false;
            try
            {
                ownsFolder = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "ActionEditorValidationA05"));
                if (!ownsFolder) throw new IOException("Could not create fixture folder.");
                var resource = new Texture2D(1, 1);
                AssetDatabase.CreateAsset(resource, Root + "/Resource.asset");
                using (var session = new DocumentSession())
                {
                    session.New();
                    var group = new DocumentGroup();
                    var track = new DocumentTrack();
                    track.Events.Add(new SignalEvent { Start = 0.25, Message = "session", Resource = resource });
                    track.Events.Add(new IntervalEvent { Start = 1.25, Duration = 0.5, Value = 7, Offset = new Vector3(1, 2, 3), Resource = resource });
                    group.Tracks.Add(track);
                    session.Document.Groups.Add(group);
                    session.Save(Root + "/A.asset");
                    if (session.IsDirty) throw new InvalidOperationException("Saved document is dirty.");
                    session.Document.Title = "second save";
                    session.Save();
                    session.DiscardAndClose();
                    session.Open(Root + "/A.asset");
                    Check(session.Document, resource);
                    var original = session.Document;
                    try { session.Open(Root + "/Missing.asset"); throw new InvalidOperationException("Missing document unexpectedly loaded."); }
                    catch (FileNotFoundException) { }
                    if (session.Document != original || session.Path != Root + "/A.asset") throw new InvalidOperationException("Failed switch altered target.");
                    session.DiscardAndClose();
                    session.New();
                    session.Document.Title = "B";
                    session.Save(Root + "/B.asset");
                    session.DiscardAndClose();
                    session.Open(Root + "/A.asset");
                    Check(session.Document, resource);
                    session.Open(Root + "/B.asset");
                    if (session.Document.Title != "B") throw new InvalidOperationException("A/B switch failed.");
                    session.DiscardAndClose();
                    session.DiscardAndClose();
                }
                return new { status = "passed", root = Root, checks = new[] { "native resource roundtrip", "repeat save", "close/reopen", "failed switch", "A/B switch", "repeated close/dispose" }, cleanup = "new fixture folder deleted before response" };
            }
            finally
            {
                if (ownsFolder && !AssetDatabase.DeleteAsset(Root)) throw new IOException("Fixture cleanup failed: " + Root);
            }
        }

        private static void Check(ActionDocument document, Texture2D resource)
        {
            var signal = document.Groups[0].Tracks[0].Events[0] as SignalEvent;
            var interval = document.Groups[0].Tracks[0].Events[1] as IntervalEvent;
            if (document.Title != "second save" || signal == null || interval == null || signal.Start != 0.25 || signal.Message != "session"
                || interval.Start != 1.25 || interval.Duration != 0.5 || interval.Value != 7 || interval.Offset != new Vector3(1, 2, 3)
                || signal.Resource != resource || interval.Resource != resource)
                throw new InvalidDataException("Native document content or resource reference changed.");
        }
    }
}
