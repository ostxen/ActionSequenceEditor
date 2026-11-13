using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Application.Editing
{
    // 片段时间应用用例只接收作者候选，不解释像素、捕获或吸附。
    public sealed class ClipEditing
    {
        private readonly DocumentSession session;
        public ClipEditing(DocumentSession session) { this.session = session ?? throw new ArgumentNullException(nameof(session)); }

        // 属性提交独占一次作者事务，不参与其他窗口尚未结束的手势。
        public void SetTiming(TimelineClip clip, float start, float end, float step)
        {
            RequirePropertyEdit(clip);
            session.Edit("Edit clip timing", () => ClipTimingRules.ApplyPropertyTiming(clip, start, end, step));
        }
        public void SetBlend(TimelineClip clip, bool incoming, float value)
        {
            RequirePropertyEdit(clip);
            session.Edit("Edit clip blend", () => ClipTimingRules.ApplyPropertyBlend(clip, incoming, value));
        }
        private void RequirePropertyEdit(TimelineClip clip)
        {
            RequireClip(clip);
            if (session.IsEditing) throw new InvalidOperationException("请先完成当前编辑。");
        }

        // 一次候选中的片段必须仍属于当前图，全部重验后才一起更新。
        public void Move(DocumentEditTransaction token, IReadOnlyDictionary<TimelineClip, float> starts, float delta)
        {
            RequireToken(token);
            if (starts == null || starts.Count == 0) throw new ArgumentException("没有移动片段。");
            RequireFinite(delta);
            foreach (var pair in starts) { RequireClip(pair.Key); RequireFinite(pair.Value); }
            delta = Math.Max(delta, -starts.Values.Min());
            var applied = delta;
            token.Update(() => { foreach (var pair in starts) pair.Key.StartTime = pair.Value + applied; });
        }
        public void Resize(DocumentEditTransaction token, TimelineClip clip, float start, float length)
        {
            RequireToken(token); RequireClip(clip); RequireFinite(start); RequireFinite(length);
            if (start < 0 || length < .01f) throw new ArgumentOutOfRangeException(nameof(length), "片段最短为0.01秒。");
            token.Update(() => { clip.StartTime = start; clip.Length = length; });
        }
        private void RequireToken(DocumentEditTransaction token)
        { if (!session.Owns(token)) throw new InvalidOperationException("编辑令牌已失效或不属于此会话。"); }
        private void RequireClip(TimelineClip clip)
        {
            if (clip == null || session.Document == null || !session.Document.directables.Contains(clip) || clip.IsLocked)
                throw new InvalidOperationException("片段已失效或锁定。");
        }
        private static void RequireFinite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
    }
}
