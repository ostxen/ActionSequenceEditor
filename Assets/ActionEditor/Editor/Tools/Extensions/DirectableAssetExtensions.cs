using UnityEngine;

namespace NBC.ActionEditor
{
    public static class DirectableAssetExtensions
    {
        public static float SnapTime(this IDirector asset, float time)
        {
            return Mathf.Round(time / Prefs.SnapInterval) * Prefs.SnapInterval;
        }

        public static float TimeToPos(this IDirector asset, float time, float width)
        {
            return new JsonTimelineTransform(asset.ViewTimeMin, asset.ViewTime, width).X(time);
        }

        public static float PosToTime(this IDirector asset, float pos, float width)
        {
            return new JsonTimelineTransform(asset.ViewTimeMin, asset.ViewTime, width).Time(pos);
        }
        
        public static float WidthToTime(this IDirector asset, float pos, float width)
        {
            return new JsonTimelineTransform(asset.ViewTimeMin, asset.ViewTime, width).Duration(pos);
        }
    }
}