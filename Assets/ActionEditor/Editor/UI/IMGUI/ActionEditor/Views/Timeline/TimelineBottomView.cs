using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public class TimelineBottomView : ViewBase
    {
        public override void OnDraw()
        {
            ShowTimeSlider();
        }

        void ShowTimeSlider()
        {
            var asset = App.AssetData;
            if(asset == null) return;
            var left = Workspace.LeftWidth + Styles.RightGapWidth;
            var rect = new Rect(Position.x + left, Position.y, Position.width - left, Position.height);
            GUILayout.BeginArea(rect);
            var _timeMin = Workspace.ViewTimeMin;
            var _timeMax = Workspace.ViewTimeMax;
            
            GUI.color = Color.gray;
            
            var sliderRect = new Rect(5, 0, rect.width - 10, 18);
            EditorGUI.MinMaxSlider(sliderRect, ref _timeMin, ref _timeMax, 0, asset.Length);
            Workspace.ViewTimeMin = _timeMin;
            Workspace.ViewTimeMax = _timeMax;
            if (sliderRect.Contains(Event.current.mousePosition) && Event.current.clickCount == 2)
            {
                Workspace.ViewTimeMin = 0;
                Workspace.ViewTimeMax = asset.Length;
            }

            GUILayout.EndArea();

            GUI.contentColor = Color.white;
        }
    }
}