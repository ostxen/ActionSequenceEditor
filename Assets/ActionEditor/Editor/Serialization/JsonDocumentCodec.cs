using NBC.ActionEditor.Domain;
using System;
using System.IO;
using System.Linq;
using System.Text;
using NBC.ActionEditor.Serialization;

namespace NBC.ActionEditor.Storage
{
    /// <summary>Editor coordination over the unique runtime format and format-independent snapshots.</summary>
    public static class JsonDocumentCodec
    {
        private static IActionDocumentFormat Format => DocumentFormats.Registry.ById("actioneditor.json");
        public static string Encode(ActionDocument asset) => Encoding.UTF8.GetString(Format.Serialize(
            DocumentSnapshots.Capture(asset, false), new DocumentFormatOptions(false), DocumentFormats.Context));
        public static ActionDocument Decode(string json) => Format.Deserialize(new UTF8Encoding(false, true).GetBytes(json ?? ""), DocumentFormats.Context);
        public static void RequireValid(ActionDocument asset)
        {
            ActionDocumentGraph.RequireValid(asset);
            DocumentValueConverter.Capture(asset, typeof(ActionDocument), DocumentFormats.Context);
        }
        public static string CreateSaveSnapshot(ActionDocument document) => Encoding.UTF8.GetString(Format.Serialize(
            DocumentSnapshots.Capture(document, true), new DocumentFormatOptions(false), DocumentFormats.Context));
        public static string Content(ActionDocument asset) => DocumentSnapshots.Content(asset);
        public static string ContentOfSnapshot(string snapshot) => Content(Decode(snapshot));
        public static string[] InspectReadbackDifferences(ActionDocument asset)
        {
            if (asset == null) return Array.Empty<string>();
            var snapshot = CreateSaveSnapshot(asset);
            return ReadbackDifferences(snapshot, Decode(snapshot));
        }
        public static string[] ReadbackDifferences(string source, ActionDocument decoded)
        {
            var before = (ActionDocument)DocumentValueConverter.Restore(DocumentJson.Parse(source).Members["document"], typeof(ActionDocument), DocumentFormats.Context);
            var changes = new System.Collections.Generic.List<string>();
            FindDifferences(DocumentValueConverter.Capture(before, typeof(ActionDocument), DocumentFormats.Context),
                DocumentValueConverter.Capture(decoded, typeof(ActionDocument), DocumentFormats.Context), "$", changes);
            return changes.ToArray();
        }
        private static void FindDifferences(DocumentValue before, DocumentValue after, string path, System.Collections.Generic.List<string> changes)
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
