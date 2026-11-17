using ActionSequenceEditor.Model;
using System;
using System.IO;
using System.Linq;
using System.Text;
using ActionSequenceEditor.Serialization;

namespace ActionSequenceEditor.Infrastructure.Serialization
{
    // 协调严格JSON格式与隔离快照，不自行提交文件。
    public static class SequenceJsonCodec
    {
        private static ISequenceFormat Format => SequenceFormats.Registry.ById("actioneditor.json");
        public static string Encode(ActionSequence sequence) => Encoding.UTF8.GetString(Format.Serialize(
            SequenceCopy.Capture(sequence, false), new SequenceFormatOptions(false), SequenceFormats.Context));
        public static ActionSequence Decode(string json) => Format.Deserialize(new UTF8Encoding(false, true).GetBytes(json ?? ""), SequenceFormats.Context);
        public static void RequireValid(ActionSequence sequence)
        {
            SequenceGraphValidation.RequireValid(sequence);
            JsonValueConverter.Capture(sequence, typeof(ActionSequence), SequenceFormats.Context);
        }
        public static string CreateSaveSnapshot(ActionSequence sequence) => Encoding.UTF8.GetString(Format.Serialize(
            SequenceCopy.Capture(sequence, true), new SequenceFormatOptions(false), SequenceFormats.Context));
        public static string GetContentKey(ActionSequence sequence) => SequenceCopy.GetContentKey(sequence);
        public static string GetSnapshotContentKey(string snapshot) => GetContentKey(Decode(snapshot));
        public static string[] InspectReadbackDifferences(ActionSequence sequence)
        {
            if (sequence == null) return Array.Empty<string>();
            var snapshot = CreateSaveSnapshot(sequence);
            return ReadbackDifferences(snapshot, Decode(snapshot));
        }
        // 对比原始快照与读回图，不能把两边同时初始化而掩盖内容变化。
        public static string[] ReadbackDifferences(string source, ActionSequence decoded)
        {
            var before = (ActionSequence)JsonValueConverter.Restore(JsonText.Parse(source).Members["document"], typeof(ActionSequence), SequenceFormats.Context);
            var changes = new System.Collections.Generic.List<string>();
            FindDifferences(JsonValueConverter.Capture(before, typeof(ActionSequence), SequenceFormats.Context),
                JsonValueConverter.Capture(decoded, typeof(ActionSequence), SequenceFormats.Context), "$", changes);
            return changes.ToArray();
        }
        private static void FindDifferences(JsonValue before, JsonValue after, string path, System.Collections.Generic.List<string> changes)
        {
            if (before.Members != null && after.Members != null)
            {
                foreach (var key in before.Members.Keys.Union(after.Members.Keys))
                {
                    if (!before.Members.TryGetValue(key, out var a) || !after.Members.TryGetValue(key, out var b)) changes.Add(path + "." + key + " (missing)");
                    else FindDifferences(a, b, path + "." + key, changes);
                }
            }
            else if (before.Items != null && after.Items != null)
            {
                if (before.Items.Count != after.Items.Count) changes.Add(path + " (count)");
                for (var i = 0; i < Math.Min(before.Items.Count, after.Items.Count); i++) FindDifferences(before.Items[i], after.Items[i], path + "[" + i + "]", changes);
            }
            else if (before.Kind != after.Kind || before.Scalar != after.Scalar) changes.Add(path + " (value)");
        }
    }
}
