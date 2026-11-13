using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public static class ClipDrawer
    {
        // private static Dictionary<Type, ClipDrawBase> _clipDrawDictionary = new Dictionary<Type, ClipDrawBase>();

        private static Dictionary<TimelineClip, ClipDrawBase> _clipDraws = new Dictionary<TimelineClip, ClipDrawBase>();

        static ClipDrawer()
        {
            // _clipDrawDictionary.Add(typeof(SimpleClipDraw), new SimpleClipDraw());
            // _clipDrawDictionary.Add(typeof(BasicClipDraw), new BasicClipDraw());
            // _clipDrawDictionary.Add(typeof(SignalClipDraw), new SignalClipDraw());
        }

        #region Get

        public static List<IDirectable> GetClips(Rect rect, NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace workspace)
        {
            List<IDirectable> list = new List<IDirectable>();
            foreach (var clip in _clipDraws.Keys.Where(clip => TimelineGeometry.IsVisible(clip, workspace)))
            {
                var draw = _clipDraws[clip];
                if (TimelineGeometry.Overlaps(rect, draw.ClipRealRect))
                {
                    list.Add(clip);
                }
            }

            return list;
        }

        public static bool ClipContainsByRealRect(Vector2 pos, NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace workspace)
        {
            foreach (var clip in _clipDraws.Keys.Where(clip => TimelineGeometry.IsVisible(clip, workspace)))
            {
                var draw = _clipDraws[clip];
                if (draw.ClipRealRect.Contains(pos))
                {
                    return true;
                }
            }

            return false;
        }

        public static TimelineClip GetClipByTrackPosition(IDirectable track, Vector2 mousePosition, NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace workspace)
        {
            foreach (var clip in _clipDraws.Keys.Where(clip => TimelineGeometry.IsVisible(clip, workspace)))
            {
                if (clip.Parent != track) continue;
                var draw = _clipDraws[clip];
                if (draw.ClipRect.Contains(mousePosition))
                {
                    return clip;
                }
            }

            return null;
        }

        #endregion

        #region Draw

        public static void Reset()
        {
            List<TimelineClip> clips = new List<TimelineClip>();
            var asset = App.AssetData;
            if (asset == null) return;
            foreach (var track in asset.AllTracks) clips.AddRange(track.Clips);

            List<TimelineClip> invalidClips = _clipDraws.Keys.Where(clip => !clips.Contains(clip)).ToList();
            foreach (var clip in invalidClips)
            {
                _clipDraws.Remove(clip);
            }

            foreach (var clip in clips)
            {
                if (!_clipDraws.ContainsKey(clip))
                {
                    var type = typeof(BasicClipDraw);
                    if (clip is PointClip)
                    {
                        type = typeof(SignalClipDraw);
                    }

                    _clipDraws[clip] = Activator.CreateInstance(type) as BasicClipDraw;
                }
            }
        }

        public static ClipDrawBase GetDraw<T>(T clip) where T : TimelineClip
        {
            return _clipDraws.GetValueOrDefault(clip);
        }

        // public static ClipDrawBase Draw<T>(EditorWindow window, Rect trackRect, Rect trackRightRect, T clip,
        //     bool select,
        //     Type drawType = null) where T : Clip
        // {
        //     if (_clipDraws.TryGetValue(clip, out var draw))
        //     {
        //         draw.Draw(window, trackRect, trackRightRect, clip, select);
        //         return draw;
        //     }
        //
        //     return default;
        // }

        #endregion
    }
}