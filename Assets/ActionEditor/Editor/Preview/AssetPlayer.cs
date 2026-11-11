using System;
using UnityEngine;

namespace NBC.ActionEditor
{
    public enum EditorPlaybackState { Stoped, PlayingForwards, PlayingBackwards }

    public class AssetPlayer
    {
        public static AssetPlayer Inst => App.Context.Player;
        private readonly Func<Asset> document;
        private readonly ActionEditorPreviewSession preview;
        private float currentTime;
        public event Action Faulted;
        public float previousTime { get; private set; }
        public bool IsInitialized => preview.IsInitialized;
        public AssetPlayer() : this(() => App.AssetData) { }
        public AssetPlayer(Func<Asset> document, Func<Type, PreviewBase> factory = null)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            preview = new ActionEditorPreviewSession(factory ?? ActionEditorTypeCatalog.CreatePreview);
        }
        public Asset Asset => document();
        public float CurrentTime { get => currentTime; set => currentTime = Mathf.Clamp(value, 0, Length); }
        public float Length => Asset?.Length ?? 0;
        public void Reset() { preview.Dispose(); previousTime = 0; }
        public void Sample() => Sample(currentTime);
        public void Sample(float time)
        {
            if (Asset == null) { Reset(); return; }
            CurrentTime = time;
            if (IsInitialized && (currentTime == 0 || currentTime == Length) && previousTime == currentTime) return;
            try
            {
                if (!IsInitialized) InitializePreviewPointers();
                preview.Sample(currentTime, previousTime, Application.isPlaying);
                previousTime = currentTime;
            }
            catch { Reset(); Faulted?.Invoke(); throw; }
        }
        public void InitializePreviewPointers() { previousTime = 0; preview.Initialize(Asset); }
    }
}