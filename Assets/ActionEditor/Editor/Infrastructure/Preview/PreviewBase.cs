namespace NBC.ActionEditor
{
    // 为业务预览提供明确类型的目标访问。
    public abstract class PreviewBase<T> : PreviewBase where T : IDirectable
    {
        public T clip => (T)directable;
    }

    // 定义预览回调与释放契约，调用顺序由预览会话管理。
    public abstract class PreviewBase
    {
        public IDirectable directable;

        public void SetTarget(IDirectable t)
        {
            directable = t;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public virtual void ReverseEnter()
        {
        }

        public virtual void Reverse()
        {
        }

        public virtual void Dispose()
        {
            Reverse();
        }


        public abstract void Update(float time, float previousTime);
    }
}