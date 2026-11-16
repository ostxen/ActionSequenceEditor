using NBC.ActionEditor.Model;
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

        void DeleteGroup(ActionGroup group);

        void UpdateMaxTime();
        void Validate();
    }
}