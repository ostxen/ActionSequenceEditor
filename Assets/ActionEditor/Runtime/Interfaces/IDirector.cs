using NBC.ActionEditor.Domain;
using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 提供时间图根节点的范围、结构校验与派生时长更新。
    public interface IDirector : IData
    {
        float Length { get; }

        public float RangeMin { get; set; }
        public float RangeMax { get; set; }

        void DeleteGroup(TimelineGroup group);

        void UpdateMaxTime();
        void Validate();
    }
}