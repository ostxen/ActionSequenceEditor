using ActionSequenceEditor.Application.Ports;
using ActionSequenceEditor.Model;
using System;
using System.Linq;
using ActionSequenceEditor.Serialization;

namespace ActionSequenceEditor.Infrastructure.Serialization
{
    // 隔离序列化回调与源图，并以同一受控值树生成作者比较键。
    public static class SequenceCopy
    {
        public static ActionSequence Capture(ActionSequence source, bool beforeSerialize, SequenceFormatContext context = null)
        {
            context ??= SequenceFormats.Context;
            SequenceGraphValidation.RequireValid(source);
            var tree = JsonValueConverter.Capture(source, typeof(ActionSequence), context);
            var clone = (ActionSequence)JsonValueConverter.Restore(tree, typeof(ActionSequence), context);
            SequenceGraphValidation.Initialize(clone, context);
            if (beforeSerialize) clone.OnBeforeSerialize();
            SequenceGraphValidation.RequireValid(clone);
            clone.UpdateMaxTime();
            JsonValueConverter.Capture(clone, typeof(ActionSequence), context);
            return clone;
        }
        public static string GetContentKey(ActionSequence source, SequenceFormatContext context = null)
        {
            context ??= SequenceFormats.Context;
            SequenceGraphValidation.RequireValid(source);
            var tree = JsonValueConverter.Capture(source, typeof(ActionSequence), context, true);
            var duration = 0f;
            foreach (var track in source.AllTracks)
                if (track.Group == null || track.Group.IsActive)
                    if ((bool)JsonValueConverter.Fields(track.GetType()).Single(f => f.DeclaringType == typeof(ActionTrack) && f.Name == "active").GetValue(track))
                        foreach (var clip in track.Clips) duration = Math.Max(duration, clip.EndTime);
            var members = tree.Members.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            members["length"] = JsonValueConverter.Capture(Math.Max(.1f, duration), typeof(float), context);
            return JsonValue.Object(members).ContentKey();
        }
        public static string CaptureFragment(IDirectable item, SequenceFormatContext context = null)
        {
            if (item is not ActionGroup && item is not ActionTrack && item is not ActionClip) throw new InvalidOperationException("Unsupported clipboard node.");
            return JsonText.Print(JsonValueConverter.Capture(item, item.GetType(), context ?? SequenceFormats.Context), true);
        }
        public static IDirectable RestoreFragment(Type type, string text, SequenceFormatContext context = null)
        {
            if (!typeof(ActionGroup).IsAssignableFrom(type) && !typeof(ActionTrack).IsAssignableFrom(type) && !typeof(ActionClip).IsAssignableFrom(type))
                throw new InvalidOperationException("Unsupported clipboard type.");
            return (IDirectable)JsonValueConverter.Restore(JsonText.Parse(text), type, context ?? SequenceFormats.Context);
        }
    }

}
