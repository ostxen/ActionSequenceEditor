using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    public sealed class ActionEditorPreviewSession : IDisposable
    {
        private readonly Func<Type, PreviewBase> factory;
        private readonly List<PreviewBase> previews = new List<PreviewBase>();
        private List<IDirectableTimePointer> pointers;
        private readonly List<IDirectableTimePointer> starts = new List<IDirectableTimePointer>();
        public bool IsInitialized { get; private set; }
        public ActionEditorPreviewSession(Func<Type, PreviewBase> factory) => this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        public void Initialize(Asset asset)
        {
            Dispose();
            if (asset == null) return;
            pointers = new List<IDirectableTimePointer>();
            try
            {
                void Add(IDirectable target)
                {
                    var preview = factory(target.GetType());
                    if (preview == null) return;
                    previews.Add(preview); preview.SetTarget(target);
                    var start = new StartTimePointer(preview);
                    pointers.Add(start); starts.Add(start); pointers.Add(new EndTimePointer(preview));
                }
                foreach (var track in asset.AllTracks.Reverse())
                {
                    if (!track.IsActive) continue;
                    Add(track); foreach (var clip in track.Clips) Add(clip);
                }
                pointers = pointers.OrderBy(pointer => pointer.time).ToList(); IsInitialized = true;
            }
            catch { Dispose(); throw; }
        }
        public void Sample(float current, float previous, bool playing)
        {
            try
            {
                if (!playing || current > previous) foreach (var pointer in pointers) pointer.TriggerForward(current, previous);
                if (!playing || current < previous) for (var i = pointers.Count - 1; i >= 0; i--) pointers[i].TriggerBackward(current, previous);
                foreach (var pointer in starts) pointer.Update(current, previous);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            // Clear ownership before callbacks so repeated or reentrant disposal cannot release twice.
            var owned = previews.ToArray(); previews.Clear(); pointers = null; starts.Clear(); IsInitialized = false;
            foreach (var preview in owned)
            {
                try { preview.Dispose(); } catch (Exception error) { UnityEngine.Debug.LogException(error); }
            }
        }
    }
}
