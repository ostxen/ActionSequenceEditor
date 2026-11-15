using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace NBC.ActionEditor.Domain
{
    // 保存作者结构和播放范围，并重建非持久化的所属关系。
    [Serializable]
    public abstract class ActionDocument : IDirector
    {
        [SerializeField] private List<TimelineRootItem> rootItems = new();
        public IReadOnlyList<TimelineRootItem> RootItems => rootItems == null
            ? throw new InvalidDataException("$.rootItems: 根结构不能为空。") : rootItems.AsReadOnly();
        public IEnumerable<TimelineTrack> AllTracks => rootItems.SelectMany(item => item is TimelineGroup group
            ? group.Tracks : new[] { (TimelineTrack)item }.AsEnumerable());
        [SerializeField] private float length = 5f;

        [SerializeField] private float rangeMin;
        [SerializeField] private float rangeMax = 5f;

        public ActionDocument()
        {
            Init();
        }


        public List<IDirectable> directables { get; private set; }

        public float Length
        {
            get => length;
            set => length = Mathf.Max(value, 0.1f);
        }

        public float RangeMin
        {
            get => rangeMin;
            set
            {
                rangeMin = value;
                if (rangeMin < 0) rangeMin = 0;
            }
        }

        public float RangeMax
        {
            get => rangeMax;
            set
            {
                rangeMax = value;
                rangeMax = Mathf.Max(rangeMax, rangeMin + 0.001f);
            }
        }


        public void UpdateMaxTime()
        {
            var t = 0f;
            foreach (var track in AllTracks)
            {
                if (!track.IsActive) continue;
                foreach (var clip in track.Clips)
                    if (clip.EndTime > t) t = clip.EndTime;
            }

            Length = t;
        }

        public void DeleteGroup(TimelineGroup group)
        {
            rootItems.Remove(group);
            Validate();
        }

        // 根级插入允许组和轨道任意交错，所属关系在编辑完成后统一重建。
        public void InsertRoot(int index, TimelineRootItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            rootItems.Insert(index, item);
        }

        public void AddRoot(TimelineRootItem item) => InsertRoot(rootItems.Count, item);
        public bool RemoveRoot(TimelineRootItem item) => rootItems.Remove(item);
        public void ClearRoots() => rootItems.Clear();

        public void Validate()
        {
            directables = new List<IDirectable>();
            if (rootItems == null) throw new InvalidDataException("根结构不能为空。");
            var seen = new HashSet<IDirectable>();
            foreach (var item in rootItems.AsEnumerable().Reverse())
            {
                if (item is TimelineGroup group)
                {
                    Bind(group, null);
                    if (group.Tracks == null) throw new InvalidDataException("组轨道集合不能为空。");
                    foreach (var track in group.Tracks.AsEnumerable().Reverse()) BindTrack(track, group);
                }
                else if (item is TimelineTrack track) BindTrack(track, null);
                else throw new InvalidDataException("根项只能是分组或轨道。");
            }

            foreach (var item in directables) item.OnAfterDeserialize();
            UpdateMaxTime();

            void Bind(IDirectable node, IDirectable parent)
            {
                if (node == null || !seen.Add(node)) throw new InvalidDataException("结构包含空项或重复所有权。");
                node.Validate(this, parent);
                directables.Add(node);
            }
            void BindTrack(TimelineTrack track, TimelineGroup parent)
            {
                Bind(track, parent);
                if (track.Clips == null) throw new InvalidDataException("片段集合不能为空。");
                foreach (var clip in track.Clips) Bind(clip, track);
            }
        }

        public TimelineGroup AddGroup(Type type)
        {
            if (!typeof(TimelineGroup).IsAssignableFrom(type)) return null;
            var error = StructureRules.AttachmentError(this, type);
            if (error != null) throw new InvalidOperationException(error);
            var newGroup = Activator.CreateInstance(type) as TimelineGroup;
            if (newGroup != null)
            {
                newGroup.Name = "New Group";
                AddRoot(newGroup);
                Validate();
            }

            return newGroup;
        }

        public T AddGroup<T>(string name = "") where T : TimelineGroup, new()
        {
            var error = StructureRules.AttachmentError(this, typeof(T));
            if (error != null) throw new InvalidOperationException(error);
            var newGroup = new T();
            if (string.IsNullOrEmpty(name))
            {
                name = newGroup.GetType().Name;
            }

            newGroup.Name = name;
            AddRoot(newGroup);
            Validate();
            return newGroup;
        }


        public void Init()
        {
            Validate();
        }

        public void OnBeforeSerialize()
        {
            if (directables != null)
                foreach (var d in directables)
                    d.OnBeforeSerialize();

        }

        public void OnAfterDeserialize()
        {
        }
    }
}