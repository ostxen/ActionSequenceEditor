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
        public string Name = "Event";
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
        public string Message = "Signal";
        public UnityEngine.Object Resource;
        public List<string> Tags = new();
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
