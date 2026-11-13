namespace NBC.ActionEditor
{
    // IMGUI Header只声明即时绘制，不混入Toolkit控件构建。
    public abstract class HeaderBase
    {
        public abstract void OnGUI();
    }
}