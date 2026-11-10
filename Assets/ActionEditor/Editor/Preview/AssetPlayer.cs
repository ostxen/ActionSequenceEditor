using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBC.ActionEditor
{
    public enum EditorPlaybackState
    {
        Stoped,
        PlayingForwards,
        PlayingBackwards
    }
    
    public class AssetPlayer
    {
        public static AssetPlayer Inst => App.Context.Player;
        private readonly Func<Asset> document;
        public AssetPlayer() : this(() => App.AssetData) { }
        public AssetPlayer(Func<Asset> document) => this.document = document ?? throw new ArgumentNullException(nameof(document));

        private List<IDirectableTimePointer> timePointers;

        /// <summary>
        /// 预览器
        /// </summary>
        private List<IDirectableTimePointer> unsortedStartTimePointers;

        private float playTimeMin;
        private float playTimeMax;
        private float currentTime;

        public float previousTime { get; private set; }

        private bool preInitialized;
        private readonly List<PreviewBase> previews = new List<PreviewBase>();
        public bool IsInitialized => preInitialized;

        public void Reset()
        {
            foreach (var preview in previews)
            {
                try { preview.Dispose(); } catch (Exception e) { Debug.LogException(e); }
            }
            previews.Clear(); timePointers = null; unsortedStartTimePointers = null;
            preInitialized = false; previousTime = 0;
        }

        public Asset Asset => document();

        /// <summary>
        /// 当前时间
        /// </summary>
        public float CurrentTime
        {
            get => currentTime;
            set => currentTime = Mathf.Clamp(value, 0, Length);
        }
        
        public float Length
        {
            get
            {
                if (Asset != null)
                {
                    return Asset.Length;
                }

                return 0;
            }
        }

        public void Sample()
        {
            Sample(currentTime);
        }

        public void Sample(float time)
        {
            if (Asset == null) { Reset(); return; }
            CurrentTime = time;
            if (preInitialized && (currentTime == 0 || currentTime == Length) && previousTime == currentTime)
            {
                return;
            }
            
            if (!preInitialized)
            {
                InitializePreviewPointers();
            }


            if (timePointers != null)
            {
                InternalSamplePointers(currentTime, previousTime);
            }

            previousTime = currentTime;
        }

        void InternalSamplePointers(float currentTime, float previousTime)
        {
            if (!Application.isPlaying || currentTime > previousTime)
            {
                foreach (var t in timePointers)
                {
                    try
                    {
                        t.TriggerForward(currentTime, previousTime);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }


            if (!Application.isPlaying || currentTime < previousTime)
            {
                for (var i = timePointers.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        timePointers[i].TriggerBackward(currentTime, previousTime);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            if (unsortedStartTimePointers != null)
            {
                foreach (var t in unsortedStartTimePointers)
                {
                    try
                    {
                        t.Update(currentTime, previousTime);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        /// <summary>
        /// 初始化时间指针预览器
        /// </summary>
        public void InitializePreviewPointers()
        {
            Reset();
            if (Asset == null) return;
            timePointers = new List<IDirectableTimePointer>();
            unsortedStartTimePointers = new List<IDirectableTimePointer>();

            Dictionary<Type, Type> typeDic = new Dictionary<Type, Type>();
            var childs = EditorTools.GetTypeMetaDerivedFrom(typeof(PreviewBase));
            foreach (var t in childs)
            {
                var arrs = t.type.GetCustomAttributes(typeof(CustomPreviewAttribute), true);
                foreach (var arr in arrs)
                {
                    if (arr is CustomPreviewAttribute c)
                    {
                        var bindT = c.PreviewType;
                        var iT = t.type;
                        if (!typeDic.ContainsKey(bindT))
                        {
                            if (!iT.IsAbstract) typeDic[bindT] = iT;
                        }
                        else
                        {
                            var old = typeDic[bindT];
                            //如果不是抽象类，且是子类就更新
                            if (!iT.IsAbstract && iT.IsSubclassOf(old))
                            {
                                typeDic[bindT] = iT;
                            }
                        }
                    }
                }
            }

            foreach (var group in Asset.groups.AsEnumerable().Reverse())
            {
                if (!group.IsActive) continue;
                foreach (var track in group.Tracks.AsEnumerable().Reverse())
                {
                    if (!track.IsActive) continue;
                    var tType = track.GetType();
                    if (typeDic.TryGetValue(tType, out var t1))
                    {
                        if (Activator.CreateInstance(t1) is PreviewBase preview)
                        {
                            preview.SetTarget(track);
                            previews.Add(preview);
                            var p3 = new StartTimePointer(preview);
                            timePointers.Add(p3);
                
                            unsortedStartTimePointers.Add(p3);
                            timePointers.Add(new EndTimePointer(preview));
                        }
                    }
                
                    foreach (var clip in track.Clips)
                    {
                        var cType = clip.GetType();
                        if (typeDic.TryGetValue(cType, out var t))
                        {
                            if (Activator.CreateInstance(t) is PreviewBase preview)
                            {
                                preview.SetTarget(clip);
                                previews.Add(preview);
                                var p3 = new StartTimePointer(preview);
                                timePointers.Add(p3);
                
                                unsortedStartTimePointers.Add(p3);
                                timePointers.Add(new EndTimePointer(preview));
                            }
                        }
                    }
                }
            }

            preInitialized = true;
            timePointers = timePointers.OrderBy(p => p.time).ToList();
        }
    }
}