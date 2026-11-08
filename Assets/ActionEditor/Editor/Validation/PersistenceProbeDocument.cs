using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor.Validation
{
    public sealed class PersistenceProbeDocument : ScriptableObject
    {
        public int Version = 1;
        public string DocumentId;
        [SerializeReference] public List<PersistenceProbeEvent> Events = new();
    }

    [Serializable]
    public abstract class PersistenceProbeEvent
    {
        public string Id;
        public double Start;
        public double Duration;
        public Texture2D Resource;
    }

    [Serializable]
    public sealed class PersistenceProbeSignal : PersistenceProbeEvent
    {
        public string Message;
    }

    [Serializable]
    public sealed class PersistenceProbeInterval : PersistenceProbeEvent
    {
        public int Amount;
        public Vector3 Offset;
    }
}
