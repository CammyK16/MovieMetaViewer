using System;
using System.Collections;
#if UNITY_6000_2_OR_NEWER
using Unity.InferenceEngine;
#else

#endif
using UnityEngine;
using YOLOTools.Inference;
using YOLOTools.YOLO.ObjectDetection.Utilities;

namespace YOLOTools.YOLO.ObjectDetection
{
    public class YOLOInferenceHandler : InferenceHandler<Texture2D>, IDisposable
    {
        private readonly TextureAnalyser _textureAnalyser;
        private readonly int _size;

        private readonly float _iouThreshold = 0.5f;
        private readonly float _scoreThreshold = 0.5f;


        public YOLOInferenceHandler(YOLOModel model, ref int size, Unity.InferenceEngine.BackendType backendType = Unity.InferenceEngine.BackendType.GPUCompute)
        {
            _model = model.Model;

            if (_model.inputs[0].shape.Get(2) != -1) size = _model.inputs[0].shape.Get(2);
            _size = size;

            _worker = new Unity.InferenceEngine.Worker(_model, backendType);
            _textureAnalyser = new TextureAnalyser(_worker);
        }

        public override Awaitable<Unity.InferenceEngine.Tensor<float>> Run(Texture2D input)
        {
            //Texture2D inputTexture = new(input.width, input.height, input.format, false);
            //Graphics.CopyTexture(input, inputTexture);

            //ResizeTool.Resize(input, _size, _size, false, input.filterMode);

            return _textureAnalyser.AnalyseTexture(input, _size);
        }

        public override IEnumerator RunWithLayerControl(Texture2D input)
        {
            //Texture2D inputTexture = new(input.width, input.height, input.format, false);
            //Graphics.CopyTexture(input, inputTexture);

            //ResizeTool.Resize(input, _size, _size, false, input.filterMode);

            return _textureAnalyser.AnalyseTextureWithLayerControl(input, _size);
        }

        public override void DisposeTensors()
        {
            _textureAnalyser.Dispose();
        }

        public override Unity.InferenceEngine.Tensor PeekOutput()
        {
            return _worker.PeekOutput();
        }

        public void Dispose()
        {
            _textureAnalyser?.Dispose();
        }
    }
}