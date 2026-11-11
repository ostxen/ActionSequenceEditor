using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Storage;
using UnityEngine;

namespace NBC.ActionEditor
{
    /// <summary>The one shared document and authoring state. Window adapters own GUI and focus.</summary>
    public sealed class ActionEditorContext : IDisposable
    {
        public JsonDocumentSession Session { get; }
        public Asset Document => Session.Document;
        public TextAsset Source { get; private set; }
        public ActionEditorSelection Selection { get; } = new ActionEditorSelection();
        public ActionEditorClipboard Clipboard { get; } = new ActionEditorClipboard();
        public ActionEditorPlayback Playback { get; }
        public AssetPlayer Player => Playback.Player;
        public event Action Changed;

        public ActionEditorContext(JsonDocumentSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Playback = new ActionEditorPlayback(new AssetPlayer(() => Document));
            Session.Changed += OnDocumentChanged;
        }

        public void Open(TextAsset source)
        {
            Session.Open(source);
            Source = source;
            Playback.Stop(false);
        }

        internal void RestoreSource(TextAsset source) => Source = source;

        private void OnDocumentChanged()
        {
            Player.Reset();
            Selection.Retain(Document);
            Changed?.Invoke();
        }

        public void Dispose()
        {
            Session.Changed -= OnDocumentChanged;
            Playback.Stop(false);
            Session.Dispose();
        }
    }

    public sealed class ActionEditorSelection
    {
        private readonly List<IDirectable> items = new List<IDirectable>();
        public IDirectable[] Items => items.ToArray();
        public int Count => items.Count;
        public IDirectable First => items.Count == 0 ? null : items[0];
        public bool CanMultipleSelect { get; set; }
        public bool Contains(IDirectable item) => items.Contains(item);
        public IDirectable[] ClickCandidate(IDirectable item, bool add, bool retainSelected)
        {
            if (item == null) return Array.Empty<IDirectable>();
            if (retainSelected && items.Contains(item)) return Items;
            return add ? items.Concat(new[] { item }).Distinct().ToArray() : new[] { item };
        }

        public bool Set(IDirectable[] selected, bool allowMultiple)
        {
            var next = selected?.Where(item => item != null).ToArray() ?? Array.Empty<IDirectable>();
            var changed = next.Length != items.Count || next.Any(item => !items.Contains(item));
            if (allowMultiple && !changed) return false;
            items.Clear(); items.AddRange(next);
            CanMultipleSelect = allowMultiple && items.Count == 1 && items[0] is not Clip;
            return changed;
        }

        internal void Retain(Asset document)
        {
            items.RemoveAll(item => document == null || !document.directables.Contains(item));
            if (items.Count != 1 || items[0] is Clip) CanMultipleSelect = false;
        }
    }

    public sealed class ActionEditorClipboard
    {
        public string Json { get; internal set; }
        public Type Type { get; internal set; }
        public IDirectable Item { get; set; }
        public IDirectable CutSource { get; internal set; }
        public bool IsCut { get; set; }
    }

    public sealed class ActionEditorPlayback
    {
        public AssetPlayer Player { get; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsRange { get; set; }
        private float previousTime;
        public event Action Played;
        public event Action Stopped;

        public ActionEditorPlayback(AssetPlayer player) => Player = player ?? throw new ArgumentNullException(nameof(player));
        public void Play(float now)
        {
            if (Player.Asset == null) return;
            Played?.Invoke(); previousTime = now; IsPaused = false; IsPlaying = true;
        }
        public void Pause(bool paused) => IsPaused = paused;
        public void Stop(bool notify = true)
        {
            Player.CurrentTime = 0;
            if (notify) Stopped?.Invoke();
            Player.Reset(); IsPlaying = false; IsPaused = false;
        }
        public void StepForward(float interval) => Player.Sample(Math.Abs(Player.CurrentTime - Player.Length) < .00001f ? 0 : Player.CurrentTime + interval);
        public void StepBackward(float interval) => Player.Sample(Player.CurrentTime == 0 ? Player.Length : Player.CurrentTime - interval);
        public bool Update(float now, float timeScale)
        {
            var delta = (now - previousTime) * timeScale;
            previousTime = now;
            var document = Player.Asset;
            if (document == null || !IsPlaying || IsPaused) return false;
            var min = IsRange ? document.RangeMin : 0;
            var max = IsRange ? Mathf.Min(document.RangeMax, document.Length) : document.Length;
            if (max <= min) { Stop(); return true; }
            if (Player.CurrentTime + delta >= max)
            {
                var next = min + Mathf.Repeat(Player.CurrentTime + delta - max, max - min);
                Player.Sample(max); Player.Reset(); Player.Sample(next);
            }
            else Player.Sample(Player.CurrentTime + delta);
            return true;
        }
    }

    public sealed class ActionEditorViews
    {
        private sealed class View
        {
            public Action Repaint, Changed;
        }
        private readonly Dictionary<object, View> views = new Dictionary<object, View>();
        private object updateOwner;
        public int Count => views.Count;
        public void Attach(object owner, Action repaint, Action changed)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            views[owner] = new View { Repaint = repaint, Changed = changed };
            if (updateOwner == null) updateOwner = owner;
        }
        public void Detach(object owner)
        {
            if (owner == null) return;
            views.Remove(owner);
            if (ReferenceEquals(updateOwner, owner)) updateOwner = views.Keys.FirstOrDefault();
        }
        public bool OwnsUpdate(object owner) => owner != null && ReferenceEquals(updateOwner, owner);
        public void Repaint() { foreach (var view in views.Values.ToArray()) view.Repaint?.Invoke(); }
        public void NotifyChanged() { foreach (var view in views.Values.ToArray()) view.Changed?.Invoke(); }
    }
}
