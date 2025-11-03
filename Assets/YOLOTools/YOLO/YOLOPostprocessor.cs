using System.Collections.Generic;
#if UNITY_6000_2_OR_NEWER
using Unity.InferenceEngine;
#else

#endif
using UnityEngine;
using UnityEngine.Profiling;
using YOLOTools.YOLO.ObjectDetection;
using YOLOTools.YOLO.RemoteYOLO;

namespace YOLOTools.YOLO
{
    public static class YOLOPostProcessor
    {
        public static List<DetectedObject> PostProcess(Unity.InferenceEngine.Tensor<float> result, Texture2D inputTexture, int inputSize,
            Dictionary<int, string> classes, float confidenceThreshold)
        {
            Profiler.BeginSample("YOLO.Postprocess");

            List<DetectedObject> objects = new();
            float widthScale = inputTexture.width / (float)inputSize;
            float heightScale = inputTexture.height / (float)inputSize;

            for (int i = 0; i < result.shape[2]; i++)
            {
                float confidence = result[0, 5, i];
                if (confidence < confidenceThreshold) continue;
                int cocoClass = (int)result[0, 4, i];
                float centerX = result[0, 0, i] * widthScale;
                float centerY = result[0, 1, i] * heightScale;
                float width = result[0, 2, i] * widthScale;
                float height = result[0, 3, i] * heightScale;

                var className = classes.GetValueOrDefault(cocoClass, null);
                
                objects.Add(new DetectedObject(centerX, centerY, width, height, cocoClass, className,
                    confidence));
            }

            objects.Sort((x, y) => y.Confidence.CompareTo(x.Confidence));

            Profiler.EndSample();

            return objects;
        }
        
        public static List<DetectedObject> RemoteYOLOPostprocess(RemoteYOLOAnalyseResponse response, float confidenceThreshold)
        {
            List<DetectedObject> results = new();
            foreach (RemoteYOLOAnalysePredictionResult obj in response.result)
            {
                if (obj.confidence < confidenceThreshold) continue;
                var cx = (obj.box.x1 + obj.box.x2) / 2;
                var cy = (obj.box.y1 + obj.box.y2) / 2;
                var width = (obj.box.x2 - obj.box.x1);
                var height = (obj.box.y2 - obj.box.y1);
                results.Add(new DetectedObject(cx, cy, width, height, obj.class_id, obj.name, obj.confidence));
            }

            return results;
        }
    }
}