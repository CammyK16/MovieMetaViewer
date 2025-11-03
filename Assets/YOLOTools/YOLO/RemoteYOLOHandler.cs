using System;
using System.IO;
using System.Threading.Tasks;
using MyBox;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Experimental.Rendering;
using YOLOTools.Utilities;
using YOLOTools.YOLO.Display;
using YOLOTools.YOLO.RemoteYOLO;

namespace YOLOTools.YOLO
{
    public class RemoteYOLOHandler : YOLOProvider
    {

        #region Inputs
        
        [Tooltip("The network address (including port number if not using standard HTTP port 80) of the device running the remoteyolo processing server.")]
        [MustBeAssigned] [SerializeField]
        public string m_remoteYOLOProcessorAddress;
        [SerializeField] private YOLOFormat m_YOLOFormat;
        [ConditionalField(nameof(m_useCustomModel), true)] [SerializeField] public YOLOModel m_YOLOModel;
        [Tooltip("A custom YOLO model in .pt format. This field takes a file with a .bytes extension. Importing a .pt file into the project will automatically convert it to the correct format.")]
        [ConditionalField(nameof(m_useCustomModel))] [SerializeField] private TextAsset m_customModel;
        [SerializeField] public bool m_useCustomModel;
        [Space(30f)]
        [Tooltip("The threshold below which a detection will be ignored.")]
        [SerializeField] [Range(0f,1f)] public float m_confidenceThreshold = 0.5f;
        [Space(30f)]
        [Tooltip("The ObjectDisplayManager that will handle the spawning of digital double models.")]
        [SerializeField] [DisplayInspector] private ObjectDisplayManager m_objectDisplayManager;
        [Tooltip("The VideoFeedManager to analyse frames from.")]
        [MustBeAssigned] public VideoFeedManager YOLOCamera;
        [MustBeAssigned]
        [Tooltip("The base camera for scene analysis")]
        [SerializeField] private Camera m_referenceCamera;

        #endregion
        
        #region Internal Variables
        
        private Texture2D m_inputTexture;
        private bool m_inferencePending = false;
        private bool m_inferenceDone = false;
        private RemoteYOLOAnalyseResponse m_remoteYOLOResponse;
        private Camera m_analysisCamera;

        private byte[] m_imageData;
        
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
        }

        private void Update()
        {
            if (m_inferencePending) return;
            
            try
            {
                if (!m_inferenceDone)
                {
                    if (!YOLOCamera) return;
                    if (!(m_inputTexture = YOLOCamera.GetTexture())) return;
                    _ = AnalyseImage(m_inputTexture);
                    m_inferencePending = true;
                    m_analysisCamera.CopyFrom(m_referenceCamera);
                }
                else
                {
                    m_inferencePending = false;
                    m_inferenceDone = false;
                    var detectedObjects = YOLOPostProcessor.RemoteYOLOPostprocess(m_remoteYOLOResponse, m_confidenceThreshold);
                    OnDetectedObjectsUpdated(detectedObjects);
                    if (m_objectDisplayManager) m_objectDisplayManager.DisplayModels(detectedObjects, m_analysisCamera);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                m_inferencePending = false;
                m_inferenceDone = false;
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

        private void EncodeImageJPG(object paras)
        {
            var p = (ImageConversionThreadParams)paras;
            m_imageData = ImageConversion.EncodeArrayToJPG(p.imageBuffer, p.graphicsFormat, p.width, p.height, quality: p.quality);
        }

        private async Awaitable AnalyseImage(Texture2D texture)
        {
            var imageConversionThreadParams = new ImageConversionThreadParams
            {
                imageBuffer = texture.GetRawTextureData(),
                graphicsFormat = texture.graphicsFormat,
                height = (uint)texture.height,
                width = (uint)texture.width,
                quality = 75
            };
            
            await Task.Run(() => EncodeImageJPG(imageConversionThreadParams));

            try
            {
                m_remoteYOLOResponse = await m_remoteYOLOClient.AnalyseAsync(m_imageData, m_YOLOModel, m_YOLOFormat, m_useCustomModel);
                m_inferenceDone = true;
                m_inferencePending = false;
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't analyse image: " + e.Message);
                m_inferenceDone = false;
                m_inferencePending = false;
            }
        }
        
        private class ImageConversionThreadParams
        {
            public byte[] imageBuffer;
            public GraphicsFormat graphicsFormat;
            public uint width;
            public uint height;
            public int quality;
        }
    }

       
}