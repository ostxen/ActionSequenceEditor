using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBC.ActionEditor.Domain
{
    // 保存有序片段及作者状态，可直接归文档或归分组。
    [Serializable]
    [Attachable(typeof(TimelineGroup))]
    public abstract class TimelineTrack : TimelineRootItem, IDirectable
    {
        [SerializeField] private List<TimelineClip> actionClips = new();

        [SerializeField] [HideInInspector] private string name;
        [SerializeField] [HideInInspector] private bool active = true;
        [SerializeField] [HideInInspector] private bool isLocked;
        [SerializeField] private Color color = Color.white;

        public Color Color => color.a > 0.1f ? color : Color.white;


        public virtual string info => string.Empty;

        public List<TimelineClip> Clips
        {
            get => actionClips;
            set => actionClips = value;
        }


        public virtual void OnBeforeSerialize()
        {
        }

        public virtual void OnAfterDeserialize()
        {
        }

        private IDirector root;
        public IDirector Root => Parent?.Root ?? root;
        public IDirectable Parent { get; private set; }

        public TimelineGroup Group => Parent as TimelineGroup;
        
        public string Name
        {
            get => name;
            set => name = value;
        }

        IEnumerable<IDirectable> IDirectable.Children => Clips;

        public GameObject Actor => Parent?.Actor;

        public virtual bool IsActive
        {
            get => (Parent == null || Parent.IsActive) && active;
            set
            {
                if (active != value)
                {
                    active = value;
                    if (Root != null) Root.Validate();
                }
            }
        }

        public virtual bool IsLocked
        {
            get => isLocked || Parent?.IsLocked == true;
            set => isLocked = value;
        }


        public int StartTimeInt => 0;
        public int EndTimeInt => 0;

        public virtual float StartTime
        {
            get => Parent?.StartTime ?? 0;
            set { }
        }


        public virtual float EndTime
        {
            get => Parent?.EndTime ?? Root?.Length ?? 0;
            set { }
        }

        public virtual float BlendIn
        {
            get => 0f;
            set { }
        }


        public virtual float BlendOut
        {
            get => 0f;
            set { }
        }

        public bool CanCrossBlend => false;

        public bool Initialize()
        {
            return true;
        }

        public void Validate(IDirector root, IDirectable parent)
        {
            this.root = root;
            Parent = parent;
            OnAfterValidate();
        }

        protected virtual void OnCreate()
        {
        }

        protected virtual void OnAfterValidate()
        {
        }


        public void PostCreate(IDirectable parent)
        {
            Parent = parent;
            OnCreate();
        }


        public T AddClip<T>(float time) where T : TimelineClip
        {
            return (T)AddClip(typeof(T), time);
        }

        public TimelineClip AddClip(Type type, float time)
        {
            var error = StructureRules.AttachmentError(this, type);
            if (error != null) throw new InvalidOperationException(error);
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            var catAtt =
                type.GetCustomAttributes(typeof(CategoryAttribute), true).FirstOrDefault() as CategoryAttribute;
            if (catAtt != null && Clips.Count == 0) Name = catAtt.category + " Track";

            var newAction = Activator.CreateInstance(type) as TimelineClip;


            if (newAction != null)
            {

                newAction.StartTime = time;
                newAction.Name = type.Name;
                Clips.Add(newAction);
                newAction.PostCreate(this);

                var nextAction = Clips.FirstOrDefault(a => a.StartTime > newAction.StartTime);
                if (nextAction != null) newAction.EndTime = Mathf.Min(newAction.EndTime, nextAction.StartTime);

                Root.Validate();
            }

            return newAction;
        }

        public TimelineClip AddClip(TimelineClip clip)
        {
            if (clip != null)
            {
                var error = StructureRules.AttachmentError(this, clip.GetType(), clip);
                if (error != null) throw new InvalidOperationException(error);
            }
            if (clip != null && clip.CanValidTime(this, clip.StartTime, clip.EndTime))
            {
                if (clip.Parent != null && clip.Parent is TimelineTrack track)
                {
                    track.DeleteAction(clip);
                }

                Clips.Add(clip);
                Root.Validate();
            }

            return clip;
        }

        public void DeleteAction(TimelineClip action)
        {
            Clips.Remove(action);
            Root.Validate();
        }
    }
}