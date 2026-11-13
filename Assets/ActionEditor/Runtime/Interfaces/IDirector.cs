using NBC.ActionEditor.Domain;
using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor
{
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