﻿using UnityEngine;

namespace SAIN.Components
{
    public class RaycastObject : AbstractJobObject
    {
        public LayerMask LayerMask;
        public RaycastCommand Command;
        public RaycastHit Hit;

        public DistanceObject DistanceData {
            get
            {
                return _distanceData;
            }
            set
            {
                unSub();
                value.OnCompleted += onDistanceCalced;
                _distanceData = value;
            }
        }

        private DistanceObject _distanceData;

        public void Complete(RaycastHit hit)
        {
            Hit = hit;
            Status = EJobStatus.Complete;
        }

        public void Create()
        {
            if (!base.CanBeScheduled()) {
                return;
            }
            Command = new RaycastCommand {
                from = DistanceData.Origin,
                direction = DistanceData.Direction,
                distance = DistanceData.Distance,
                layerMask = LayerMask,
                maxHits = 1,
            };
            Hit = new RaycastHit();
            Status = EJobStatus.UnScheduled;
        }

        public override void Dispose()
        {
            base.Dispose();
            unSub();
        }

        private void unSub()
        {
            if (_distanceData != null) {
                _distanceData.OnCompleted -= onDistanceCalced;
            }
        }

        private void onDistanceCalced(AbstractJobObject distanceData)
        {
            Create();
            Status = EJobStatus.UnScheduled;
        }
    }
}