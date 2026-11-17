using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionSequenceEditor.Model
{
    // 保存片段作者参数，时间候选的编辑规则由领域校验统一处理。
    [Serializable]
    public abstract class ActionClip : IClip
    {
        [SerializeField] private float startTime;
        [SerializeField] [HideInInspector] protected float length = 1f;
        [SerializeField] private string name;
        

        [MenuName("片段长度")]
        public virtual float Length
        {
            get => length;
            set
            {
                length = value;
            }
        }

        public virtual string Info
        {
            get
            {
                var nameAtt = GetType().RTGetAttribute<NameAttribute>(true);
                if (nameAtt != null) return nameAtt.name;

                return GetType().Name.SplitCamelCase();
            }
        }

        public virtual bool IsValid => false;

        public IDirector Root => Parent?.Root;

        public IDirectable Parent { get; private set; }

        IEnumerable<IDirectable> IDirectable.Children => null;

        public GameObject Actor => Parent?.Actor;

        public string Name
        {
            get => name;
            set => name = value;
        }
        

        [MenuName("开始时间")]
        public float StartTime
        {
            get => startTime;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new System.IO.InvalidDataException("开始时间必须为有限数值。");
                // 仅处理负起点，不用显示精度阈值丢弃有效位或负零。
                startTime = value < 0 ? 0 : value;
            }
        }

        [MenuName("结束时间")]
        public float EndTime
        {
            get => StartTime + Length;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new System.IO.InvalidDataException("结束时间必须为有限数值。");
                Length = Mathf.Max(value - StartTime, 0);
                BlendOut = Mathf.Clamp(BlendOut, 0, Length - BlendIn);
                BlendIn = Mathf.Clamp(BlendIn, 0, Length - BlendOut);
            }
        }

        public bool IsActive
        {
            get => Parent?.IsActive ?? false;
            set { }
        }

        public bool IsLocked
        {
            get { return Parent != null && Parent.IsLocked; }
            set { }
        }

        public virtual float BlendIn
        {
            get => 0;
            set { }
        }

        public virtual float BlendOut
        {
            get => 0;
            set { }
        }

        public virtual bool CanCrossBlend { get; }

        internal virtual void ConstrainAuthoredBlends()
        {
            var maximumOut = Mathf.Max(0, Length - Mathf.Max(0, BlendIn));
            if (this.CanBlendOut() && BlendOut > maximumOut) BlendOut = maximumOut;
            var maximumIn = Mathf.Max(0, Length - Mathf.Max(0, BlendOut));
            if (this.CanBlendIn() && BlendIn > maximumIn) BlendIn = maximumIn;
        }

        public virtual void OnBeforeSerialize()
        {
        }

        public virtual void OnAfterDeserialize()
        {
        }

        public bool Initialize()
        {
            return true;
        }

        public void Validate(IDirector root, IDirectable parent)
        {
            Parent = parent;
        }


        public object AnimatedParametersTarget { get; }

        public ActionClip GetNextClip()
        {
            return this.GetNextSibling<ActionClip>();
        }

        public float GetClipWeight(float localTime)
        {
            return GetClipWeight(localTime, BlendIn, BlendOut);
        }

        public float GetClipWeight(float localTime, float blendDuration)
        {
            return GetClipWeight(localTime, blendDuration, blendDuration);
        }

        public float GetClipWeight(float localTime, float blendIn, float blendOut)
        {
            return this.GetWeight(localTime, blendIn, blendOut);
        }
        
        #region 混合切片

        public virtual void SetCrossBlendIn(float value)
        {
        }

        public virtual void SetCrossBlendOut(float value)
        {
        }

        #endregion

        public void PostCreate(IDirectable parent)
        {
            Parent = parent;
        }
    }


    // 点事件的有效时长固定为零，基类存储字段仍原样保留。
    [Serializable]
    public abstract class PointClip : ActionClip
    {
        public override float Length
        {
            get => 0;
        }
    }

    // 区分作者渐变参数与邻接片段计算出的交叉混合参数。
    [Serializable]
    public abstract class CrossBlendClip : ActionClip
    {
        [SerializeField] [HideInInspector] protected float blendIn = 0f;
        [SerializeField] [HideInInspector] protected float blendOut = 0f;

        [SerializeField] [HideInInspector] private float CrossBlendIn = 0f;
        [SerializeField] [HideInInspector] private float CrossBlendOut = 0f;


        public override bool CanCrossBlend => true;

        // 只收敛作者渐变，不能把邻接计算缓存写回作者字段。
        internal override void ConstrainAuthoredBlends()
        {
            var maximumOut = Mathf.Max(0, Length - Mathf.Max(0, blendIn));
            if (this.CanBlendOut() && blendOut > maximumOut) blendOut = maximumOut;
            var maximumIn = Mathf.Max(0, Length - Mathf.Max(0, blendOut));
            if (this.CanBlendIn() && blendIn > maximumIn) blendIn = maximumIn;
        }
        [MenuName("渐入时间")]
        public override float BlendIn
        {
            get
            {
                if (CrossBlendIn > 0)
                {
                    return CrossBlendIn;
                }

                return blendIn;
            }
            set
            {
                blendIn = value;
                if (blendIn < 0)
                {
                    blendIn = 0;
                }
                else if (blendIn > Length - BlendOut)
                {
                    blendIn = Length - BlendOut;
                }
            }
        }

        [MenuName("渐出时间")]
        public override float BlendOut
        {
            get
            {
                if (CrossBlendOut > 0)
                {
                    return CrossBlendOut;
                }

                return blendOut;
            }
            set
            {
                blendOut = value;
                if (blendOut < 0)
                {
                    blendOut = 0;
                }
                else if (blendOut > Length - BlendIn)
                {
                    blendOut = Length - BlendIn;
                }
            }
        }

        public override void SetCrossBlendIn(float value)
        {
            CrossBlendIn = value;
        }

        public override void SetCrossBlendOut(float value)
        {
            CrossBlendOut = value;
        }
    }
}