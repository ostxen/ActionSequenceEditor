using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI
{
    internal static class ImguiStyles
    {
        private static GUIStyle header;
        public static GUIStyle HeaderBoxStyle => header ??= ((GUISkin)Resources.Load("ActionSequenceEditor/StyleSheet")).GetStyle("HeaderBox");
    }
}
