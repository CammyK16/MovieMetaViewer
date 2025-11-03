using MyBox;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
#if UNITY_6000_2_OR_NEWER
using Unity.InferenceEngine;
#else

#endif
using UnityEngine;
using UnityEngine.Profiling;
using YOLOTools.Inference;
using YOLOTools.Utilities;
using YOLOTools.YOLO.Display;
using YOLOTools.YOLO.ObjectDetection;
using YOLOTools.YOLO.ObjectDetection.Utilities;

namespace YOLOTools.YOLO
{
    public class YOLOHandler : YOLOProvider
    {
        #region Inputs

        [Tooltip("The YOLO model to run.")] [MustBeAssigned] [SerializeField]
        private Unity.InferenceEngine.ModelAsset _model;

        [Tooltip(
            "The size of the input image to the model. This will be overwritten if the model has a fixed input size.")]
        [SerializeField]
        private int InputSize = 640;

        [Tooltip("The number of model layers to run per frame. Increasing this value will decrease performance.")]
        [MinValue(1)]
        [SerializeField]
        public uint _layersPerFrame = 10;

        [Tooltip("The threshold at which a detection is accepted.")] [MinValue(0), MaxValue(1)] [SerializeField]
        public float _confidenceThreshold = 0.5f;

        [Tooltip("A JSON containing a mapping of class numbers to class names")] [MustBeAssigned] [SerializeField]
        private TextAsset _classJson;

        [Tooltip("The VideoFeedManager to analyse frames from.")]
        public VideoFeedManager YOLOCamera;

        [Tooltip("The base camera for scene analysis")] [MustBeAssigned] [SerializeField]
        private Camera _referenceCamera;

        [Tooltip("The ObjectDisplayManager that will handle the spawning of digital double models.")]
        [DisplayInspector]
        [SerializeField]
        private ObjectDisplayManager _displayManager;


        [Space(30)] [SerializeField] private bool _customizeModel = false;

        [Header("YOLO Model Parameters")]
        [Tooltip("Add a classification head to the model to select the most likely class for each detection.")]
        [ConditionalField(nameof(_customizeModel))]
        [SerializeField]
        private bool _addClassificationHead = false;

        [Tooltip("Level of quantization to perform, if any.")]
        [ConditionalField(nameof(_customizeModel))]
        [SerializeField]
        private YOLOQuantizationType _quantizationType = YOLOQuantizationType.None;

        [Tooltip("Beckend to use for neural network inference.")]
        [ConditionalField(nameof(_customizeModel))]
        [SerializeField]
        private Unity.InferenceEngine.BackendType _backendType = Unity.InferenceEngine.BackendType.GPUCompute;


        [Tooltip("Add Non-Max Suppression to the output of the model.")]
        [ConditionalField(nameof(_customizeModel), nameof(_addClassificationHead))]
        [SerializeField]
        private bool _addNMS = false;

        [Tooltip("The IOU threshold for Non-Max Suppression.")]
        [ConditionalField(nameof(_customizeModel), nameof(_addNMS), nameof(_addClassificationHead))]
        [SerializeField]
        [Range(0, 1)]
        private float _iouThreshold = 0.5f;

        [Tooltip("The Score threshold for Non-Max Suppression.")]
        [ConditionalField(nameof(_customizeModel), nameof(_addNMS), nameof(_addClassificationHead))]
        [SerializeField]
        [Range(0, 1)]
        private float _scoreThreshold = 0.5f;


        public Camera ReferenceCamera
        {
            get => _referenceCamera;
            private set => _referenceCamera = value;
        }

        #endregion

        #region InstanceFields

        private InferenceHandler<Texture2D> _inferenceHandler;

        private bool inferencePending;
        private bool readingBack;
        private Unity.InferenceEngine.Tensor<float> analysisResultTensor;
        private Texture2D _inputTexture;
        private IEnumerator splitInferenceEnumerator;

        private Camera _analysisCamera;
            
        #endregion

        #region Data

        private Dictionary<int, string> _classes = new();

        #endregion

        void Start()
        {
            _classes =
                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, string>>>(
                    _classJson.text)["class"];
            YOLOModel yoloModel;
            if (_customizeModel)
            {
                if (!YOLOCustomizer.CustomizeModel(_model,
                        new YOLOCustomizationParameters(_addClassificationHead, _quantizationType, _addNMS,
                            _iouThreshold, _scoreThreshold), out yoloModel))
                {
                    throw new ArgumentException("YOLO Model could not be customized.");
                }
            }
            else yoloModel = new YOLOModel(Unity.InferenceEngine.ModelLoader.Load(_model));

            _inferenceHandler = new YOLOInferenceHandler(yoloModel, ref InputSize, backendType: _backendType);
            if (_layersPerFrame <= 0) _layersPerFrame = 1;
            if (!TryGetComponent(out _analysisCamera))
            {
                _analysisCamera = gameObject.AddComponent<Camera>();
                _analysisCamera.enabled = true;
                _analysisCamera.clearFlags = CameraClearFlags.SolidColor;
                _analysisCamera.backgroundColor = Color.clear;
                _analysisCamera.stereoTargetEye = StereoTargetEyeMask.None;
                _analysisCamera.targetDisplay = 7;
            }
        }

        void Update()
        {
            if (_classes is null) return;

            if (_inferenceHandler is null) return;

            if (!YOLOCamera) return;

            if (readingBack) return;

            try
            {
                if (!inferencePending)
                {
                    Profiler.BeginSample("YOLOHandler.Setup");

                    if (!(_inputTexture = YOLOCamera.GetTexture())) return;
                    splitInferenceEnumerator = _inferenceHandler.RunWithLayerControl(_inputTexture);
                    inferencePending = true;
                    _analysisCamera.CopyFrom(ReferenceCamera);

                    Profiler.EndSample();
                }

                if (inferencePending)
                {
                    int it = 0;
                    Profiler.BeginSample("YOLOHandler.SplitInference");
                    while (splitInferenceEnumerator.MoveNext())
                        if (++it % _layersPerFrame == 0)
                        {
                            Profiler.EndSample();
                            return;
                        }

                    readingBack = true;
                    analysisResultTensor = _inferenceHandler.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
                    var analysisResult = analysisResultTensor?.ReadbackAndCloneAsync().GetAwaiter();
                    analysisResult?.OnCompleted(() =>
                    {
                        try
                        {
                            Profiler.BeginSample("YOLOHandler.GetResult");
                            analysisResultTensor = analysisResult.Value.GetResult();
                            Profiler.EndSample();
                            readingBack = false;

                            var detectedObjects = YOLOPostProcessor.PostProcess(analysisResultTensor, _inputTexture,
                                InputSize, _classes, _confidenceThreshold);
                            analysisResultTensor.Dispose();
                            _inferenceHandler.DisposeTensors();
                            inferencePending = false;
                            analysisResultTensor = null;
                            
                            OnDetectedObjectsUpdated(detectedObjects);
                            if (_displayManager) _displayManager.DisplayModels(detectedObjects, _analysisCamera);
                        }
                        catch
                        {
                            analysisResultTensor?.Dispose();
                            analysisResultTensor = null;
                            inferencePending = false;
                            _inferenceHandler.DisposeTensors();
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                analysisResultTensor?.Dispose();
                analysisResultTensor = null;
                _inferenceHandler.DisposeTensors();
                inferencePending = false;
            }
        }
    }
}