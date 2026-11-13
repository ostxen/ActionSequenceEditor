using System;
using System.Linq;
using NBC.ActionEditor.Serialization;

namespace NBC.ActionEditor.Storage
{
    public static class DocumentSnapshots
    {
        public static Asset Capture(Asset source, bool beforeSerialize, DocumentFormatContext context = null)
        {
            context ??= DocumentFormats.Context;
            ActionDocumentGraph.RequireValid(source);
            var tree = DocumentValueConverter.Capture(source, typeof(Asset), context);
            var clone = (Asset)DocumentValueConverter.Restore(tree, typeof(Asset), context);
            ActionDocumentGraph.Initialize(clone, context);
            if (beforeSerialize) clone.OnBeforeSerialize();
            ActionDocumentGraph.RequireValid(clone);
            clone.UpdateMaxTime();
            DocumentValueConverter.Capture(clone, typeof(Asset), context);
            return clone;
        }
        public static string Content(Asset source, DocumentFormatContext context = null)
        {
            context ??= DocumentFormats.Context;
            ActionDocumentGraph.RequireValid(source);
            var tree = DocumentValueConverter.Capture(source, typeof(Asset), context, true);
            var duration = 0f;
            foreach (var track in source.AllTracks)
                if (track.Group == null || track.Group.IsActive)
                    if ((bool)DocumentValueConverter.Fields(track.GetType()).Single(f => f.DeclaringType == typeof(Track) && f.Name == "active").GetValue(track))
                        foreach (var clip in track.Clips) duration = Math.Max(duration, clip.EndTime);
            var members = tree.Members.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            members["length"] = DocumentValueConverter.Capture(Math.Max(.1f, duration), typeof(float), context);
            return DocumentValue.Object(members).ContentKey();
        }
        public static string CaptureFragment(IDirectable item, DocumentFormatContext context = null)
        {
            if (item is not Group && item is not Track && item is not Clip) throw new InvalidOperationException("Unsupported clipboard node.");
            return DocumentJson.Print(DocumentValueConverter.Capture(item, item.GetType(), context ?? DocumentFormats.Context), true);
        }
        public static IDirectable RestoreFragment(Type type, string text, DocumentFormatContext context = null)
        {
            if (!typeof(Group).IsAssignableFrom(type) && !typeof(Track).IsAssignableFrom(type) && !typeof(Clip).IsAssignableFrom(type))
                throw new InvalidOperationException("Unsupported clipboard type.");
            return (IDirectable)DocumentValueConverter.Restore(DocumentJson.Parse(text), type, context ?? DocumentFormats.Context);
        }
    }
}
