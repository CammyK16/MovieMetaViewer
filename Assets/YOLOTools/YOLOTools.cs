using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
#if UNITY_6000_2_OR_NEWER
using Unity.InferenceEngine;
#else

#endif
using UnityEngine;
using YOLOTools.YOLO.ObjectDetection;
using YOLOTools.YOLO.ObjectDetection.Utilities;
using YOLOTools.YOLO;
using YOLOTools.YOLO.RemoteYOLO;
using YOLOModel = YOLOTools.YOLO.RemoteYOLO.YOLOModel;

namespace YOLOTools
{
    public class YOLOTools
    {
        /// <summary>
        /// Analyses the provided texture using the provided YOLO model. This method schedules the analysis on the GPU and uses the
        /// async/await paradigm to return control until this is completed. This method runs the analyses greedily which can cause
        /// performance issues on limited GPUs.
        /// </summary>
        /// <param name="texture">The texture to analyse.</param>
        /// <param name="modelAsset">The model to use for analysis.</param>
        /// <param name="yoloAnalysisParameters">The YOLO analysis settings.</param>
        /// <param name="customizationParameters">The model customization settings.</param>
        /// <exception cref="ArgumentException">Thrown if the provided <paramref name="modelAsset"/> could not be customized, likely due to an incorrect output format.</exception>
        /// <returns>Returns a list of <see cref="DetectedObject"/>s which represent any detections in the given texture.</returns>
        public static async Task<List<DetectedObject>> YOLOAnalyseAsync(Texture2D texture, Unity.InferenceEngine.ModelAsset modelAsset, YOLOAnalysisParameters yoloAnalysisParameters, YOLOCustomizationParameters customizationParameters)
        {
            if (!YOLOCustomizer.CustomizeModel(modelAsset, customizationParameters, out var yoloModel))
                throw new ArgumentException("Could not customize provided YOLO model asset.");
            var size = yoloAnalysisParameters.Size;
            var inferenceHandler = new YOLOInferenceHandler(yoloModel, ref size, yoloAnalysisParameters.BackendType);
            
            var result = await inferenceHandler.Run(texture);

            var objects = YOLOPostProcessor.PostProcess(result, texture, size, yoloAnalysisParameters.Classes, yoloAnalysisParameters.ConfidenceThreshold);
            result?.Dispose();
            inferenceHandler.Dispose();
            return objects;
        }

        /// <summary>
        /// Analyses the provided texture using the provided YOLO model. This method schedules the analysis on the GPU and waits until the analysis is
        /// complete before returning. This method runs the analyses greedily which can cause
        /// performance issues on limited GPUs.
        /// </summary>
        /// <param name="texture">The texture to analyse.</param>
        /// <param name="modelAsset">The model to use for analysis.</param>
        /// <param name="yoloAnalysisParameters">The YOLO analysis settings.</param>
        /// <param name="customizationParameters">The model customization settings.</param>
        /// <exception cref="ArgumentException">Thrown if the provided <paramref name="modelAsset"/> could not be customized, likely due to an incorrect output format.</exception>
        /// <returns>Returns a list of <see cref="DetectedObject"/>s which represent any detections in the given texture.</returns>
        public static List<DetectedObject> YOLOAnalyse(Texture2D texture, Unity.InferenceEngine.ModelAsset modelAsset,
            YOLOAnalysisParameters yoloAnalysisParameters, YOLOCustomizationParameters customizationParameters)
        {
            if (!YOLOCustomizer.CustomizeModel(modelAsset, customizationParameters, out var yoloModel))
                throw new ArgumentException("Could not customize provided YOLO model asset.");            var size = yoloAnalysisParameters.Size;
            var inferenceHandler = new YOLOInferenceHandler(yoloModel, ref size, yoloAnalysisParameters.BackendType);

            var result = inferenceHandler.Run(texture).GetAwaiter().GetResult();

            var objects = YOLOPostProcessor.PostProcess(result, texture, size, yoloAnalysisParameters.Classes, yoloAnalysisParameters.ConfidenceThreshold);
            result?.Dispose();
            inferenceHandler.Dispose();
            return objects;
        }
        
        /// <summary>
        /// Analyses the provided texture using the provided YOLO model. This method schedules <paramref name="layersPerFrame"/> layers for analysis on the GPU per frame and uses the
        /// async/await paradigm to return control during each frame as these are being executed. This method is most useful
        /// when it is necessary to restrict GPU processing time to allow other graphics operations to take place, such as to
        /// reduce frame rendering latency.
        /// </summary>
        /// <param name="texture">The texture to analyse.</param>
        /// <param name="modelAsset">The model to use for analysis.</param>
        /// <param name="yoloAnalysisParameters">The YOLO analysis settings.</param>
        /// <param name="customizationParameters">The YOLO model customization settings.</param>
        /// <param name="layersPerFrame">The number of layers to execute per frame. Increasing this number will increase the GPU latency per frame.</param>
        /// <exception cref="ArgumentException">Thrown if the provided <paramref name="modelAsset"/> could not be customized, likely due to an incorrect output format.</exception>
        /// <returns>Returns a list of <see cref="DetectedObject"/>s which represent any detections in the given texture.</returns>
        public static async Task<List<DetectedObject>> YOLOAnalyseAsync(Texture2D texture, Unity.InferenceEngine.ModelAsset modelAsset,
            YOLOAnalysisParameters yoloAnalysisParameters, YOLOCustomizationParameters customizationParameters, uint layersPerFrame)
        {
            if (!YOLOCustomizer.CustomizeModel(modelAsset, customizationParameters, out var yoloModel))
                throw new ArgumentException("Could not customize provided YOLO model asset.");            var size = yoloAnalysisParameters.Size;
            var inferenceHandler = new YOLOInferenceHandler(yoloModel, ref size, yoloAnalysisParameters.BackendType);

            var splitInferenceEnumerator = inferenceHandler.RunWithLayerControl(texture);

            int it = 0;
            while (splitInferenceEnumerator.MoveNext())
                if (++it % layersPerFrame == 0)
                {
                    await Awaitable.NextFrameAsync();
                    it = 0;
                }

            var result = await inferenceHandler.PeekOutput().ReadbackAndCloneAsync() as Unity.InferenceEngine.Tensor<float>;
            
            var objects = YOLOPostProcessor.PostProcess(result, texture, size, yoloAnalysisParameters.Classes,
                yoloAnalysisParameters.ConfidenceThreshold);
            result?.Dispose();
            inferenceHandler.Dispose();
            return objects;

        }

        /// <summary>
        /// Analyses the provided texture using the specified <paramref name="yoloModel"/> on the remoteyolo server at <paramref name="remoteYOLOAddress"/> asynchronously.
        /// <paramref name="remoteYOLOAddress"/>.
        /// </summary>
        /// <param name="texture">The texture to analyse.</param>
        /// <param name="remoteYOLOAddress">The network address of the <a href="https://github.com/matthewlyon23/remoteyolo">remoteyolo</a> server
        /// to use for analysis.</param>
        /// <param name="confidenceThreshold">The confidence threshold below which to ignore detections.</param>
        /// <param name="yoloModel">The YOLO11 model to use for analysis.</param>
        /// <param name="yoloFormat">The format to execute the model in.</param>
        /// <param name="imageFormat">The image format to convert the texture to before uploading.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided <paramref name="imageFormat"/> is not valid.</exception>
        /// <returns>Returns a list of <see cref="DetectedObject"/>s which represent any detections in the given texture.</returns>
        public static async Task<List<DetectedObject>> RemoteYOLOAnalyseAsync(Texture2D texture, string remoteYOLOAddress, float confidenceThreshold = 0.5f, YOLOModel yoloModel = YOLOModel.YOLO11N, YOLOFormat yoloFormat = YOLOFormat.NCNN, RemoteYOLOImageFormat imageFormat = RemoteYOLOImageFormat.JPG)
        {
            var client = new RemoteYOLOClient(remoteYOLOAddress);

            var imageData = imageFormat switch
            {
                RemoteYOLOImageFormat.JPG => Texture2DToJPGAsync(texture),
                RemoteYOLOImageFormat.PNG => Texture2DToPNGAsync(texture),
                _ => throw new ArgumentOutOfRangeException(nameof(imageFormat), imageFormat, null)
            };
            var result = client.AnalyseAsync(await imageData, yoloModel, yoloFormat);

            return YOLOPostProcessor.RemoteYOLOPostprocess(await result, confidenceThreshold);
        }
        
        /// <summary>
        /// Analyses the provided texture using the specified <paramref name="customModel"/> on the remoteyolo server at <paramref name="remoteYOLOAddress"/> asynchronously.
        /// </summary>
        /// <param name="texture">The texture to analyse.</param>
        /// <param name="customModel">The custom model to upload and use for analysis. (Will replace any existing uploaded custom model.)</param>
        /// <param name="remoteYOLOAddress">The network address of the <a href="https://github.com/matthewlyon23/remoteyolo">remoteyolo</a> server
        /// to use for analysis.</param>
        /// <param name="confidenceThreshold">The confidence threshold below which to ignore detections.</param>
        /// <param name="yoloFormat">The format to execute the model in.</param>
        /// <param name="imageFormat">The image format to convert the texture to before uploading.</param>
        /// <returns>Returns a list of <see cref="DetectedObject"/>s which represent any detections in the given texture.</returns>
        /// <exception cref="HttpRequestException">Thrown if there is no response or a failure response from the remoteyolo server, either in analysis or custom model uploading.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided <paramref name="imageFormat"/> is not valid.</exception>
        public static async Task<List<DetectedObject>> RemoteYOLOAnalyseAsync(Texture2D texture, byte[] customModel, string remoteYOLOAddress, float confidenceThreshold = 0.5f, YOLOFormat yoloFormat = YOLOFormat.NCNN, RemoteYOLOImageFormat imageFormat = RemoteYOLOImageFormat.JPG)
        {
            var client = new RemoteYOLOClient(remoteYOLOAddress);

            var customModelResponse = await client.UploadCustomModelAsync(customModel);
            if (!customModelResponse.success)
            {
                throw new HttpRequestException(customModelResponse.error);
            }
            
            var imageData = imageFormat switch
            {
                RemoteYOLOImageFormat.JPG => Texture2DToJPGAsync(texture),
                RemoteYOLOImageFormat.PNG => Texture2DToPNGAsync(texture),
                _ => throw new ArgumentOutOfRangeException(nameof(imageFormat), imageFormat, null)
            };
            var result = client.AnalyseAsync(await imageData, yoloFormat: yoloFormat, useCustomModel: true);

            return YOLOPostProcessor.RemoteYOLOPostprocess(await result, confidenceThreshold);
        }

        /// <summary>
        /// Analyses the provided texture using the specified <paramref name="yoloModel"/> on the remoteyolo server at <paramref name="remoteYOLOAddress"/>.
        /// </summary>
        /// <param name="texture">The texture to analyse.</param>
        /// <param name="remoteYOLOAddress">The network address of the <a href="https://github.com/matthewlyon23/remoteyolo">remoteyolo</a> server
        /// to use for analysis.</param>
        /// <param name="confidenceThreshold">The confidence threshold below which to ignore detections.</param>
        /// <param name="yoloModel">The YOLO11 model to use for analysis.</param>
        /// <param name="yoloFormat">The format to execute the model in.</param>
        /// <param name="imageFormat">The image format to convert the texture to before uploading.</param>
        /// <returns>Returns a list of <see cref="DetectedObject"/>s which represent any detections in the given texture.</returns>
        /// <exception cref="HttpRequestException">Thrown if there is no response or a failure response from the remoteyolo server, either in analysis or custom model uploading.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided <paramref name="imageFormat"/> is not valid.</exception>
        public static List<DetectedObject> RemoteYOLOAnalyse(Texture2D texture, string remoteYOLOAddress, float confidenceThreshold = 0.5f, YOLOModel yoloModel = YOLOModel.YOLO11N, YOLOFormat yoloFormat = YOLOFormat.NCNN, RemoteYOLOImageFormat imageFormat = RemoteYOLOImageFormat.JPG)
        {
            var client = new RemoteYOLOClient(remoteYOLOAddress);

            var imageData = imageFormat switch
            {
                RemoteYOLOImageFormat.JPG => Texture2DToJPGAsync(texture),
                RemoteYOLOImageFormat.PNG => Texture2DToPNGAsync(texture),
                _ => throw new ArgumentOutOfRangeException(nameof(imageFormat), imageFormat, null)
            };
            var result = client.Analyse(imageData.Result, yoloModel, yoloFormat);

            return YOLOPostProcessor.RemoteYOLOPostprocess(result, confidenceThreshold);
        }

        /// <summary>
        /// Analyses the provided texture using the specified <paramref name="customModel"/> on the remoteyolo server at <paramref name="remoteYOLOAddress"/>.
        /// </summary>
        /// <param name="texture">The texture to analyse.</param>
        /// <param name="customModel">The custom model to upload and use for analysis. (Will replace any existing uploaded custom model.)</param>
        /// <param name="remoteYOLOAddress">The network address of the <a href="https://github.com/matthewlyon23/remoteyolo">remoteyolo</a> server
        /// to use for analysis.</param>
        /// <param name="confidenceThreshold">The confidence threshold below which to ignore detections.</param>
        /// <param name="yoloFormat">The format to execute the model in.</param>
        /// <param name="imageFormat">The image format to convert the texture to before uploading.</param>
        /// <returns>Returns a list of <see cref="DetectedObject"/>s which represent any detections in the given texture.</returns>
        /// <exception cref="HttpRequestException">Thrown if there is no response or a failure response from the remoteyolo server, either in analysis or custom model uploading.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided <paramref name="imageFormat"/> is not valid.</exception>
        public static List<DetectedObject> RemoteYOLOAnalyse(Texture2D texture, byte[] customModel, string remoteYOLOAddress, float confidenceThreshold = 0.5f, YOLOFormat yoloFormat = YOLOFormat.NCNN, RemoteYOLOImageFormat imageFormat = RemoteYOLOImageFormat.JPG)
        {
            var client = new RemoteYOLOClient(remoteYOLOAddress);

            var customModelResponse = client.UploadCustomModel(customModel);
            if (!customModelResponse.success)
            {
                throw new HttpRequestException(customModelResponse.error);
            }
            
            var imageData = imageFormat switch
            {
                RemoteYOLOImageFormat.JPG => Texture2DToJPGAsync(texture),
                RemoteYOLOImageFormat.PNG => Texture2DToPNGAsync(texture),
                _ => throw new ArgumentOutOfRangeException(nameof(imageFormat), imageFormat, null)
            };
            var result = client.AnalyseAsync(imageData.Result, YOLOModel.YOLO11N, yoloFormat).GetAwaiter().GetResult();

            return YOLOPostProcessor.RemoteYOLOPostprocess(result, confidenceThreshold);
        }

        /// <summary>
        /// Uploads the specified <paramref name="customModel"/> to the <a href="https://github.com/matthewlyon23/remoteyolo">remoteyolo</a>
        /// server at <paramref name="remoteYOLOAddress"/> asynchronously.
        /// </summary>
        /// <param name="customModel">The custom YOLO model to upload.</param>
        /// <param name="remoteYOLOAddress">The network address of the remoteyolo server to use for analysis.</param>
        /// <returns>Returns a boolean which specifies whether the model was uploaded successfully or not.</returns>
        /// <exception cref="HttpRequestException">Throws an HttpRequestException on failure status code. The message field contains the error message from the server, if one exists.</exception>
        // (This is very badly designed, this will never return false because an exception will be thrown if there is a failure.)
        public static async Task<bool> UploadCustomModelAsync(byte[] customModel, string remoteYOLOAddress)
        {
            var client = new RemoteYOLOClient(remoteYOLOAddress);
            
            var customModelResponse = await client.UploadCustomModelAsync(customModel);

            return customModelResponse.success;
        }

        /// <summary>
        /// Uploads the specified <paramref name="customModel"/> to the <a href="https://github.com/matthewlyon23/remoteyolo">remoteyolo</a>
        /// server at <paramref name="remoteYOLOAddress"/>.
        /// </summary>
        /// <param name="customModel">The custom YOLO model to upload.</param>
        /// <param name="remoteYOLOAddress">The network address of the remoteyolo server to use for analysis.</param>
        /// <returns>Returns a boolean which specifies whether the model was uploaded successfully or not. (This is very badly designed, this will never return false because an exception will be thrown if there is a failure.)</returns>
        /// <exception cref="HttpRequestException">Throws an HttpRequestException on failure status code. The message field contains the error message from the server, if one exists.</exception>
        // (This is very badly designed, this will never return false because an exception will be thrown if there is a failure.)
        public static bool UploadCustomModel(byte[] customModel, string remoteYOLOAddress)
        {
            var client = new RemoteYOLOClient(remoteYOLOAddress);
            
            var customModelResponse = client.UploadCustomModel(customModel);

            return customModelResponse.success;
        }
        
        /// <summary>
        /// Converts the provided texture to JPG format on a background thread.
        /// </summary>
        /// <param name="texture">The texture to convert.</param>
        /// <param name="quality">The quality of the output JPG.</param>
        /// <returns>The raw bytes of the texture in JPG format.</returns>
        public static async Task<byte[]> Texture2DToJPGAsync(Texture2D texture, int quality = 75)
        {
            var rawTextureData = texture.GetRawTextureData();
            var graphicsFormat = texture.graphicsFormat;
            var width = texture.width;
            var height = texture.height;
            
            return await Task.Run(() => ImageConversion.EncodeArrayToJPG(rawTextureData, graphicsFormat, (uint)width, (uint)height, quality: quality));
        }

        /// <summary>
        /// Converts the provided texture to PNG format on a background thread.
        /// </summary>
        /// <param name="texture">The texture to convert.</param>
        /// <returns>The raw bytes of the texture in PNG format.</returns>
        public static async Task<byte[]> Texture2DToPNGAsync(Texture2D texture)
        {
            var rawTextureData = texture.GetRawTextureData();
            var graphicsFormat = texture.graphicsFormat;
            var width = texture.width;
            var height = texture.height;
            
            return await Task.Run(() => ImageConversion.EncodeArrayToPNG(rawTextureData, graphicsFormat, (uint)width, (uint)height));
        }

        /// <summary>
        /// Converts the provided texture to JPG format.
        /// </summary>
        /// <param name="texture">The texture to convert.</param>
        /// <param name="quality">The quality of the output JPG.</param>
        /// <returns>The raw bytes of the texture in JPG format.</returns>
        public static byte[] Texture2DToJPG(Texture2D texture, int quality = 75)
        {
            return texture.EncodeToJPG(quality: quality);
        }

        /// <summary>
        /// Converts the provided texture to PNG format.
        /// </summary>
        /// <param name="texture">The texture to convert.</param>
        /// <returns>The raw bytes of the texture in PNG format.</returns>
        public static byte[] Texture2DToPNG(Texture2D texture)
        {
            return texture.EncodeToPNG();
        }
    }

    public class YOLOAnalysisParameters
    {
        public readonly int Size;
        public readonly Unity.InferenceEngine.BackendType BackendType;
        public readonly Dictionary<int, string> Classes;
        public readonly float ConfidenceThreshold;

        public YOLOAnalysisParameters(Unity.InferenceEngine.BackendType backendType, int size, Dictionary<int, string> classes, float confidenceThreshold)
        {
            BackendType = backendType;
            Size = size;
            Classes = classes;
            ConfidenceThreshold = confidenceThreshold;
        }
    }
    
    public enum RemoteYOLOImageFormat
    {
        JPG,
        PNG
    }
}
