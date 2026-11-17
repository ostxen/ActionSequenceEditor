using System;
using System.Linq;
using UnityEngine;

namespace ActionSequenceEditor
{
    // 提供直接节点的时间换算、同级约束与混合权重，不持有编辑器状态。
    public static class IDirectableExtensions
    {
        #region 长度 and 时间转换

        public static float GetLength(this IDirectable directable)
        {
            return directable.EndTime - directable.StartTime;
        }


        public static float ToLocalTime(this IDirectable directable, float time)
        {
            return Mathf.Clamp(time - directable.StartTime, 0, directable.GetLength());
        }


        public static float ToLocalTimeUnclamped(this IDirectable directable, float time)
        {
            return time - directable.StartTime;
        }

        #endregion

        #region 操作判定

        /// <summary>
        /// 切片能否混合
        /// </summary>
        public static bool CanCrossBlend(this IDirectable directable, IDirectable other)
        {
            if (directable == null || other == null) return false;

            if ((directable.CanCrossBlend || other.CanCrossBlend) && directable.GetType() == other.GetType())
                return true;

            return false;
        }

        public static bool CanBlendIn(this IDirectable directable)
        {
            return DirectableTypeInfo.For(directable.GetType()).HasBlendIn && Math.Abs(directable.BlendIn - -1) > 0.0001f;
        }

        public static bool CanBlendOut(this IDirectable directable) =>
            DirectableTypeInfo.For(directable.GetType()).HasBlendOut && Math.Abs(directable.BlendOut - -1) > 0.0001f;

        public static bool CanScale(this IDirectable directable) => DirectableTypeInfo.For(directable.GetType()).CanScale;

        /// <summary>
        /// 当前开始时间是否可用
        /// </summary>
        public static bool FitsSiblingTiming(this IDirectable directable)
        {
            return FitsSiblingTiming(directable, directable.StartTime, directable.EndTime);
        }

        /// <summary>
        /// 当前开始时间是否可用
        /// </summary>
        public static bool FitsSiblingTiming(this IDirectable directable, float startTime, float endTime)
        {
            if (directable.Parent != null)
            {
                return FitsSiblingTiming(directable, directable.Parent, startTime, endTime);
            }

            return true;
        }

        /// <summary>
        /// 当前开始时间是否可用
        /// </summary>
        public static bool FitsSiblingTiming(this IDirectable directable, IDirectable parent, float startTime, float endTime)
        {
            var prevDirectable = directable.GetPreviousSibling(parent);
            var nextDirectable = directable.GetNextSibling(parent);

            var limitStartTime = 0f;
            var limitEndTime = float.MaxValue;

            if (prevDirectable != null)
            {
                limitStartTime = prevDirectable.EndTime;
                if (directable.CanCrossBlend(prevDirectable))
                {
                    limitStartTime = prevDirectable.StartTime;

                    //如果完全包含
                    if (startTime > limitStartTime && endTime < prevDirectable.EndTime)
                    {
                        return false;
                    }
                }
            }

            if (nextDirectable != null)
            {
                limitEndTime = nextDirectable.StartTime;
                if (directable.CanCrossBlend(nextDirectable))
                {
                    limitEndTime = nextDirectable.EndTime;
                }
            }

            if (limitStartTime - startTime > 0.0001f) //直接比大小存在精度问题
            {
                return false;
            }

            if (endTime - limitEndTime > 0.0001f)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region 切片获取

        public static IDirectable[] GetSiblingsWithSameTiming(this IDirectable directable)
        {
            return GetSiblingsWithSameTiming(directable, directable.Parent);
        }

        public static IDirectable[] GetSiblingsWithSameTiming(this IDirectable directable, IDirectable parent)
        {
            if (parent != null)
            {
                return parent.Children.Where(child => child != directable).Where(child =>
                        child.StartTime == directable.StartTime && child.EndTime == directable.EndTime)
                    .ToArray();
            }

            return Array.Empty<IDirectable>();
        }

        public static T GetPreviousSibling<T>(this IDirectable directable) where T : IDirectable
        {
            return (T)GetPreviousSibling(directable, directable.Parent);
        }

        public static IDirectable GetPreviousSibling(this IDirectable directable)
        {
            return GetPreviousSibling(directable, directable.Parent);
        }

        public static IDirectable GetPreviousSibling(this IDirectable directable, IDirectable parent)
        {
            // 保留父列表次序，取最后一个更早开始的节点，而非重新按时间排序。
            if (parent != null)
            {
                return parent.Children.LastOrDefault(d =>
                    d != directable && (d.StartTime < directable.StartTime));
            }

            return null;
        }

        /// <summary>
        /// 返回父对象的下个同级
        /// </summary>
        public static T GetNextSibling<T>(this IDirectable directable) where T : IDirectable
        {
            return (T)GetNextSibling(directable, directable.Parent);
        }

        /// <summary>
        /// 返回父对象的下个同级
        /// </summary>
        public static IDirectable GetNextSibling(this IDirectable directable)
        {
            return GetNextSibling(directable, directable.Parent);
        }

        /// <summary>
        /// 返回指定轨道的下个子对象
        /// </summary>
        public static IDirectable GetNextSibling(this IDirectable directable, IDirectable parent)
        {
            // 保留父列表次序，取第一个更晚开始的节点；同起点不构成前后邻居。
            if (parent != null)
            {
                return parent.Children.FirstOrDefault(d =>
                    d != directable && d.StartTime > directable.StartTime);
            }

            return null;
        }

        #endregion

        #region 混合权重

        /// <summary>
        /// 根据其混合特性在指定当地时间的权重。
        /// </summary>
        public static float GetWeight(this IDirectable directable, float localTime)
        {
            return GetWeight(directable, localTime, directable.BlendIn, directable.BlendOut);
        }

        /// <summary>
        /// 基于所提供的覆盖混合入/出属性在指定本地时间的权重
        /// </summary>
        public static float GetWeight(this IDirectable directable, float localTime, float blendDuration)
        {
            return GetWeight(directable, localTime, blendDuration, blendDuration);
        }

        public static float GetWeight(this IDirectable directable, float localTime, float blendIn, float blendOut)
        {
            var length = GetLength(directable);
            if (localTime <= 0) return blendIn <= 0 ? 1 : 0;

            if (localTime >= length) return blendOut <= 0 ? 1 : 0;

            if (localTime < blendIn) return localTime / blendIn;

            if (localTime > length - blendOut) return (length - localTime) / blendOut;

            return 1;
        }

        #endregion

        #region 循环长度

        /// <summary>
        /// 返回剪辑的上一个循环长度
        /// </summary>
        public static float GetPreviousLoopAlignedLength(this ISubClipContainable clip)
        {
            var clipLength = clip.GetLength();
            var loopLength = clip.SubClipLength / clip.SubClipSpeed;
            if (clipLength > loopLength)
            {
                var loopRemainder = (clipLength - clip.SubClipOffset) % loopLength;
                var isOnLoopBoundary = Mathf.Abs(loopRemainder) < 0.01f;
                return clipLength - (isOnLoopBoundary ? loopLength : loopRemainder);
            }

            return clipLength;
        }

        /// <summary>
        /// 返回剪辑的下一个循环长度
        /// </summary>
        public static float GetNextLoopAlignedLength(this ISubClipContainable clip)
        {
            var clipLength = clip.GetLength();
            var loopLength = clip.SubClipLength / clip.SubClipSpeed;
            var loopRemainder = (clipLength - clip.SubClipOffset) % loopLength;
            var isOnLoopBoundary = Mathf.Abs(loopRemainder) < 0.01f || Mathf.Abs(loopLength - loopRemainder) < 0.01f;
            return clipLength + (isOnLoopBoundary ? loopLength : loopLength - loopRemainder);
        }

        #endregion
    }
}