using System;
using System.Collections.Generic;
using UnityEngine;
using YOLOTools.YOLO.ObjectDetection;

namespace YOLOTools.YOLO
{
    public abstract class YOLOProvider : MonoBehaviour
    {
        public event Action<List<DetectedObject>> DetectedObjectsUpdated;

        protected virtual void OnDetectedObjectsUpdated(List<DetectedObject> obj)
        {
            DetectedObjectsUpdated?.Invoke(obj);
        }
    }
}
