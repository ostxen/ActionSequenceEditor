
namespace ActionSequenceEditor.Infrastructure.Preview
{
    // 按时间边界转发预览回调，所有权由预览会话管理。
    public interface IDirectableTimePointer
    {
        PreviewBase target { get; }
        float time { get; }
        void TriggerForward(float currentTime, float previousTime);
        void TriggerBackward(float currentTime, float previousTime);
        void Update(float currentTime, float previousTime);
    }

    // 起点维护进入状态，回调之间重验会话是否仍拥有实例。
    public struct StartTimePointer : IDirectableTimePointer
    {
        private bool triggered;
        private float lastTargetStartTime;
        private readonly System.Func<bool> active;
        public PreviewBase target { get; private set; }
        float IDirectableTimePointer.time => target.directable.StartTime;

        public StartTimePointer(PreviewBase target, System.Func<bool> active = null)
        {
            this.target = target;
            this.active = active;
            triggered = false;
            lastTargetStartTime = target.directable.StartTime;
        }

        void IDirectableTimePointer.TriggerForward(float currentTime, float previousTime)
        {
            if (!target.directable.IsActive) return;
            if (currentTime >= target.directable.StartTime)
            {
                if (!triggered)
                {
                    triggered = true;
                    target.Enter();
                    if (active != null && !active()) return;
                    target.Update(target.directable.ToLocalTime(currentTime), 0);
                }
            }
        }

        void IDirectableTimePointer.Update(float currentTime, float previousTime)
        {
            if (!target.directable.IsActive) return;
            if (currentTime >= target.directable.StartTime && currentTime < target.directable.EndTime &&
                currentTime > 0)
            {
                var deltaMoveClip = target.directable.StartTime - lastTargetStartTime;
                var localCurrentTime = target.directable.ToLocalTime(currentTime);
                var localPreviousTime = target.directable.ToLocalTime(previousTime + deltaMoveClip);

                target.Update(localCurrentTime, localPreviousTime);
                // 更新回调也可能释放预览并清空目标，失效后不再读取作者对象。
                if (active != null && !active()) return;
                lastTargetStartTime = target.directable.StartTime;
            }
        }

        void IDirectableTimePointer.TriggerBackward(float currentTime, float previousTime)
        {
            if (!target.directable.IsActive) return;
            if (currentTime < target.directable.StartTime || currentTime <= 0)
            {
                if (triggered)
                {
                    triggered = false;
                    target.Update(0, target.directable.ToLocalTime(previousTime));
                    if (active != null && !active()) return;
                    target.Reverse();
                }
            }
        }
    }

    // 终点负责退出和反向进入，不能继续调用已被同步释放的实例。
    public struct EndTimePointer : IDirectableTimePointer
    {
        private bool triggered;
        private readonly System.Func<bool> active;
        public PreviewBase target { get; private set; }
        float IDirectableTimePointer.time => target.directable.EndTime;

        public EndTimePointer(PreviewBase target, System.Func<bool> active = null)
        {
            this.target = target;
            this.active = active;
            triggered = false;
        }

        void IDirectableTimePointer.TriggerForward(float currentTime, float previousTime)
        {
            if (!target.directable.IsActive) return;
            if (currentTime >= target.directable.EndTime)
            {
                if (!triggered)
                {
                    triggered = true;
                    target.Update(target.directable.GetLength(), target.directable.ToLocalTime(previousTime));
                    if (active != null && !active()) return;
                    target.Exit();
                }
            }
        }


        void IDirectableTimePointer.Update(float currentTime, float previousTime)
        {
            
        }


        void IDirectableTimePointer.TriggerBackward(float currentTime, float previousTime)
        {
            if (!target.directable.IsActive) return;
            if (currentTime < target.directable.EndTime || currentTime <= 0)
            {
                if (triggered)
                {
                    triggered = false;
                    target.ReverseEnter();
                    if (active != null && !active()) return;
                    target.Update(target.directable.ToLocalTime(currentTime), target.directable.GetLength());
                }
            }
        }
    }
}