using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor.Model
{
    public sealed class ActionDocument : ScriptableObject
    {
        public const int CurrentVersion = 1;
        public int FormatVersion = CurrentVersion;
        public string Id = StableId.New();
        public string Title = "Untitled";
        public double Duration = 5;
        public List<DocumentGroup> Groups = new();
    }

    [Serializable]
    public sealed class DocumentGroup
    {
        public string Id = StableId.New();
        public string Name = "Group";
        public bool Locked;
        public List<DocumentTrack> Tracks = new();
    }

    [Serializable]
    public sealed class DocumentTrack
    {
        public string Id = StableId.New();
        public string Name = "Track";
        public bool Locked;
        public OverlapPolicy Overlap;
        [SerializeReference] public List<TimelineEvent> Events = new();
    }

    [Serializable]
    public abstract class TimelineEvent
    {
        public string Id = StableId.New();
        [SerializeField] private string eventName = "Event";
        [SerializeField] private bool nameNull;
        public string Name { get => nameNull ? null : eventName; set { nameNull = value == null; eventName = value ?? ""; } }
        public double Start;
        public double Duration;
        public int Order;
        public bool Locked;
        public abstract string TypeId { get; }
        public abstract EventKind Kind { get; }
        public virtual EventRole Role => EventRole.Presentation;
    }

    [Serializable]
    public sealed class SignalEvent : TimelineEvent
    {
        [SerializeField] private string message = "Signal";
        [SerializeField] private bool messageNull;
        public string Message { get => messageNull ? null : message; set { messageNull = value == null; message = value ?? ""; } }
        public UnityEngine.Object Resource;
        [SerializeField] private List<string> tags = new();
        [SerializeField] private bool tagsNull;
        // Unity normalizes serialized null lists to empty. Keep the user's null choice explicitly.
        public List<string> Tags
        {
            get => tagsNull ? null : tags;
            set { tagsNull = value == null; tags = value ?? new List<string>(); }
        }
        public override string TypeId => "actioneditor.signal";
        public override EventKind Kind => EventKind.Point;
    }

    [Serializable]
    public sealed class IntervalEvent : TimelineEvent
    {
        public float Value = 1;
        public Vector3 Offset;
        public UnityEngine.Object Resource;
        public override string TypeId => "actioneditor.interval";
        public override EventKind Kind => EventKind.Interval;
    }
}
