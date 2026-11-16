using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI
{
    internal static class ImguiStyles
    {
        private static GUIStyle header;
        public static GUIStyle HeaderBoxStyle => header ??= ((GUISkin)Resources.Load("nbc/StyleSheet")).GetStyle("HeaderBox");
    }
}
