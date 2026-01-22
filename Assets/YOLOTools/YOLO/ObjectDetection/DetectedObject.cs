using JetBrains.Annotations;
using UnityEngine;

namespace YOLOTools.YOLO.ObjectDetection
{
    public class DetectedObject
    {
        public Rect BoundingBox { get; private set; }
        public int CocoClass { get; private set; }
        [CanBeNull] public string CocoName { get; private set; }
        public string Crop {get; private set; }
        public float Confidence { get; private set; }
        public string MovieID { get; private set; }
        public float MovieConfidence { get; private set; }
        public int TrackID { get; private set; }
        public Texture2D CurrentTexture;

        public DetectedObject(float centreX, float centreY, float width, float height, int cocoClass, string cocoName, string crop, float confidence, string movieID, float movieConfidence, int trackID)
        {
            CocoClass = cocoClass;
            CocoName = cocoName;
            Crop = crop;
            Confidence = confidence;
            BoundingBox = new Rect((int)centreX-(width/2), (int)centreY-(height/2), (int)width, (int)height);
            MovieID = movieID;
            MovieConfidence = movieConfidence;
            TrackID = trackID;
            CurrentTexture = null;
        }
    }
}
