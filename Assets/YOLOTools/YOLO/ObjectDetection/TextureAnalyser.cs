using System;
using System.Collections;
using UnityEngine;
#if UNITY_6000_2_OR_NEWER
using Unity.InferenceEngine;
#else

#endif

namespace YOLOTools.YOLO.ObjectDetection
{
    public class TextureAnalyser : IDisposable
    {
        private readonly Unity.InferenceEngine.Worker _worker;
        private Unity.InferenceEngine.Tensor<float> _input;

        public TextureAnalyser(Unity.InferenceEngine.Worker worker)
        {
            _worker = worker;
        }

        public Awaitable<Unity.InferenceEngine.Tensor<float>> AnalyseTexture(Texture2D texture, int size)
        {
            Unity.InferenceEngine.TextureTransform textureTransform = new Unity.InferenceEngine.TextureTransform().SetChannelSwizzle().SetDimensions(size, size, 3);
            _input = Unity.InferenceEngine.TextureConverter.ToTensor(texture, textureTransform);

            _worker.Schedule(_input);

            var tensor = _worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
            var output = tensor.ReadbackAndCloneAsync();
            return output;
        }

        public IEnumerator AnalyseTextureWithLayerControl(Texture2D texture, int size)
        {
            Unity.InferenceEngine.TextureTransform textureTransform = new Unity.InferenceEngine.TextureTransform().SetChannelSwizzle().SetDimensions(size, size, 3);
            _input = Unity.InferenceEngine.TextureConverter.ToTensor(texture, textureTransform);

            var output = _worker.ScheduleIterable(_input);

            return output;
        }


        public void Dispose()
        {
            _worker?.Dispose();
            _input?.Dispose();
        }
    }
}