using NBC.ActionEditor.Domain;
using System;
using System.Linq;
using NBC.ActionEditor.Serialization;

namespace NBC.ActionEditor.Storage
{
    // 隔离序列化回调与源图，并以同一受控值树生成作者比较键。
    public static class DocumentSnapshots
    {
        public static ActionDocument Capture(ActionDocument source, bool beforeSerialize, DocumentFormatContext context = null)
        {
            context ??= DocumentFormats.Context;
            ActionDocumentGraph.RequireValid(source);
            var tree = DocumentValueConverter.Capture(source, typeof(ActionDocument), context);
            var clone = (ActionDocument)DocumentValueConverter.Restore(tree, typeof(ActionDocument), context);
            ActionDocumentGraph.Initialize(clone, context);
            if (beforeSerialize) clone.OnBeforeSerialize();
            ActionDocumentGraph.RequireValid(clone);
            clone.UpdateMaxTime();
            DocumentValueConverter.Capture(clone, typeof(ActionDocument), context);
            return clone;
        }
        public static string Content(ActionDocument source, DocumentFormatContext context = null)
        {
            context ??= DocumentFormats.Context;
            ActionDocumentGraph.RequireValid(source);
            var tree = DocumentValueConverter.Capture(source, typeof(ActionDocument), context, true);
            var duration = 0f;
            foreach (var track in source.AllTracks)
                if (track.Group == null || track.Group.IsActive)
                    if ((bool)DocumentValueConverter.Fields(track.GetType()).Single(f => f.DeclaringType == typeof(TimelineTrack) && f.Name == "active").GetValue(track))
                        foreach (var clip in track.Clips) duration = Math.Max(duration, clip.EndTime);
            var members = tree.Members.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            members["length"] = DocumentValueConverter.Capture(Math.Max(.1f, duration), typeof(float), context);
            return DocumentValue.Object(members).ContentKey();
        }
        public static string CaptureFragment(IDirectable item, DocumentFormatContext context = null)
        {
            if (item is not TimelineGroup && item is not TimelineTrack && item is not TimelineClip) throw new InvalidOperationException("Unsupported clipboard node.");
            return DocumentJson.Print(DocumentValueConverter.Capture(item, item.GetType(), context ?? DocumentFormats.Context), true);
        }
        public static IDirectable RestoreFragment(Type type, string text, DocumentFormatContext context = null)
        {
            if (!typeof(TimelineGroup).IsAssignableFrom(type) && !typeof(TimelineTrack).IsAssignableFrom(type) && !typeof(TimelineClip).IsAssignableFrom(type))
                throw new InvalidOperationException("Unsupported clipboard type.");
            return (IDirectable)DocumentValueConverter.Restore(DocumentJson.Parse(text), type, context ?? DocumentFormats.Context);
        }
    }
}
