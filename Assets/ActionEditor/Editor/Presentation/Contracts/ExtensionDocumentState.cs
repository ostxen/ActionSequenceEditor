using System;
using System.Collections.Generic;

namespace NBC.ActionEditor.Presentation.Contracts
{
    // 扩展显示帧只包含值副本，节点不提供作者身份或修改入口。
    public sealed class ExtensionNodeState
    {
        public Type Type { get; }
        public float Start { get; }
        public float Length { get; }
        public float End => Start + Length;
        public float Weight { get; }
        public float LoopOffset { get; }
        public float LoopSpeed { get; }
        public float LoopLength { get; }
        private readonly IReadOnlyDictionary<string, object> fields;
        public ExtensionNodeState(Type type, float start, float length, float weight, float loopOffset, float loopSpeed,
            float loopLength, IDictionary<string, object> fields)
        {
            Type = type; Start = start; Length = length; Weight = weight;
            LoopOffset = loopOffset; LoopSpeed = loopSpeed; LoopLength = loopLength;
            this.fields = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>(fields));
        }
        public T Field<T>(string name, T fallback = default) => fields.TryGetValue(name, out var value) && value is T result ? result : fallback;
    }
    public sealed class ExtensionDocumentState
    {
        public Type Type { get; }
        public float Time { get; }
        public float Length { get; }
        public ExtensionNodeState Document { get; }
        public IReadOnlyList<ExtensionNodeState> Clips { get; }
        public ExtensionDocumentState(Type type, float time, float length, ExtensionNodeState document, ExtensionNodeState[] clips)
        { Type = type; Time = time; Length = length; Document = document; Clips = Array.AsReadOnly((ExtensionNodeState[])clips.Clone()); }
    }

}
