using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyBox;
using TMPro;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using YOLOTools.Utilities;
using YOLOTools.YOLO.Display;
using YOLOTools.YOLO.RemoteYOLO;

namespace YOLOTools.YOLO
{
    public class RemoteYOLOHandler : YOLOProvider
    {

        #region Inputs

        [Tooltip("The network address (including port number if not using standard HTTP port 80) of the device running the remoteyolo processing server.")]
        [MustBeAssigned][SerializeField]
        public string m_remoteYOLOProcessorAddress;
        [SerializeField] private YOLOFormat m_YOLOFormat;
        [ConditionalField(nameof(m_useCustomModel), true)][SerializeField] public YOLOModel m_YOLOModel;
        [Tooltip("A custom YOLO model in .pt format. This field takes a file with a .bytes extension. Importing a .pt file into the project will automatically convert it to the correct format.")]
        [ConditionalField(nameof(m_useCustomModel))][SerializeField] private TextAsset m_customModel;
        [SerializeField] public bool m_useCustomModel;
        [Space(30f)]
        [Tooltip("The threshold below which a detection will be ignored.")]
        [SerializeField][Range(0f, 1f)] public float m_confidenceThreshold = 0.5f;
        [Space(30f)]
        [Tooltip("The ObjectDisplayManager that will handle the spawning of digital double models.")]
        [SerializeField][DisplayInspector] private ObjectDisplayManager m_objectDisplayManager;
        [Tooltip("The VideoFeedManager to analyse frames from.")]
        [MustBeAssigned] public VideoFeedManager YOLOCamera;
        [MustBeAssigned]
        [Tooltip("The base camera for scene analysis")]
        [SerializeField] private Camera m_referenceCamera;
        [Space(30f)]
        [SerializeField] private OVRInput.RawButton m_stopInferenceButton = OVRInput.RawButton.A;
        private bool shouldRun = true;
        [SerializeField] private Canvas m_settingsCanvas;

        #endregion

        #region Internal Variables

        private Texture2D m_inputTexture;
        private Camera m_analysisCamera;
        private TMP_Dropdown m_blockingModeDropdown;

        [SerializeField][Range(1, 4)] private int m_maxInFlightRequests = 2;
        [SerializeField][Range(1, 4)] private int m_maxResponsesPerFrame = 1;

        private int m_inFlightRequestCount;
        private long m_totalRequestsStarted;
        private long m_totalRequestsCompleted;
        private float m_nextThrottleLogTime;
        private string m_lastBlockingMode = "blur";
        private int m_lastBlockingModeIndex = 0;

        private readonly Queue<InferenceCompletion> m_inferenceCompletions = new Queue<InferenceCompletion>();
        private readonly object m_inferenceQueueLock = new object();
        private static readonly string[] BlockingModes = { "blur", "desaturated", "black", "borders" };
        
        #endregion
        
        public RemoteYOLOClient m_remoteYOLOClient;
        
        private void Start()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                Permission.RequestUserPermission("internet");
            }
            
            if (!TryGetComponent(out m_analysisCamera))
            {
                m_analysisCamera = gameObject.AddComponent<Camera>();
                m_analysisCamera.enabled = true;
                m_analysisCamera.clearFlags = CameraClearFlags.SolidColor;
                m_analysisCamera.backgroundColor = Color.clear;
                m_analysisCamera.stereoTargetEye = StereoTargetEyeMask.None;
                m_analysisCamera.targetDisplay = 7;
            }
            
            File.Delete(Path.Join(Application.persistentDataPath, "metrics.txt"));
            File.Create(Path.Join (Application.persistentDataPath, "metrics.txt")).Close();

            m_remoteYOLOClient = new RemoteYOLOClient(m_remoteYOLOProcessorAddress);
            m_blockingModeDropdown = m_settingsCanvas != null
                ? m_settingsCanvas.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(d => d.name == "BlockingModeDropdown")
                : null;
            
            if (m_useCustomModel)
            {
                try
                {
                    m_remoteYOLOClient.UploadCustomModel(m_customModel.bytes);
                }
                catch (Exception e)
                {
                    Debug.LogError("Couldn't upload custom model: " + e.Message);
                    m_useCustomModel = false;
                }
            }

            m_remoteYOLOClient.Reset();
        }

        private void Update()
        {
            // Toggle running inference if A button is pressed, or we have selected a movie to view
            // if (OVRInput.GetUp(m_stopInferenceButton))
            // {
            //     shouldRun = shouldRun ? false : true;
            // } else
            
            shouldRun = !m_objectDisplayManager.isFocusMode;

            if (!shouldRun) return;

            try
            {
                ProcessCompletedInferences();

                if (!TryAcquireRequestSlot()) return;

                if (!YOLOCamera)
                {
                    ReleaseRequestSlot();
                    return;
                }

                if (!(m_inputTexture = YOLOCamera.GetTexture()))
                {
                    ReleaseRequestSlot();
                    return;
                }

                Interlocked.Increment(ref m_totalRequestsStarted);
                _ = AnalyseImage(m_inputTexture, CaptureCameraState(m_referenceCamera));

                if (Time.unscaledTime >= m_nextThrottleLogTime)
                {
                    m_nextThrottleLogTime = Time.unscaledTime + 5f;
                    var inFlight = Volatile.Read(ref m_inFlightRequestCount);
                    var started = Interlocked.Read(ref m_totalRequestsStarted);
                    var completed = Interlocked.Read(ref m_totalRequestsCompleted);
                    Debug.Log($"RemoteYOLO throttle: inFlight={inFlight}/{m_maxInFlightRequests}, started={started}, completed={completed}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public async Awaitable UploadCustomModelAsync()
        {
            try
            {
                await m_remoteYOLOClient.UploadCustomModelAsync(m_customModel.bytes);
                m_useCustomModel = true;
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't upload custom model: " + e.Message);
                m_useCustomModel = false;
            }
        }

        private static byte[] EncodeImageJPG(ImageConversionThreadParams p)
        {
            return ImageConversion.EncodeArrayToJPG(p.imageBuffer, p.graphicsFormat, p.width, p.height, quality: p.quality);
        }

        private async Awaitable AnalyseImage(Texture2D texture, CameraState cameraState)
        {
            var imageConversionThreadParams = new ImageConversionThreadParams
            {
                imageBuffer = texture.GetRawTextureData(),
                graphicsFormat = texture.graphicsFormat,
                height = (uint)texture.height,
                width = (uint)texture.width,
                quality = 75
            };
            byte[] imageData;

            try
            {
                imageData = await Task.Run(() => EncodeImageJPG(imageConversionThreadParams));
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't encode image: " + e.Message);
                ReleaseRequestSlot();
                return;
            }

            string blockingMode = GetBlockingMode();

            try
            {
                var response = await m_remoteYOLOClient.AnalyseAsync(imageData, m_YOLOModel, m_YOLOFormat, m_useCustomModel, blockingMode);
                lock (m_inferenceQueueLock)
                {
                    m_inferenceCompletions.Enqueue(new InferenceCompletion(response, cameraState));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't analyse image: " + e.Message);
            }
            finally
            {
                Interlocked.Increment(ref m_totalRequestsCompleted);
                ReleaseRequestSlot();
            }
        }

        private bool TryAcquireRequestSlot()
        {
            while (true)
            {
                var current = Volatile.Read(ref m_inFlightRequestCount);
                if (current >= m_maxInFlightRequests)
                {
                    return false;
                }

                var next = current + 1;
                if (Interlocked.CompareExchange(ref m_inFlightRequestCount, next, current) == current)
                {
                    return true;
                }
            }
        }

        private void ReleaseRequestSlot()
        {
            var next = Interlocked.Decrement(ref m_inFlightRequestCount);
            if (next >= 0)
            {
                return;
            }

            // Guard against accidental over-release so throttling remains valid.
            Interlocked.Exchange(ref m_inFlightRequestCount, 0);
            Debug.LogWarning("RemoteYOLOHandler: request slot counter underflow detected; counter reset to 0.");
        }

        private void ProcessCompletedInferences()
        {
            var processed = 0;
            while (processed < m_maxResponsesPerFrame)
            {
                InferenceCompletion completion;
                lock (m_inferenceQueueLock)
                {
                    if (m_inferenceCompletions.Count == 0) break;
                    completion = m_inferenceCompletions.Dequeue();
                }

                ApplyCameraState(completion.cameraState);

                var detectedObjects = YOLOPostProcessor.RemoteYOLOPostprocess(completion.response, m_confidenceThreshold);
                OnDetectedObjectsUpdated(detectedObjects);
                if (m_objectDisplayManager) m_objectDisplayManager.DisplayModels(detectedObjects, m_analysisCamera);

                processed++;
            }
        }

        private CameraState CaptureCameraState(Camera source)
        {
            if (source == null)
            {
                return default;
            }

            return new CameraState
            {
                position = source.transform.position,
                rotation = source.transform.rotation,
                fieldOfView = source.fieldOfView,
                orthographic = source.orthographic,
                orthographicSize = source.orthographicSize,
                nearClipPlane = source.nearClipPlane,
                farClipPlane = source.farClipPlane,
                aspect = source.aspect
            };
        }

        private void ApplyCameraState(CameraState state)
        {
            if (m_analysisCamera == null || m_referenceCamera == null)
            {
                return;
            }

            m_analysisCamera.CopyFrom(m_referenceCamera);
            m_analysisCamera.transform.SetPositionAndRotation(state.position, state.rotation);
            m_analysisCamera.fieldOfView = state.fieldOfView;
            m_analysisCamera.orthographic = state.orthographic;
            m_analysisCamera.orthographicSize = state.orthographicSize;
            m_analysisCamera.nearClipPlane = state.nearClipPlane;
            m_analysisCamera.farClipPlane = state.farClipPlane;
            m_analysisCamera.aspect = state.aspect;
        }

        private string GetBlockingMode()
        {
            if (m_blockingModeDropdown == null)
            {
                return m_lastBlockingMode;
            }

            var index = Mathf.Clamp(m_blockingModeDropdown.value, 0, BlockingModes.Length - 1);
            if (index != m_lastBlockingModeIndex)
            {
                m_lastBlockingModeIndex = index;
                m_lastBlockingMode = BlockingModes[index];
            }

            return m_lastBlockingMode;
        }
        
        private class ImageConversionThreadParams
        {
            public byte[] imageBuffer;
            public GraphicsFormat graphicsFormat;
            public uint width;
            public uint height;
            public int quality;
        }

        private struct CameraState
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView;
            public bool orthographic;
            public float orthographicSize;
            public float nearClipPlane;
            public float farClipPlane;
            public float aspect;
        }

        private readonly struct InferenceCompletion
        {
            public readonly RemoteYOLOAnalyseResponse response;
            public readonly CameraState cameraState;

            public InferenceCompletion(RemoteYOLOAnalyseResponse response, CameraState cameraState)
            {
                this.response = response;
                this.cameraState = cameraState;
            }
        }
    }

       
}