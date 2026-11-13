using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor.Samples.LegacyExperience
{
    [Serializable]
    [Name("功能体验样例（预置轨道）")]
    [Description("独立功能样例，不依赖 Combat。创建后已有分组、轨道、点事件、区间和内循环。")]
    public sealed class LegacyExperienceAsset : ActionDocument
    {
        [MenuName("样例说明")] public string Notes = "选择片段查看属性，拖动时间轴或片段边缘；顶部按钮打开状态预览。修改后可保存与撤销。";
        [MenuName("版本")] public int Version = 1;
        [MenuName("预览背景")] public Color Background = new Color(0.15f, 0.18f, 0.22f);

        public LegacyExperienceAsset()
        {
            var main = new LegacyExperienceGroup { Name = "01 基础编辑：拖动、伸缩、渐变" };
            var intervals = new LegacyIntervalTrack { Name = "区间与渐入渐出" };
            intervals.Clips.Add(new LegacyExperienceInterval { Name = "准备", Caption = "准备", StartTime = 0.25f, Length = 1.5f, BlendIn = 0.25f, BlendOut = 0.25f });
            intervals.Clips.Add(new LegacyExperienceInterval { Name = "主体", Caption = "主体", StartTime = 2, Length = 2, BlendIn = 0.4f, BlendOut = 0.4f, Tint = new Color(0.3f, 0.8f, 0.5f), Offset = new Vector3(2, 1, 0) });
            intervals.Clips.Add(new LegacyExperienceInterval { Name = "收尾", Caption = "收尾", StartTime = 4.5f, Length = 1.5f, BlendIn = 0.2f, BlendOut = 0.5f });
            var blend = new LegacyIntervalTrack { Name = "同类片段交叉混合" };
            blend.Clips.Add(new LegacyExperienceInterval { Name = "混合 A", Caption = "混合 A", StartTime = 1, Length = 2.5f, BlendOut = 0.5f });
            blend.Clips.Add(new LegacyExperienceInterval { Name = "混合 B", Caption = "混合 B", StartTime = 3, Length = 2, BlendIn = 0.5f });
            var markers = new LegacyMarkerTrack { Name = "点事件：右键新增" };
            markers.Clips.Add(new LegacyExperienceMarker { Name = "开始标记", StartTime = 0.5f, Message = "开始" });
            markers.Clips.Add(new LegacyExperienceMarker { Name = "中间标记", StartTime = 2.5f, Message = "中间", EventCode = 1 });
            markers.Clips.Add(new LegacyExperienceMarker { Name = "结束标记", StartTime = 6, Message = "结束", EventCode = 2 });
            main.Tracks.Add(intervals); main.Tracks.Add(blend); main.Tracks.Add(markers);

            var extended = new LegacyExperienceGroup { Name = "02 内循环、属性与资源路径" };
            var loops = new LegacyLoopTrack { Name = "内循环：右键匹配长度" };
            loops.Clips.Add(new LegacyExperienceLoop { Name = "三次循环", Caption = "周期 0.75 秒 × 3", StartTime = 0.5f, Length = 2.25f });
            loops.Clips.Add(new LegacyExperienceLoop { Name = "倍速循环", Caption = "2 倍速", StartTime = 3, Length = 1.5f, Speed = 2 });
            var properties = new LegacyIntervalTrack { Name = "Inspector 常用字段" };
            properties.Clips.Add(new LegacyExperienceInterval { Name = "属性练习", Caption = "修改 Inspector 字段", StartTime = 1, Length = 4, Mode = 1, Gain = 0.8f });
            extended.Tracks.Add(loops); extended.Tracks.Add(properties);

            var states = new LegacyExperienceGroup { Name = "03 折叠与锁定：右键解锁" };
            var locked = new LegacyIntervalTrack { Name = "锁定轨道", IsLocked = true };
            locked.Clips.Add(new LegacyExperienceInterval { Name = "锁定区间", Caption = "解锁后可拖动", StartTime = 1, Length = 2 });
            var inactive = new LegacyMarkerTrack { Name = "未激活轨道", IsActive = false };
            inactive.Clips.Add(new LegacyExperienceMarker { Name = "未激活点", StartTime = 4, Message = "激活轨道后参与状态显示" });
            states.Tracks.Add(locked); states.Tracks.Add(inactive);
            AddRoot(main); AddRoot(extended); AddRoot(states);
            Init(); RangeMin = 0; RangeMax = 6;
        }
    }

    [Serializable, Name("体验分组"), Attachable(typeof(LegacyExperienceAsset))]
    public sealed class LegacyExperienceGroup : TimelineGroup { }

    [Serializable, Name("体验/区间轨道"), Attachable(typeof(LegacyExperienceGroup)), NBC.ActionEditor.Color(0.3f, 0.65f, 1)]
    public sealed class LegacyIntervalTrack : TimelineTrack
    {
        [MenuName("轨道备注")] public string Notes = "右键空白新增区间；选中片段后查看 Inspector";
    }

    [Serializable, Name("体验/点事件轨道"), Attachable(typeof(LegacyExperienceGroup)), NBC.ActionEditor.Color(1, 0.7f, 0.25f)]
    public sealed class LegacyMarkerTrack : TimelineTrack { }

    [Serializable, Name("体验/内循环轨道"), Attachable(typeof(LegacyExperienceGroup)), NBC.ActionEditor.Color(0.45f, 0.85f, 0.5f)]
    public sealed class LegacyLoopTrack : TimelineTrack { }

    public static class LegacyExperienceModes
    {
        [MenuName("文字"), OptionSort(0)] public const int Text = 0;
        [MenuName("数值"), OptionSort(1)] public const int Number = 1;
        [MenuName("资源路径（不执行）"), OptionSort(2)] public const int Resource = 2;
    }

    [Serializable]
    public sealed class LegacyExperienceParameter
    {
        [MenuName("键")] public string Key = "参数";
        [MenuName("值")] public float Value = 1;
    }

    public enum LegacyExperienceDirection { Forward, Backward, Both }

    [Serializable, Name("体验区间"), Category("体验区间"), Attachable(typeof(LegacyIntervalTrack)), NBC.ActionEditor.Color(0.3f, 0.65f, 1)]
    public sealed class LegacyExperienceInterval : CrossBlendClip
    {
        [MenuName("标题")] public string Caption = "新建区间";
        [MenuName("显示模式"), OptionParam(typeof(LegacyExperienceModes))] public int Mode;
        [MenuName("说明文字"), OptionRelateParam("Mode", LegacyExperienceModes.Text)] public string Message = "可编辑的文字";
        [MenuName("数值"), OptionRelateParam("Mode", LegacyExperienceModes.Number)] public float Amount = 10;
        [MenuName("资源路径"), SelectObjectPath(typeof(AudioClip)), OptionRelateParam("Mode", LegacyExperienceModes.Resource)] public string ResourcePath = "";
        [MenuName("强度"), UnityEngine.Range(0f, 1f)] public float Gain = 1;
        [MenuName("次数")] public int Count = 3;
        [MenuName("方向")] public LegacyExperienceDirection Direction = LegacyExperienceDirection.Forward;
        [MenuName("启用状态显示")] public bool ShowInPreview = true;
        [MenuName("颜色")] public Color Tint = new Color(0.4f, 0.65f, 1);
        [MenuName("偏移")] public Vector3 Offset = Vector3.zero;
        [MenuName("范围")] public Vector2 Range = new Vector2(0, 1);
        [MenuName("变化曲线")] public AnimationCurve Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [MenuName("参数列表")] public List<LegacyExperienceParameter> Parameters = new List<LegacyExperienceParameter>
        {
            new LegacyExperienceParameter { Key = "幅度", Value = 1 },
            new LegacyExperienceParameter { Key = "频率", Value = 2 }
        };
        public override float Length { get => length; set => length = Mathf.Max(0.01f, value); }
        public override bool IsValid => true;
        public override string Info => Caption;
    }

    [Serializable, Name("体验点事件"), Category("体验标记"), Attachable(typeof(LegacyMarkerTrack)), NBC.ActionEditor.Color(1, 0.7f, 0.25f)]
    public sealed class LegacyExperienceMarker : PointClip
    {
        [MenuName("消息")] public string Message = "新建标记";
        [MenuName("事件编号")] public int EventCode;
        [MenuName("备注")] public string Notes = "点事件长度为零，只显示状态，不触发游戏逻辑";
        public override bool IsValid => true;
        public override string Info => Message;
    }

    [Serializable, Name("体验循环片段"), Category("体验循环"), Attachable(typeof(LegacyLoopTrack)), NBC.ActionEditor.Color(0.45f, 0.85f, 0.5f)]
    public sealed class LegacyExperienceLoop : CrossBlendClip, ISubClipContainable
    {
        [MenuName("标题")] public string Caption = "循环片段";
        [MenuName("源片段周期（秒）")] public float Period = 0.75f;
        [MenuName("播放速度")] public float Speed = 1;
        [MenuName("源片段偏移")] public float Offset;
        public float SubClipOffset { get => Offset; set => Offset = value; }
        public float SubClipSpeed => Mathf.Max(0.01f, Speed);
        public float SubClipLength => Mathf.Max(0.01f, Period);
        public override float Length { get => length; set => length = Mathf.Max(0.01f, value); }
        public override bool IsValid => Period > 0 && Speed > 0;
        public override string Info => Caption;
    }
}
