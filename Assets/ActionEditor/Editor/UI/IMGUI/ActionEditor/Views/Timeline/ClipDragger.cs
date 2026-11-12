using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Events;
using UnityEngine;
using NBC.ActionEditor.Storage;

namespace NBC.ActionEditor
{
    public enum ItemDragType
    {
        None,
        Pos,
        Track,
        StretchStart,
        StretchEnd,
    }

    struct DragBeginInfo
    {
        public float StartTime;
        public float Length;
        public float BlendIn;
        public float BlendOut;
        public Track Parent;

        public DragBeginInfo(Clip clip)
        {
            StartTime = clip.StartTime;
            Length = clip.Length;
            BlendIn = clip.BlendIn;
            BlendOut = clip.BlendOut;
            Parent = clip.Parent as Track;
        }
    }

    public sealed class OriginalTimelineGesture
    {
        private ItemDragType _dragType = ItemDragType.None;
        private JsonEditTransaction edit;
        private readonly object editOwner = new object();
        private bool bypassMagnet;
        public IDirectable MoveToItem { get; private set; }

        public ItemDragType DragType
        {
            get => _dragType;
            set
            {
                _dragType = value;
                if (value != ItemDragType.None)
                {
                    CacheDragClipInfo();
                }
                else
                {
                    CheckDragEndInfo();
                }
            }
        }


        #region Tracks

        private readonly List<IDirectable> _tracks = new List<IDirectable>();

        public void TryResetTracks()
        {
            _tracks.Clear();
            var assets = App.AssetData;
            if (assets == null) return;
            foreach (var group in assets.groups)
            {
                _tracks.Add(group);
                foreach (var track in group.Tracks)
                {
                    _tracks.Add(track);
                }
            }
        }

        #endregion

        #region Drag Time Info

        /// <summary>
        /// 开始拖动时的位置
        /// </summary>
        private readonly Dictionary<Clip, DragBeginInfo> _dragBeginStartTime =
            new Dictionary<Clip, DragBeginInfo>();

        public readonly List<Clip> DragItems = new List<Clip>();

        private void CacheDragClipInfo()
        {
            DragItems.Clear();
            _dragBeginStartTime.Clear();
            foreach (var select in App.SelectItems)
            {
                if (select is Clip clip)
                {
                    DragItems.Add(clip);
                    _dragBeginStartTime[clip] = new DragBeginInfo(clip);
                }
            }
        }

        /// <summary>
        /// 拖动完毕，检查数据合法性
        /// </summary>
        private void CheckDragEndInfo()
        {
            if (DragItems == null) return;
            if (!TimelineCandidates.Valid(DragItems))
            {
                foreach (var clip in DragItems)
                {
                    if (clip == null) continue;
                    if (_dragBeginStartTime.TryGetValue(clip, out var dragBeginInfo))
                    {
                        clip.StartTime = dragBeginInfo.StartTime;
                        if (clip.CanScale()) clip.Length = dragBeginInfo.Length;
                        clip.BlendIn = dragBeginInfo.BlendIn;
                        clip.BlendOut = dragBeginInfo.BlendOut;
                        if (clip.Parent != dragBeginInfo.Parent)
                        {
                            dragBeginInfo.Parent.AddClip(clip);
                        }
                    }
                    else
                    {
                        //找一个空白的地方
                        Debug.LogError("错误，需要找一个空白地方");
                    }
                }
            }
        }

        #endregion

        #region Drag

        public void OnBeginDrag(PointerEventData eventData, JsonEditTransaction transaction = null)
        {
            bypassMagnet = eventData.Shift;
            if (!eventData.IsLeft() || App.SelectItems.Any(i => i.IsLocked)) return;
            var asset = App.AssetData;
            if (asset == null) return;
            Clip stretchClip = null;
            var dragType = ItemDragType.Pos;
            if (App.SelectCount == 1)
            {
                if (App.FistSelect is Clip clip && clip.CanScale())
                {
                    var x = eventData.MousePosition.x - Styles.TimelineLeftTotalWidth;
                    var time = asset.PosToTime(x, ActionEditorHost.Width);
                    var gapTime = asset.WidthToTime(Styles.ClipScaleRectWidth, ActionEditorHost.Width);
                    if (time >= clip.StartTime && time <= clip.StartTime + gapTime)
                    {
                        dragType = ItemDragType.StretchStart;
                    }
                    else if (time <= clip.EndTime && time >= clip.EndTime - gapTime)
                    {
                        dragType = ItemDragType.StretchEnd;
                    }

                    stretchClip = clip;
                }
                else if (App.FistSelect is Track track)
                {
                    if (eventData.MousePosition.x < Styles.TimelineLeftWidth)
                    {
                        dragType = ItemDragType.Track;
                    }
                }
            }
            else
            {
                var pos = new Vector2(eventData.MousePosition.x - Styles.TimelineLeftTotalWidth,
                    eventData.MousePosition.y);
                if (!ClipDrawer.ClipContainsByRealRect(pos)) return;
            }

            edit = transaction?.IsActive == true ? transaction : App.Session.BeginEdit(editOwner);
            if (edit == null) return;
            if (dragType == ItemDragType.Pos)
            {
                OnBeginDragPos(eventData);
            }
            else if (dragType == ItemDragType.Track)
            {
                OnBeginDragTrack(eventData);
            }
            else
            {
                OnBeginDragLength(eventData, stretchClip, dragType == ItemDragType.StretchStart);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            bypassMagnet = eventData.Shift;
            if (edit?.IsActive != true) return;
            try { edit.Update(() => UpdateDrag(eventData)); }
            catch { Cancel(); throw; }
        }

        private void UpdateDrag(PointerEventData eventData)
        {
            if (DragType == ItemDragType.Pos)
            {
                OnDragPos(eventData);
            }
            else if (DragType == ItemDragType.Track)
            {
                OnDragTrack(eventData);
            }
            else if (DragType >= ItemDragType.StretchStart)
            {
                OnDragLength(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (edit?.IsActive != true) { Cancel(); return; }
            try { EndDrag(eventData); }
            catch { Cancel(); throw; }
        }

        private void EndDrag(PointerEventData eventData)
        {
            if (DragType == ItemDragType.Track)
            {
                OnEndDragTrack(eventData);
            }

            DragType = ItemDragType.None;
            edit.Commit("Drag timeline items"); edit = null;
            eventData.StopPropagation();
        }

        public void Cancel()
        {
            var token = edit; edit = null;
            _dragType = ItemDragType.None; DragItems.Clear(); _dragBeginStartTime.Clear();
            NowDragClips.Clear(); DragOffsetDictionary.Clear(); MoveToItem = null;
            token?.Cancel();
        }

        internal void Cancel(JsonEditTransaction owner)
        {
            if (ReferenceEquals(edit, owner)) Cancel();
        }

        #endregion

        #region Drag position

        private readonly List<Clip> NowDragClips = new List<Clip>();

        private readonly Dictionary<Clip, float> DragOffsetDictionary =
            new Dictionary<Clip, float>();

        private void ReloadClipItems(IDirectable[] selectItems)
        {
            TryResetTracks();
            NowDragClips.Clear();
            if (selectItems == null || selectItems.Length < 1) return;
            foreach (var item in selectItems)
            {
                if (item is not Clip clip) continue;
                NowDragClips.Add(clip);
            }

            CacheMagnetSnapInfo(selectItems);
        }


        /// <summary>
        /// 计算按下时的偏移量 Calculates the offset when pressed
        /// </summary>
        private void CacheDragOffset(Vector2 vector2)
        {
            var asset = App.AssetData;
            var dragX = asset.PosToTime(vector2.x, ActionEditorHost.Width);
            foreach (var clipItem in NowDragClips)
            {
                var offset = dragX - clipItem.StartTime;
                DragOffsetDictionary[clipItem] = offset;
            }
        }

        private void OnBeginDragPos(PointerEventData eventData)
        {
            DragType = ItemDragType.Pos;
            ReloadClipItems(App.SelectItems);
            CacheDragOffset(eventData.MousePosition);
        }

        private void OnDragPos(PointerEventData eventData)
        {
            if (DragType != ItemDragType.Pos) return;
            if (NowDragClips.Count < 1) return;
            var asset = App.AssetData;
            var nowPos = eventData.MousePosition;
            //缓存拖动需要的信息 cache drag result 
            List<float> subTimes = new List<float>();
            foreach (var clipItem in NowDragClips)
            {
                if (DragOffsetDictionary.TryGetValue(clipItem, out var offsetTime))
                {
                    var cursorTime = asset.SnapTime(asset.PosToTime(nowPos.x, ActionEditorHost.Width) - offsetTime);
                    cursorTime = CheckMagnetSnapTime(cursorTime, clipItem.Length);
                    var delta = cursorTime - _dragBeginStartTime[clipItem].StartTime;
                    subTimes.Add(NowDragClips.Count == 1 ? delta : asset.SnapTime(delta));
                }
            }

            var max = TimelineCandidates.ClampMoveDelta(subTimes.Max(), NowDragClips.Select(clip => _dragBeginStartTime[clip].StartTime));
            foreach (var clipItem in NowDragClips)
            {
                var newTime = _dragBeginStartTime[clipItem].StartTime + max;
                clipItem.StartTime = newTime;
                CheckBlendInOut(clipItem);
            }

            eventData.StopPropagation();
        }


        #region Clip Jump Track

        //Todo:Add clip jump to other track

        #endregion

        #endregion

        #region Drag Lenght

        private float _dragStretchOffset;
        private float _limitMinTime;
        private float _limitMaxTime;
        private float _dragStretchTime;
        private float _dragStretchLength;

        private void CacheDragStretchOffset(Clip clip, Vector2 vector2)
        {
            _dragStretchOffset = vector2.x;
            _dragStretchTime = clip.StartTime;
            _dragStretchLength = clip.Length;
        }

        private void OnBeginDragLength(PointerEventData eventData, Clip clip, bool isStart)
        {
            DragType = isStart ? ItemDragType.StretchStart : ItemDragType.StretchEnd;
            TryResetTracks();
            CacheMagnetSnapInfo(App.SelectItems);
            CacheDragStretchOffset(clip, eventData.MousePosition);

            if (isStart)
            {
                _limitMinTime = 0;
                _limitMaxTime = clip.EndTime - 0.1f;
                var prevClip = clip.GetPreviousSibling();
                if (prevClip != null)
                {
                    _limitMinTime = prevClip.EndTime;
                }
            }
            else
            {
                _limitMinTime = clip.StartTime + 0.1f;
                _limitMaxTime = int.MaxValue;
                var nextClip = clip.GetNextSibling();
                if (nextClip != null)
                {
                    _limitMaxTime = nextClip.StartTime;
                }
            }
        }

        private void OnDragLength(PointerEventData eventData)
        {
            if (DragType < ItemDragType.StretchStart) return;

            var asset = App.AssetData;
            var clipData = App.FistSelect as Clip;
            if (clipData == null) return;

            var nowOffset = eventData.MousePosition.x;
            var offsetX = nowOffset - _dragStretchOffset; //Mathf.Abs(nowOffset - _dragStretchOffset);
            var offsetTime = asset.SnapTime(asset.WidthToTime(offsetX, ActionEditorHost.Width));

            var newTime = _dragStretchTime + offsetTime;
            if (DragType == ItemDragType.StretchEnd)
            {
                newTime += _dragStretchLength;
            }

            newTime = CheckMagnetSnapTime(newTime, 0);
            newTime = asset.SnapTime(newTime);
            var candidate = TimelineCandidates.Resize(new Vector2(_dragStretchTime, _dragStretchLength),
                DragType == ItemDragType.StretchStart, newTime, .1f, _limitMinTime, _limitMaxTime, value => asset.SnapTime(value));
            if (DragType == ItemDragType.StretchStart) { clipData.StartTime = candidate.x; clipData.Length = candidate.y; }
            else clipData.EndTime = candidate.x + candidate.y;

            App.Refresh();
        }

        #endregion

        #region Drag Track

        private void OnBeginDragTrack(PointerEventData eventData)
        {
            DragType = ItemDragType.Track;
        }

        private void OnDragTrack(PointerEventData eventData)
        {
            CheckMoveToTrack(eventData);
        }

        private void OnEndDragTrack(PointerEventData eventData)
        {
            CheckMoveToTrack(eventData, true);
        }

        private void CheckMoveToTrack(PointerEventData eventDat, bool confirm = false)
        {
            if (App.FistSelect is not Track targetTrack) return;
            var trackItem = TimelineMiddleView.GetItem(eventDat.MousePosition);

            if (trackItem != null)
            {
                var halfY = trackItem.Position.y + trackItem.Position.height * 0.5f;
                if (eventDat.MousePosition.y < halfY)
                {
                    trackItem = TimelineMiddleView.GetItem(new Vector2(eventDat.MousePosition.x,
                        eventDat.MousePosition.y - Styles.LineHeight * 0.45f));
                }
            }

            if (trackItem == null)
            {
                var last = TimelineMiddleView.GetLastItem();
                if (last != null && eventDat.MousePosition.y > last.Position.y)
                {
                    trackItem = last;
                }
            }


            if (trackItem != null)
            {
                if (targetTrack == trackItem.Data) return;
                // if(trackItem.Data == )
                var old = MoveToItem;
                MoveToItem = trackItem.Data;
                if (old != trackItem.Data)
                {
                    App.Repaint();
                }

                if (confirm)
                {
                    if (trackItem.Data is Group group)
                    {
                        NBC.ActionEditor.Storage.JsonEditCommands.MoveTrack(targetTrack, group, 0);
                    }
                    else if (trackItem.Data is Track track)
                    {
                        var index = track.Group.GetTrackIndex(track);
                        if (track.Group == targetTrack.Group)
                        {
                            var selfIndex = targetTrack.Group.GetTrackIndex(targetTrack);
                            if (selfIndex < index)
                            {
                                index = track.Group.GetTrackIndex(track);
                            }
                            else
                            {
                                index += 1;
                            }
                        }
                        else
                        {
                            index += 1;
                        }

                        NBC.ActionEditor.Storage.JsonEditCommands.MoveTrack(targetTrack, track.Group, index);
                    }

                    App.Refresh();
                }


                // Debug.LogError($"当前所在track位置={trackItem.Data.Name}");
            }

            if (confirm)
            {
                MoveToItem = null;
            }
        }

        #endregion

        #region Magnet Snap

        private float[] magnetSnapTimesCache;
        private float magnetSnapInterval;


        public bool HasMagnetSnapTime(float time)
        {
            if (magnetSnapTimesCache == null) return false;
            foreach (var t in magnetSnapTimesCache)
            {
                if (Math.Abs(t - time) < 0.00001f)
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheMagnetSnapInfo(IDirectable[] selectItems)
        {
            magnetSnapInterval = App.AssetData.ViewTime * 0.01f;
            magnetSnapTimesCache = TimelineCandidates.References(_tracks.OfType<Track>().SelectMany(track => track.Clips),
                selectItems.OfType<Clip>(), AssetPlayer.Inst.Length, AssetPlayer.Inst.CurrentTime, true);
        }


        private float CheckMagnetSnapTime(float startTime, float length)
        {
            if (!Prefs.MagnetSnapping || bypassMagnet || magnetSnapTimesCache == null) return startTime;
            return TimelineCandidates.Magnet(startTime, length, magnetSnapTimesCache, magnetSnapInterval, true);
        }

        #endregion

        #region 融合检测

        public void CheckBlendInOut(Clip clip)
        {
            // if (!clip.CanCrossBlend) return;

            //获取Clip前后相交的所有clip，并计算混合
            // List<Clip> linkClips = new List<Clip>();
            // GetPreviousCrossBlends(clip, linkClips);
            // GetNextCrossBlends(clip, linkClips);
            // GetCoincideCrossBlends(clip, linkClips);
            // linkClips.Add(clip);
            //
            //
            // linkClips.Sort((a, b) => a.StartTime - b.StartTime > 0 ? 1 : -1);
            // Debug.LogError($"连接的cli个数==={linkClips.Count}");
            //
            // foreach (var item in linkClips)
            // {
            //     item.SetCrossBlendIn(0);
            //     item.SetCrossBlendOut(0);
            // }
            //
            // for (int i = 0; i < linkClips.Count; i++)
            // {
            //     var item = linkClips[i];
            //     if (i == 0)
            //     {
            //         item.SetCrossBlendIn(0);
            //     }
            //     else if (i == linkClips.Count - 1)
            //     {
            //         item.SetCrossBlendOut(0);
            //     }
            //
            //
            //     if (i > 0)
            //     {
            //         var prev = linkClips[i - 1];
            //         var overlap = Mathf.Max(prev.EndTime - item.StartTime, 0);
            //         item.SetCrossBlendIn(overlap);
            //         prev.SetCrossBlendOut(overlap);
            //
            //         if (i < linkClips.Count - 1)
            //         {
            //             var next = linkClips[i + 1];
            //             // var overlap2 = Mathf.Max(prev.EndTime - clip.StartTime, 0);
            //             var overlap2 = Mathf.Max(item.EndTime - next.StartTime, 0);
            //             next.SetCrossBlendIn(overlap2);
            //             item.SetCrossBlendOut(overlap2);
            //         }
            //     }
            // }
            //
            //
            // var nextSibling = clip.GetNextSibling<Clip>();
            // if (nextSibling != null && !linkClips.Contains(nextSibling))
            // {
            //     nextSibling.SetCrossBlendIn(0);
            // }
            //
            // var previousSibling = clip.GetPreviousSibling<Clip>();
            // if (previousSibling != null && !linkClips.Contains(previousSibling))
            // {
            //     previousSibling.SetCrossBlendOut(0);
            // }

            // if (_trackDic.TryGetValue(clip.Parent, out var parentViewItem))
            // {
            //     foreach (var item in linkClips)
            //     {
            //         var clipItem = parentViewItem.GetClipItem(item);
            //         if (clipItem != null)
            //         {
            //             clipItem.OnTryUpdateMesh();
            //         }
            //     }
            //
            //     if (nextSibling != null)
            //     {
            //         var clipItem = parentViewItem.GetClipItem(nextSibling);
            //         if (clipItem != null)
            //         {
            //             clipItem.OnTryUpdateMesh();
            //         }
            //     }
            //
            //     if (previousSibling != null)
            //     {
            //         var clipItem = parentViewItem.GetClipItem(previousSibling);
            //         if (clipItem != null)
            //         {
            //             clipItem.OnTryUpdateMesh();
            //         }
            //     }
            // }
        }

        private void GetPreviousCrossBlends(Clip clip, List<Clip> list)
        {
            var previous = clip.GetPreviousSibling<Clip>();
            if (previous == null)
            {
                Debug.LogError("没有更前的Clip");
                return;
            }

            if (previous.StartTime <= clip.StartTime && previous.EndTime > clip.StartTime)
            {
                list.Add(previous);
                GetPreviousCrossBlends(previous, list);
            }
        }

        private void GetNextCrossBlends(Clip clip, List<Clip> list)
        {
            var nextSibling = clip.GetNextSibling<Clip>();
            if (nextSibling == null) return;
            if (nextSibling.StartTime > clip.StartTime && nextSibling.StartTime < clip.EndTime)
            {
                list.Add(nextSibling);
                GetNextCrossBlends(nextSibling, list);
            }
        }

        private void GetCoincideCrossBlends(Clip clip, List<Clip> list)
        {
            var pick = clip.GetCoincideSibling();
            if (pick != null && pick.Length > 0)
            {
                foreach (var cDirectable in pick)
                {
                    if (cDirectable is Clip c)
                    {
                        list.Add(c);
                    }
                }
            }
        }

        #endregion
    }
}