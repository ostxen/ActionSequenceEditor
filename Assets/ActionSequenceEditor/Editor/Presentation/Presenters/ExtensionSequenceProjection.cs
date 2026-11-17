using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Model;
using UnityEngine;
using ActionSequenceEditor.Presentation.Contracts;

namespace ActionSequenceEditor.Presentation.Presenters
{
    // 通用扩展投影只读取声明字段，集合和复杂作者对象不进入显示边界。
    public sealed class ExtensionSequenceProjection
    {
        private readonly Func<ActionSequence> getSequence;
        private readonly Func<float> time;
        public ExtensionSequenceProjection(Func<ActionSequence> getSequence, Func<float> time)
        { this.getSequence = getSequence ?? throw new ArgumentNullException(nameof(getSequence)); this.time = time ?? throw new ArgumentNullException(nameof(time)); }
        public ExtensionSequenceState Capture()
        {
            var sequence = getSequence(); var currentTime = time();
            ExtensionNodeState Node(object value, ActionClip clip = null)
            {
                var fields = new Dictionary<string, object>();
                foreach (var description in PropertyFieldMetadata.For(value.GetType()))
                {
                    var field = description.Field; var item = field.GetValue(value);
                    if (item == null || item is string || item.GetType().IsPrimitive || item.GetType().IsEnum
                        || item is decimal || item is Vector2 || item is Vector3 || item is Vector4 || item is Color)
                        fields[field.Name] = item;
                    else if (item is AnimationCurve curve)
                        fields[field.Name] = new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
                }
                var loop = clip as ISubClipContainable;
                return new ExtensionNodeState(value.GetType(), clip?.StartTime ?? 0, clip?.Length ?? 0,
                    clip?.GetClipWeight(currentTime - clip.StartTime) ?? 1, loop?.SubClipOffset ?? 0,
                    loop?.SubClipSpeed ?? 1, loop?.SubClipLength ?? 0, fields);
            }
            return new ExtensionSequenceState(sequence?.GetType(), currentTime, sequence?.Length ?? 0, sequence == null ? null : Node(sequence),
                sequence == null ? Array.Empty<ExtensionNodeState>() : sequence.AllTracks.Where(track => track.IsActive)
                    .SelectMany(track => track.Clips).Select(clip => Node(clip, clip)).ToArray());
        }
    }
}
