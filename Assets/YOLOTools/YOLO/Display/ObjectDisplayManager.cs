using System;
using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using MyBox;
using UnityEngine;
using UnityEngine.Profiling;
using YOLOTools.Utilities;
using YOLOTools.YOLO.ObjectDetection;

namespace YOLOTools.YOLO.Display
{
    public class ObjectDisplayManager : MonoBehaviour
    {
        #region Model Management

        private Dictionary<int, Dictionary<int, GameObject>> _activeModels;

        private int _modelCount;
        [Tooltip("The maximum number of models which can spawn at once.")]
        [PositiveValueOnly] [SerializeField] private int _maxModelCount = 10;
        [Tooltip("The minimum distance from an existing model at which a model of the same class can spawn.")]
        [PositiveValueOnly] [SerializeField] private float _distanceThreshold = 1f;

        [Tooltip("The names of the COCO classes to detect and their associated models.")]
        [SerializeField, SerializedDictionary("Coco Class", "3D Model")]
        private SerializedDictionary<string, GameObject> _cocoModels;

        [Tooltip("Use existing models when a new object is detected.")]
        [SerializeField] private bool _movingObjects;
        public bool MovingObjects { get => _movingObjects; set => _movingObjects = value; }

        public int ModelCount { get { return _modelCount; } private set { _modelCount = value; } }
        public int MaxModelCount { get { return _maxModelCount; } private set { _maxModelCount = value; } }
        public float DistanceThreshold { get { return _distanceThreshold; } private set { _distanceThreshold = value; } }

        [Tooltip("The scaling method to use:\nMIN: Use the minimum of the x and y scale change.\nMAX: Use the maximum of the x and y scale change.\nAVERAGE: Use the average of both the x and y scale change.\nWIDTH: Use the x scale change.\nHEIGHT: Use the y scale change.")]
        [SerializeField] private ScaleType _scaleType = ScaleType.AVERAGE;
        
        private const float ScaleDampener = 0f;
        
        
        #endregion

        #region External Data Management

        [Tooltip("The VideoFeedManager used to capture input frames.")]
        [MustBeAssigned] [SerializeField] private VideoFeedManager _videoFeedManager;

        private Camera _camera;

        #endregion

        #region Depth

        private MRUK _mruk;
        private MRUK SceneManager { get => _mruk; set => _mruk = value; }
        private MRUKRoom _currentRoom = null;

        private EnvironmentRaycastManager _environmentRaycastManager;

        private bool _sceneLoaded = false;

        #endregion

        private void Start()
        {
            _activeModels = new Dictionary<int, Dictionary<int, GameObject>>();
            SceneManager = FindAnyObjectByType<MRUK>();
            SceneManager.SceneLoadedEvent.AddListener(OnSceneLoad);
            SceneManager.RoomUpdatedEvent.AddListener(OnSceneUpdated);
            if (!TryGetComponent(out _environmentRaycastManager))
            {
                _environmentRaycastManager = gameObject.AddComponent<EnvironmentRaycastManager>();
            }
            Unity.XR.Oculus.Utils.SetupEnvironmentDepth(new Unity.XR.Oculus.Utils.EnvironmentDepthCreateParams());
        }

        public void DisplayModels(List<DetectedObject> objects, Camera referenceCamera)
        {
            Profiler.BeginSample("ObjectDisplayManager.DisplayModels");

            _camera = referenceCamera;

            Dictionary<int, int> objectCounts = new();

            foreach (var obj in objects)
            {
                if (obj.CocoName == null) continue;
                
                if (objectCounts.GetValueOrDefault(obj.CocoClass) == 3) continue;

                if (!_cocoModels.ContainsKey(obj.CocoName) || _cocoModels[obj.CocoName] == null)
                {
                    Debug.Log("Error: No model provided for the detected class.");

                    continue;
                }

                Dictionary<int, GameObject> modelList;
                if (_activeModels.ContainsKey(obj.CocoClass)) modelList = _activeModels[obj.CocoClass];
                else
                {
                    modelList = new Dictionary<int, GameObject>();
                    _activeModels.Add(obj.CocoClass, modelList);
                }

                (Vector3 spawnPosition, Quaternion spawnRotation, float hitConfidence) = GetObjectWorldCoordinates(obj);
                
                if (IsDuplicate(spawnPosition, modelList)) continue;

                if (!objectCounts.TryAdd(obj.CocoClass, 1))
                {
                    objectCounts[obj.CocoClass]++;
                }
                
                if ((!MovingObjects || objectCounts[obj.CocoClass] > modelList.Count) && ModelCount != MaxModelCount)
                {
                    var model = Instantiate(_cocoModels[obj.CocoName]);
                    modelList.Add(modelList.Count, model);
                    UpdateModel(obj, objectCounts[obj.CocoClass], spawnPosition, spawnRotation, model, _environmentRaycastManager != null && _environmentRaycastManager.isActiveAndEnabled && hitConfidence >= 0.5f);
                    ModelCount++;
                }
                else if (objectCounts[obj.CocoClass] <= modelList.Count)
                {
                    if (MovingObjects)
                    {
                        var model = modelList[objectCounts[obj.CocoClass] - 1];
                        UpdateModel(obj, objectCounts[obj.CocoClass], spawnPosition, spawnRotation, model, _environmentRaycastManager != null && _environmentRaycastManager.isActiveAndEnabled && hitConfidence >= 0.5f);
                    }
                }
            }

            Profiler.EndSample();
        }

        #region Model Methods

        public void ClearModels()
        {
            foreach (var obj in _activeModels)
            {
                foreach (var model in obj.Value)
                {
                    Destroy(model.Value);
                }
                obj.Value.Clear();
            }
            _activeModels.Clear();
            _modelCount = 0;
        }
        
        
        private void RescaleObject(DetectedObject obj, GameObject model)
        {

            Vector3 p3 = obj.BoundingBox.max;
            Vector3 p1 = obj.BoundingBox.min;

            Vector3 sP3 = ImageToScreenCoordinates(p3);
            Vector3 sP1 = ImageToScreenCoordinates(p1);

            float newHeight = Math.Abs(sP3.y - sP1.y);
            float newWidth = Math.Abs(sP3.x - sP1.x);

            (Vector2 minPoint, Vector2 maxPoint) = GetModel2DBounds(GetModel3DBounds(model));

            float currentWidth = Math.Abs(maxPoint.x - minPoint.x);
            float currentHeight = Math.Abs(maxPoint.y - minPoint.y);
            float scaleFactor = _scaleType switch
            {
                ScaleType.WIDTH => newWidth / currentWidth,
                ScaleType.HEIGHT => newHeight / currentHeight,
                ScaleType.AVERAGE => ((newWidth / currentWidth) + (newHeight / currentHeight)) / 2f,
                ScaleType.MIN => Math.Min(newWidth / currentWidth, newHeight / currentHeight),
                ScaleType.MAX => Math.Max(newWidth / currentWidth, newHeight / currentHeight),
                _ => 1f
            };
            scaleFactor *= 1f-ScaleDampener;
            if (float.IsInfinity(scaleFactor)) scaleFactor = 1f;
            Debug.Log($"Scale Factor for {obj.CocoName}: {scaleFactor}");
            Vector3 scaleVector = new(scaleFactor, scaleFactor, scaleFactor);
            model.transform.localScale = Vector3.Scale(model.transform.localScale, scaleVector);
        }

        private void UpdateModel(DetectedObject obj, int id, Vector3 newPosition, Quaternion newRotation, GameObject model, bool useRaycastNormal)
        {
            model.transform.SetPositionAndRotation(newPosition, newRotation);

            if (!useRaycastNormal) model.transform.LookAt(_camera.transform);

            model.name = $"{obj.CocoName} {id}";
            RescaleObject(obj, model);
            model.SetActive(true);
        }

        private bool IsDuplicate(Vector3 spawnPosition, Dictionary<int, GameObject> modelList)
        {
            foreach (var (id, model) in modelList)
            {
                var distance = Vector3.Distance(spawnPosition, model.transform.position);
                var boundingBoxR = Vector3.Distance(model.GetComponentInChildren<MeshRenderer>().bounds.max, model.GetComponentInChildren<MeshRenderer>().bounds.center);
                if (distance < DistanceThreshold * boundingBoxR)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Helper Methods

        private (Vector3, Quaternion, float) GetObjectWorldCoordinates(DetectedObject obj)
        {
            Vector3 position;
            Quaternion rotation;
            float hitConfidence = 1;
            
            if (_environmentRaycastManager && _environmentRaycastManager.isActiveAndEnabled && EnvironmentRaycastManager.IsSupported)
            {
                var screenPoint = ImageToScreenCoordinates(obj.BoundingBox.center);
                // If you use Camera.MonoOrStereoscopicEye.Left then objects display off centre, even though the view is from the left eye, and the whole point of that flag is to account for that. Oh, also it's offset in the Y by about 200 pixels for some reason when you use Mono.
                if (_environmentRaycastManager.Raycast(
                            _camera.ScreenPointToRay(screenPoint, Camera.MonoOrStereoscopicEye.Mono), out var hit)) 
                {
                    position = hit.point;
                    rotation = Quaternion.LookRotation(hit.normal);
                    hitConfidence = hit.normalConfidence;
                }
                else
                {
                    (position, rotation) = ImageToWorldCoordinates(obj.BoundingBox.center);
                }
            }
            else (position, rotation) = ImageToWorldCoordinates(obj.BoundingBox.center);

            return (position, rotation, hitConfidence);
        }

        private (Vector3, Quaternion, float) AverageRaycastHits(EnvironmentRaycastHit[] hits)
        {
            Vector3 pointSum = Vector3.zero;
            Vector3 normalSum = Vector3.zero;
            float confidenceSum = 0;
            int normalCount = 0;

            foreach (EnvironmentRaycastHit hit in hits)
            {
                pointSum += hit.point;
                if (hit.normalConfidence > 0.5f)
                {
                    normalSum += hit.normal;
                    confidenceSum += hit.normalConfidence;
                    normalCount++;
                }
            }

            Vector3 averagePosition = pointSum / hits.Length;
            Quaternion averageRotation = Quaternion.LookRotation(normalSum / hits.Length);
            float averageHitConfidence = confidenceSum / normalCount;

            return (averagePosition, averageRotation, averageHitConfidence);
        }

        private (Vector2, Vector2) GetModel2DBounds(Vector3[] bounds3D)
        {
            Vector2[] screenPoints = bounds3D.Select(boundPoint => (Vector2)_camera.WorldToScreenPoint(boundPoint)).ToArray();

            float maxX = screenPoints[0].x;
            float minX = screenPoints[0].x;
            float maxY = screenPoints[0].y;
            float minY = screenPoints[0].y;

            foreach (Vector3 screenPoint in screenPoints)
            {
                if (screenPoint.x > maxX) maxX = screenPoint.x;
                if (screenPoint.x < minX) minX = screenPoint.x;
                if (screenPoint.y > maxY) maxY = screenPoint.y;
                if (screenPoint.y < minY) minY = screenPoint.y;
            }

            return (new Vector2(minX, minY), new Vector2(maxX, maxY));
        }

        private Vector3[] GetModel3DBounds(GameObject model)
        {
            Vector3[] boundPoints = new Vector3[8];

            boundPoints[0] = model.GetComponentInChildren<MeshRenderer>().bounds.min;
            boundPoints[1] = model.GetComponentInChildren<MeshRenderer>().bounds.max;
            boundPoints[2] = new Vector3(boundPoints[0].x, boundPoints[0].y, boundPoints[1].z);
            boundPoints[3] = new Vector3(boundPoints[0].x, boundPoints[1].y, boundPoints[0].z);
            boundPoints[4] = new Vector3(boundPoints[1].x, boundPoints[0].y, boundPoints[0].z);
            boundPoints[5] = new Vector3(boundPoints[0].x, boundPoints[1].y, boundPoints[1].z);
            boundPoints[6] = new Vector3(boundPoints[1].x, boundPoints[0].y, boundPoints[1].z);
            boundPoints[7] = new Vector3(boundPoints[1].x, boundPoints[1].y, boundPoints[0].z);

            return boundPoints;
        }

        private EnvironmentRaycastHit[] FireRaycastSpread(DetectedObject obj, int spreadWidth, int spreadHeight)
        {
            if (spreadWidth <= 0 || spreadHeight <= 0) throw new Exception("Spread width and spread height must both be greater than 0");

            if (spreadWidth % 2 == 0) spreadWidth += 1;
            if (spreadHeight % 2 == 0) spreadHeight += 1;

            Vector2[,] rayPoints = new Vector2[spreadHeight, spreadWidth];
            rayPoints[spreadHeight / 2, spreadWidth / 2] = ImageToScreenCoordinates(obj.BoundingBox.center);

            float yDist = 0.01f * _videoFeedManager.GetFeedDimensions().Height;
            float xDist = 0.01f * _videoFeedManager.GetFeedDimensions().Width;

            float currentY = rayPoints[spreadHeight / 2, spreadWidth / 2].y - yDist;
            float currentX = rayPoints[spreadHeight / 2, spreadWidth / 2].x - xDist;

            for (int i = 0; i < spreadHeight; i++)
            {
                for (int j = 0; j < spreadWidth; j++)
                {
                    if (i == spreadHeight / 2 && j == spreadWidth / 2) continue;
                    rayPoints[i, j] = ImageToScreenCoordinates(new Vector2(currentX, currentY));
                    currentX += xDist;
                }

                currentY += yDist;
                currentX = rayPoints[spreadHeight / 2, spreadWidth / 2].x - xDist;
            }

            Ray[] rays = rayPoints.Cast<Vector2>().Select(point => _camera.ScreenPointToRay(point)).ToArray();

            EnvironmentRaycastHit[] hits = rays.Select(ray =>
            {
                _environmentRaycastManager.Raycast(ray, out EnvironmentRaycastHit hit);
                return hit;
            }).Where(hit => hit.status == EnvironmentRaycastHitStatus.Hit).ToArray();

            return hits;
        }

        private (Vector3, Quaternion) ImageToWorldCoordinates(Vector2 coordinates)
        {

            var screenPoint = ImageToScreenCoordinates(coordinates);
            
            const float spawnDepth = 1.5f;
            if (_sceneLoaded && _currentRoom)
            {
                var ray = _camera.ScreenPointToRay(screenPoint, Camera.MonoOrStereoscopicEye.Mono);
                if (_currentRoom.Raycast(ray, 500, out var hit, out var anchor))
                {
                    Debug.Log("Hit in image to world coordinates");
                    return (hit.point, Quaternion.LookRotation(hit.normal));
                }
            }

            return (_camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, spawnDepth)), Quaternion.identity);
        }

        private Vector2 ImageToScreenCoordinates(Vector2 coordinates)
        {
            FeedDimensions feedDimensions = _videoFeedManager.GetFeedDimensions();

            var xOffset = (_camera.scaledPixelWidth - feedDimensions.Width) / 2f;
            var yOffset = (_camera.scaledPixelHeight - feedDimensions.Height) / 2f;

            var newX = coordinates.x + xOffset;
            var newY = (feedDimensions.Height - coordinates.y) + yOffset;

            // 200 pixel offset when using the Camera.MonoOrStereoscopicEye.Mono flag.
            return new Vector2(newX, newY-200f);
            
        }

        private void OnSceneLoad()
        {
            _sceneLoaded = true;
            _currentRoom = SceneManager.GetCurrentRoom();
        }

        private void OnSceneUpdated(MRUKRoom room)
        {
            _currentRoom = room;
        }

        #endregion

        private enum ScaleType
        {
            WIDTH,
            HEIGHT,
            AVERAGE,
            MIN,
            MAX
        }
    }


}