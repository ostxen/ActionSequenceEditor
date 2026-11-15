using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Domain;
using UnityEngine;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 通用扩展投影只读取声明字段，集合和复杂作者对象不进入显示边界。
    public sealed class ExtensionDocumentPresenter
    {
        private readonly Func<ActionDocument> document;
        private readonly Func<float> time;
        public ExtensionDocumentPresenter(Func<ActionDocument> document, Func<float> time)
        { this.document = document ?? throw new ArgumentNullException(nameof(document)); this.time = time ?? throw new ArgumentNullException(nameof(time)); }
        public ExtensionDocumentState Capture()
        {
            var asset = document(); var current = time();
            ExtensionNodeState Node(object value, TimelineClip clip = null)
            {
                var fields = new Dictionary<string, object>();
                foreach (var description in PropertyDescription.For(value.GetType()))
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
                    clip?.GetClipWeight(current - clip.StartTime) ?? 1, loop?.SubClipOffset ?? 0,
                    loop?.SubClipSpeed ?? 1, loop?.SubClipLength ?? 0, fields);
            }
            return new ExtensionDocumentState(asset?.GetType(), current, asset?.Length ?? 0, asset == null ? null : Node(asset),
                asset == null ? Array.Empty<ExtensionNodeState>() : asset.AllTracks.Where(track => track.IsActive)
                    .SelectMany(track => track.Clips).Select(clip => Node(clip, clip)).ToArray());
        }
    }
}
