using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NBC.ActionEditor.Serialization
{
    public static class ActionDocumentGraph
    {
        private static readonly FieldInfo StoredClipLength = typeof(Clip).GetField("length", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string path, string reason)
        { if (!condition) throw new InvalidDataException(path + ": " + reason); }
        public static void RequireValid(Asset document)
        {
            Require(document != null, "$.document", "missing document.");
            Require(document.groups != null, "$.groups", "null structural collection.");
            var seen = new HashSet<object>(DocumentValueConverter.ReferenceComparer.Instance) { document };
            foreach (var group in document.groups)
            {
                Require(group != null && seen.Add(group), "$.groups", "null/shared group.");
                ParentType(group.GetType(), document.GetType(), "$.groups");
                Unique(group, document.groups.Cast<object>(), "$.groups");
                Require(group.Tracks != null, "$.groups.tracks", "null structural collection.");
                foreach (var track in group.Tracks)
                {
                    Require(track != null && seen.Add(track), "$.groups.tracks", "null/shared track.");
                    ParentType(track.GetType(), group.GetType(), "$.groups.tracks");
                    Unique(track, group.Tracks.Cast<object>(), "$.groups.tracks");
                    Require(track.Clips != null, "$.groups.tracks.actionClips", "null structural collection.");
                    foreach (var clip in track.Clips)
                    {
                        const string path = "$.groups.tracks.actionClips";
                        Require(clip != null && seen.Add(clip), path, "null/shared clip.");
                        ParentType(clip.GetType(), track.GetType(), path);
                        Unique(clip, track.Clips.Cast<object>(), path);
                        var storedLength = (float)StoredClipLength.GetValue(clip);
                        Require(Finite(storedLength) && storedLength >= 0, path + ".length", "invalid stored duration.");
                        Require(Finite(clip.StartTime) && clip.StartTime >= 0 && Finite(clip.Length) && clip.Length >= 0
                            && Finite(clip.EndTime) && Finite(clip.BlendIn) && Finite(clip.BlendOut), path, "invalid clip timing.");
                        if (clip is ClipSignal) Require(clip.Length == 0, path, "point effective duration must be zero.");
                    }
                }
            }
        }
        public static void Initialize(Asset document, DocumentFormatContext context)
        {
            RequireValid(document);
            var before = DocumentValueConverter.Capture(document, typeof(Asset), context);
            document.Init();
            RequireValid(document);
            var after = DocumentValueConverter.Capture(document, typeof(Asset), context);
            // Init may rebuild the derived root duration, but must not silently rewrite stored author/workspace values.
            var expected = before.Members.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            expected["length"] = after.Members["length"];
            Require(DocumentValue.Object(expected).ContentKey() == after.ContentKey(), "$", "initialization changed stored fields.");
        }
        private static void ParentType(Type child, Type parent, string path)
        {
            var parents = DirectableTypeInfo.For(child).AttachableTypes;
            Require(parents == null || parents.Contains(parent), path, "incompatible parent type.");
        }
        private static void Unique(object child, IEnumerable<object> siblings, string path)
        {
            if (DirectableTypeInfo.For(child.GetType()).IsUnique)
                Require(siblings.Count(item => item != null && item.GetType() == child.GetType()) == 1, path, "duplicate unique node.");
        }
    }
}
