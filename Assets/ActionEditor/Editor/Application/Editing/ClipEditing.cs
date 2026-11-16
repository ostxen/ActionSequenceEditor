using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Application.Sequences;
using NBC.ActionEditor.Model;

namespace NBC.ActionEditor.Application.Editing
{
    // 片段时间应用用例只接收作者候选，不解释像素、捕获或吸附。
    public sealed class ClipEditing
    {
        private readonly SequenceSession session;
        public ClipEditing(SequenceSession session) { this.session = session ?? throw new ArgumentNullException(nameof(session)); }

        // 属性提交独占一次作者事务，不参与其他窗口尚未结束的手势。
        public void SetTiming(ActionClip clip, float start, float end, float snapInterval)
        {
            RequirePropertyEdit(clip);
            session.Edit("Edit clip timing", () => ClipTimingRules.ApplyPropertyTiming(clip, start, end, snapInterval));
        }
        public void SetBlend(ActionClip clip, bool isBlendIn, float duration)
        {
            RequirePropertyEdit(clip);
            session.Edit("Edit clip blend", () => ClipTimingRules.ApplyPropertyBlend(clip, isBlendIn, duration));
        }
        private void RequirePropertyEdit(ActionClip clip)
        {
            RequireClip(clip);
            if (session.IsEditing) throw new InvalidOperationException("请先完成当前编辑。");
        }

        // 一次候选中的片段必须仍属于当前图，全部重验后才一起更新。
        public void Move(SequenceEditTransaction token, IReadOnlyDictionary<ActionClip, float> originalStartTimes, float timeOffset)
        {
            RequireToken(token);
            if (originalStartTimes == null || originalStartTimes.Count == 0) throw new ArgumentException("没有移动片段。");
            RequireFinite(timeOffset);
            foreach (var pair in originalStartTimes) { RequireClip(pair.Key); RequireFinite(pair.Value); }
            timeOffset = Math.Max(timeOffset, -originalStartTimes.Values.Min());
            var appliedOffset = timeOffset;
            foreach (var pair in originalStartTimes) ClipTimingRules.RequireTiming(pair.Key, pair.Value + appliedOffset, pair.Key.Length);
            token.Update(() => { foreach (var pair in originalStartTimes) ClipTimingRules.ApplyTiming(pair.Key, pair.Value + appliedOffset, pair.Key.Length); });
        }
        public void Resize(SequenceEditTransaction token, ActionClip clip, float start, float length)
        {
            RequireToken(token); RequireClip(clip); RequireFinite(start); RequireFinite(length);
            if (!clip.CanScale()) throw new InvalidOperationException("片段不支持缩放。");
            ClipTimingRules.RequireTiming(clip, start, length);
            token.Update(() => ClipTimingRules.ApplyTiming(clip, start, length));
        }
        // 手势可暂时显示重叠候选；结束时整批验收，不在View复制合法性规则。
        public bool Finish(SequenceEditTransaction token, IEnumerable<ActionClip> candidates)
        {
            RequireToken(token);
            try
            {
                foreach (var clip in candidates)
                {
                    RequireClip(clip);
                    ClipTimingRules.RequireTiming(clip, clip.StartTime, clip.Length);
                    if (!clip.FitsSiblingTiming()) { token.Cancel(); return false; }
                }
                token.Commit("Drag timeline clips");
                return true;
            }
            catch { if (token.IsActive) token.Cancel(); throw; }
        }
        public void MatchLoopLength(ActionClip clip, int loopStep)
        {
            RequirePropertyEdit(clip);
            if (clip is not ISubClipContainable loop || loop.SubClipLength <= 0 || loop.SubClipSpeed <= 0)
                throw new InvalidOperationException("片段没有有效的源循环。");
            var length = loopStep < 0 ? loop.GetPreviousLoopAlignedLength()
                : loopStep > 0 ? loop.GetNextLoopAlignedLength() : loop.SubClipLength / loop.SubClipSpeed;
            ClipTimingRules.RequireTiming(clip, clip.StartTime, length);
            var next = clip.GetNextSibling();
            if (loopStep > 0 && next != null && clip.StartTime + length > next.StartTime) return;
            if (!clip.FitsSiblingTiming(clip.StartTime, clip.StartTime + length))
                throw new InvalidOperationException("目标时长超出相邻片段边界。");
            session.Edit("Match clip loop", () => ClipTimingRules.ApplyTiming(clip, clip.StartTime, length));
        }
        private void RequireToken(SequenceEditTransaction token)
        { if (!session.Owns(token)) throw new InvalidOperationException("编辑令牌已失效或不属于此会话。"); }
        private void RequireClip(ActionClip clip)
        {
            if (clip == null || session.Sequence == null || !session.Sequence.directables.Contains(clip) || clip.IsLocked)
                throw new InvalidOperationException("片段已失效或锁定。");
        }
        private static void RequireFinite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
    }
}
