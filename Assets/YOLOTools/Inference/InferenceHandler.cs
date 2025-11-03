using System.Collections;
#if UNITY_6000_2_OR_NEWER
using Unity.InferenceEngine;
#else

#endif
using UnityEngine;

namespace YOLOTools.Inference
{
    public abstract class InferenceHandler<T>
    {
        protected Unity.InferenceEngine.Model _model;
        protected Unity.InferenceEngine.Worker _worker;

        public abstract Awaitable<Unity.InferenceEngine.Tensor<float>> Run(T input);

        public abstract IEnumerator RunWithLayerControl(T input);

        public abstract Unity.InferenceEngine.Tensor PeekOutput();

        public abstract void DisposeTensors();
    }
}