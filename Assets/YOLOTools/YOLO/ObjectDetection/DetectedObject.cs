using JetBrains.Annotations;
using UnityEngine;

namespace YOLOTools.YOLO.ObjectDetection
{
    public class DetectedObject
    {
        public Rect BoundingBox { get; private set; }
        public int CocoClass { get; private set; }
        [CanBeNull] public string CocoName { get; private set; }
        public float Confidence { get; private set; }

        public DetectedObject(float centreX, float centreY, float width, float height, int cocoClass, string cocoName, float confidence)
        {
            CocoClass = cocoClass;
            CocoName = cocoName;
            Confidence = confidence;
            BoundingBox = new Rect((int)centreX-(width/2), (int)centreY-(height/2), (int)width, (int)height);
        }
    }
}
