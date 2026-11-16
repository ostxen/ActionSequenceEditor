using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace NBC.ActionEditor.Model
{
    // 保存作者结构和播放范围，并重建非持久化的所属关系。
    [Serializable]
    public abstract class ActionSequence : IDirector
    {
        // 保留持久字段名rootItems，公开API统一使用容器术语。
        [SerializeField] private List<ActionContainer> rootItems = new();
        public IReadOnlyList<ActionContainer> RootContainers => rootItems == null
            ? throw new InvalidDataException("$.rootItems: 根结构不能为空。") : rootItems.AsReadOnly();
        public IEnumerable<ActionTrack> AllTracks => rootItems.SelectMany(item => item is ActionGroup group
            ? group.Tracks : new[] { (ActionTrack)item }.AsEnumerable());
        [SerializeField] private float length = 5f;

        [SerializeField] private float rangeMin;
        [SerializeField] private float rangeMax = 5f;

        public ActionSequence()
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

        public void DeleteGroup(ActionGroup group)
        {
            rootItems.Remove(group);
            Validate();
        }

        // 根级插入允许组和轨道任意交错，所属关系在编辑完成后统一重建。
        public void InsertRootContainer(int index, ActionContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            rootItems.Insert(index, container);
        }

        public void AddRootContainer(ActionContainer container) => InsertRootContainer(rootItems.Count, container);
        public bool RemoveRootContainer(ActionContainer container) => rootItems.Remove(container);
        public void ClearRootContainers() => rootItems.Clear();

        public void Validate()
        {
            directables = new List<IDirectable>();
            if (rootItems == null) throw new InvalidDataException("根结构不能为空。");
            var seen = new HashSet<IDirectable>();
            foreach (var item in rootItems.AsEnumerable().Reverse())
            {
                if (item is ActionGroup group)
                {
                    Bind(group, null);
                    if (group.Tracks == null) throw new InvalidDataException("组轨道集合不能为空。");
                    foreach (var track in group.Tracks.AsEnumerable().Reverse()) BindTrack(track, group);
                }
                else if (item is ActionTrack track) BindTrack(track, null);
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
            void BindTrack(ActionTrack track, ActionGroup parent)
            {
                Bind(track, parent);
                if (track.Clips == null) throw new InvalidDataException("片段集合不能为空。");
                foreach (var clip in track.Clips) Bind(clip, track);
            }
        }

        public ActionGroup AddGroup(Type type)
        {
            if (!typeof(ActionGroup).IsAssignableFrom(type)) return null;
            var error = StructureRules.AttachmentError(this, type);
            if (error != null) throw new InvalidOperationException(error);
            var newGroup = Activator.CreateInstance(type) as ActionGroup;
            if (newGroup != null)
            {
                newGroup.Name = "New Group";
                AddRootContainer(newGroup);
                Validate();
            }

            return newGroup;
        }

        public T AddGroup<T>(string name = "") where T : ActionGroup, new()
        {
            var error = StructureRules.AttachmentError(this, typeof(T));
            if (error != null) throw new InvalidOperationException(error);
            var newGroup = new T();
            if (string.IsNullOrEmpty(name))
            {
                name = newGroup.GetType().Name;
            }

            newGroup.Name = name;
            AddRootContainer(newGroup);
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