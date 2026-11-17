using System;
using UnityEditor;

namespace ActionSequenceEditor.Verification
{
    // 固定验收能力共用编辑器就绪条件，不接收外部执行输入。
    internal static class VerificationGuard
    {
        public static void RequireReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("需要空闲编辑模式。");
        }
    }
}
