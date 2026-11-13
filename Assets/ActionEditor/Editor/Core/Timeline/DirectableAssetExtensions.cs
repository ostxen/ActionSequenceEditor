using UnityEngine;

namespace NBC.ActionEditor
{
    public static class DirectableAssetExtensions
    {
        public static float SnapTime(this IDirector asset, float time)
        {
            return Mathf.Round(time / Prefs.SnapInterval) * Prefs.SnapInterval;
        }

        public static float TimeToPos(this NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace asset, float time, float width)
        {
            return new JsonTimelineTransform(asset.ViewTimeMin, asset.ViewTime, width).X(time);
        }

        public static float PosToTime(this NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace asset, float pos, float width)
        {
            return new JsonTimelineTransform(asset.ViewTimeMin, asset.ViewTime, width).Time(pos);
        }
        
        public static float WidthToTime(this NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace asset, float pos, float width)
        {
            return new JsonTimelineTransform(asset.ViewTimeMin, asset.ViewTime, width).Duration(pos);
        }
    }
}