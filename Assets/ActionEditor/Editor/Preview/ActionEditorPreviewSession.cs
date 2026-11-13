using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    // 单独拥有预览实例与边界指针，取消和重入释放使旧求值立即失效。
    public sealed class ActionEditorPreviewSession : IDisposable
    {
        private readonly Func<Type, PreviewBase> factory;
        private readonly List<PreviewBase> previews = new List<PreviewBase>();
        private List<IDirectableTimePointer> pointers;
        private readonly List<IDirectableTimePointer> starts = new List<IDirectableTimePointer>();
        private long generation;
        public bool IsInitialized { get; private set; }
        public ActionEditorPreviewSession(Func<Type, PreviewBase> factory) => this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        public void Initialize(ActionDocument asset)
        {
            Dispose();
            if (asset == null) return;
            pointers = new List<IDirectableTimePointer>();
            var version = generation;
            try
            {
                void Add(IDirectable target)
                {
                    var preview = factory(target.GetType());
                    if (generation != version)
                    {
                        // 工厂可能同步取消初始化；返回的新实例尚未进入所有权列表。
                        preview?.Dispose();
                        throw new OperationCanceledException("预览初始化已取消。");
                    }
                    if (preview == null) return;
                    previews.Add(preview); preview.SetTarget(target);
                    // 单个边界可能串联两个业务回调，第二个也必须重验资源代次。
                    Func<bool> active = () => generation == version;
                    var start = new StartTimePointer(preview, active);
                    pointers.Add(start); starts.Add(start); pointers.Add(new EndTimePointer(preview, active));
                }
                foreach (var track in asset.AllTracks.Reverse())
                {
                    if (!track.IsActive) continue;
                    Add(track); foreach (var clip in track.Clips) Add(clip);
                }
                pointers = pointers.OrderBy(pointer => pointer.time).ToList(); IsInitialized = true;
            }
            catch { if (generation == version) Dispose(); throw; }
        }
        public void Sample(float current, float previous, bool playing)
        {
            if (!IsInitialized) return;
            var version = generation;
            var activePointers = pointers;
            var activeStarts = starts.ToArray();
            try
            {
                if (!playing || current > previous)
                    foreach (var pointer in activePointers)
                    {
                        if (generation != version) return;
                        pointer.TriggerForward(current, previous);
                    }
                if (!playing || current < previous)
                    for (var i = activePointers.Count - 1; i >= 0; i--)
                    {
                        if (generation != version) return;
                        activePointers[i].TriggerBackward(current, previous);
                    }
                foreach (var pointer in activeStarts)
                {
                    if (generation != version) return;
                    pointer.Update(current, previous);
                }
            }
            catch { if (generation == version) Dispose(); throw; }
        }
        public void Dispose()
        {
            // 先清所有权再调用业务释放，重复或重入不能释放两次。
            generation++;
            var owned = previews.ToArray(); previews.Clear(); pointers = null; starts.Clear(); IsInitialized = false;
            foreach (var preview in owned)
            {
                try { preview.Dispose(); } catch (Exception error) { UnityEngine.Debug.LogException(error); }
            }
        }
    }
}
