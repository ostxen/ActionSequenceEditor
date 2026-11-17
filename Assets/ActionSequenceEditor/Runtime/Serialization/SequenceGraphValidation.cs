using ActionSequenceEditor.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ActionSequenceEditor.Serialization
{
    // 在初始化前检查结构所有权、挂载能力和原始时间字段。
    public static class SequenceGraphValidation
    {
        private static readonly FieldInfo StoredClipLength = typeof(ActionClip).GetField("length", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string path, string reason)
        { if (!condition) throw new InvalidDataException(path + ": " + reason); }
        public static void RequireValid(ActionSequence sequence)
        {
            Require(sequence != null, "$.document", "missing document.");
            Require(sequence.RootContainers != null, "$.rootItems", "null structural collection.");
            var seen = new HashSet<object>(JsonValueConverter.ReferenceComparer.Instance) { sequence };
            foreach (var item in sequence.RootContainers)
            {
                if (item is ActionGroup group)
                {
                    Require(seen.Add(group), "$.rootItems", "shared group.");
                    ParentType(group.GetType(), sequence.GetType(), "$.rootItems");
                    Unique(group, sequence.RootContainers.Cast<object>(), "$.rootItems");
                    Require(group.Tracks != null, "$.rootItems.tracks", "null structural collection.");
                    foreach (var track in group.Tracks) ValidateTrack(track, group.Tracks, group.GetType(), "$.rootItems.tracks", seen);
                }
                else if (item is ActionTrack track)
                    ValidateTrack(track, sequence.RootContainers.OfType<ActionTrack>(), null, "$.rootItems", seen);
                else Require(false, "$.rootItems", "only groups and tracks are allowed.");
            }
        }
        private static void ValidateTrack(ActionTrack track, IEnumerable<ActionTrack> siblings, Type parent, string path, HashSet<object> seen)
        {
            Require(track != null && seen.Add(track), path, "null/shared track.");
            if (parent != null) ParentType(track.GetType(), parent, path);
            Unique(track, siblings.Cast<object>(), path);
            var clipsPath = path + ".actionClips";
            Require(track.Clips != null, clipsPath, "null structural collection.");
            foreach (var clip in track.Clips)
            {
                Require(clip != null && seen.Add(clip), clipsPath, "null/shared clip.");
                ParentType(clip.GetType(), track.GetType(), clipsPath);
                Unique(clip, track.Clips.Cast<object>(), clipsPath);
                var storedLength = (float)StoredClipLength.GetValue(clip);
                Require(Finite(storedLength) && storedLength >= 0, clipsPath + ".length", "invalid stored duration.");
                Require(Finite(clip.StartTime) && clip.StartTime >= 0 && Finite(clip.Length) && clip.Length >= 0
                    && Finite(clip.EndTime) && Finite(clip.BlendIn) && Finite(clip.BlendOut), clipsPath, "invalid clip timing.");
                if (clip is PointClip) Require(clip.Length == 0, clipsPath, "point effective duration must be zero.");
            }
        }
        public static void Initialize(ActionSequence sequence, SequenceFormatContext context)
        {
            RequireValid(sequence);
            var before = JsonValueConverter.Capture(sequence, typeof(ActionSequence), context);
            sequence.Init();
            RequireValid(sequence);
            var after = JsonValueConverter.Capture(sequence, typeof(ActionSequence), context);
            // 初始化只允许重建派生时长，不能修复或改写其他作者字段。
            var expected = before.Members.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            expected["length"] = after.Members["length"];
            Require(JsonValue.Object(expected).ContentKey() == after.ContentKey(), "$", "initialization changed stored fields.");
        }
        private static void ParentType(Type child, Type parent, string path)
        {
            Require(StructureRules.AllowsParent(child, parent), path, "incompatible parent type.");
        }
        private static void Unique(object child, IEnumerable<object> siblings, string path)
        {
            Require(!StructureRules.HasUniqueConflict(child.GetType(), siblings, child), path, "duplicate unique node.");
        }
    }
}
