using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBC.ActionEditor.Domain
{
    // 保存一组有序轨道及其作者状态，不允许嵌套分组。
    [Name("Default Group")]
    [Serializable]
    public class TimelineGroup : TimelineRootItem, IDirectable
    {
        [SerializeField] [HideInInspector] private string name;
        [SerializeField] private List<TimelineTrack> tracks = new();
        [SerializeField] [HideInInspector] private bool active = true;
        [SerializeField] [HideInInspector] private bool isLocked;

        public int ActorId;

        public List<TimelineTrack> Tracks
        {
            get => tracks;
            set => tracks = value;
        }

        public IDirector Root { get; private set; }
        IDirectable IDirectable.Parent => null;

        IEnumerable<IDirectable> IDirectable.Children => Tracks;
        private GameObject _actor;

        [HideInInspector]
        public GameObject Actor
        {
            get { return _actor; }
            set
            {
                _actor = value;
            }
        }

        public int StartTimeInt => 0;
        public int EndTimeInt => 0;

        float IDirectable.StartTime => 0;

        float IDirectable.EndTime => Root.Length;

        public float BlendIn
        {
            get => 0;
            set { }
        }

        public float BlendOut
        {
            get => 0;
            set { }
        }

        bool IDirectable.CanCrossBlend => false;

        public string Name
        {
            get => name;
            set => name = value;
        }

        public bool IsActive
        {
            get => active;
            set
            {
                if (active == value) return;
                active = value;
                if (Root != null) Root?.Validate();
            }
        }

        public bool IsLocked
        {
            get => isLocked;
            set => isLocked = value;
        }


        public void Validate(IDirector _root, IDirectable _parent)
        {
            Root = _root;
        }

        public bool Initialize()
        {
            return true;
        }

        public virtual void OnBeforeSerialize()
        {
        }

        public virtual void OnAfterDeserialize()
        {
        }


        public bool CanAddTrack(TimelineTrack track)
        {
            return track != null && CanAddTrackOfType(track.GetType());
        }

        public bool CanAddTrackOfType(Type type)
        {
            return StructureRules.AttachmentError(this, type) == null;
        }

        public T AddTrack<T>(string _name = null) where T : TimelineTrack
        {
            return (T)AddTrack(typeof(T), _name);
        }

        public TimelineTrack AddTrack(Type type, string _name = null)
        {
            var error = StructureRules.AttachmentError(this, type);
            if (error != null) throw new InvalidOperationException(error);
            var newTrack = Activator.CreateInstance(type);
            if (newTrack is TimelineTrack track)
            {
                track.Name = type.Name;
                Tracks.Add(track);

                Root?.Validate();

                return track;
            }

            return null;
        }

        public int InsertTrack<T>(T track, int index) where T : TimelineTrack
        {
            if (tracks.Contains(track))
            {
                DeleteTrack(track);
            }

            if (index >= tracks.Count)
            {
                index = tracks.Count;
                tracks.Add(track);
            }
            else
            {
                if (index < 0) index = 0;
                tracks.Insert(index, track);
            }

            Root?.Validate();
            return index;
        }

        public void DeleteTrack(TimelineTrack track)
        {
            Tracks.Remove(track);

            Root?.Validate();
        }

        public int GetTrackIndex(TimelineTrack track)
        {
            return tracks.FindIndex(t => t == track);
        }
    }
}