using System;
using System.IO;
using System.Linq;
using FullSerializer;

namespace NBC.ActionEditor.Storage
{
    /// <summary>Existing FullSerializer JSON and authoring-content comparison, without file or Unity import operations.</summary>
    public static class JsonDocumentCodec
    {
        public static string Encode(Asset asset)
        {
            if (asset == null) throw new InvalidOperationException("没有打开的文档。");
            asset.OnBeforeSerialize();
            return Json.Serialize(asset);
        }

        public static Asset Decode(string json)
        {
            var asset = Json.Deserialize(typeof(Asset), json) as Asset;
            if (asset == null) throw new InvalidDataException("无法读取 ActionEditor JSON；当前文档未改变。");
            RequireValid(asset);
            asset.Init();
            return asset;
        }

        public static void RequireValid(Asset asset)
        {
            if (asset.groups == null) throw new InvalidDataException("分组列表为空引用。");
            var seen = new System.Collections.Generic.HashSet<object>();
            foreach (var group in asset.groups)
            {
                if (group == null || !seen.Add(group) || group.Tracks == null) throw new InvalidDataException("无效或共享的分组。");
                foreach (var track in group.Tracks)
                {
                    if (track == null || !seen.Add(track) || track.Clips == null) throw new InvalidDataException("无效或共享的轨道。");
                    foreach (var clip in track.Clips)
                    {
                        if (clip == null || !seen.Add(clip) || !Finite(clip.StartTime) || !Finite(clip.Length) || clip.StartTime < 0 || clip.Length < 0
                            || !Finite(clip.EndTime) || !Finite(clip.BlendIn) || !Finite(clip.BlendOut))
                            throw new InvalidDataException("无效片段或非有限时间，拒绝保存。");
                    }
                }
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static string CreateSaveSnapshot(Asset document)
        {
            if (document == null) throw new InvalidOperationException("没有打开的文档。");
            RequireValid(document);
            // Public graph edits and serialization callbacks may change the last active clip.
            // Decode rebuilds this cache too; serialize the same derived duration on both sides.
            document.OnBeforeSerialize();
            RequireValid(document);
            document.UpdateMaxTime();
            return Json.Serialize(document);
        }
        public static string Content(Asset asset)
        {
            return Normalize(Json.Serialize(asset));
        }
        public static string[] InspectReadbackDifferences(Asset asset)
        {
            if (asset == null) return Array.Empty<string>();
            var source = Json.Serialize(asset);
            return ReadbackDifferences(source, Decode(source));
        }
        public static string[] ReadbackDifferences(string source, Asset decoded)
        {
            var changes = new System.Collections.Generic.List<string>();
            FindDifferences(fsJsonParser.Parse(Normalize(source)), fsJsonParser.Parse(Normalize(Json.Serialize(decoded))), "$", changes);
            return changes.ToArray();
        }
        private static void FindDifferences(fsData before, fsData after, string path, System.Collections.Generic.List<string> changes)
        {
            if (before.IsDictionary && after.IsDictionary)
            {
                foreach (var key in before.AsDictionary.Keys.Union(after.AsDictionary.Keys))
                {
                    if (!before.AsDictionary.TryGetValue(key, out var a) || !after.AsDictionary.TryGetValue(key, out var b)) changes.Add(path + "." + key + " (missing)");
                    else FindDifferences(a, b, path + "." + key, changes);
                }
            }
            else if (before.IsList && after.IsList)
            {
                if (before.AsList.Count != after.AsList.Count) changes.Add(path + " (count)");
                for (var i = 0; i < Math.Min(before.AsList.Count, after.AsList.Count); i++) FindDifferences(before.AsList[i], after.AsList[i], path + "[" + i + "]", changes);
            }
            else if (!before.Equals(after)) changes.Add(path + " (value)");
        }
        internal static string Normalize(string json)
        {
            var data = fsJsonParser.Parse(json);
            if (data.IsDictionary)
            {
                var root = data.AsDictionary;
                foreach (var key in root.Keys.Where(k => k.Equals("viewTimeMin", StringComparison.OrdinalIgnoreCase)
                    || k.Equals("viewTimeMax", StringComparison.OrdinalIgnoreCase) || k.Equals("viewTime", StringComparison.OrdinalIgnoreCase)).ToArray()) root.Remove(key);
                if (root.TryGetValue("groups", out var groups) && groups.IsList)
                    foreach (var group in groups.AsList)
                        if (group.IsDictionary)
                            foreach (var key in group.AsDictionary.Keys.Where(k => k.Equals("isCollapsed", StringComparison.OrdinalIgnoreCase)).ToArray()) group.AsDictionary.Remove(key);
            }
            return fsJsonPrinter.CompressedJson(data);
        }
    }
}
