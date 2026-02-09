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
using TMPro;
using TMDbTools;
using Oculus.Interaction;
using UnityEngine.UI;
using System.Threading.Tasks;
using CustomScripts;

namespace YOLOTools.YOLO.Display
{
    public class ObjectDisplayManager : MonoBehaviour
    {
        #region Model Management

        private Dictionary<int, Dictionary<int, GameObject>> _activeModels;
        private GameObject _currentFocusedObject;
        public bool isFocusMode = false;

        private int _modelCount;
        [Tooltip("The maximum number of models which can spawn at once.")]
        [PositiveValueOnly][SerializeField] private int _maxModelCount = 10;
        [Tooltip("The minimum distance from an existing model at which a model of the same class can spawn.")]
        [PositiveValueOnly][SerializeField] private float _distanceThreshold = 1f;

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

        [SerializeField] private int _rottenTomatoesThreshold = 0;
        [SerializeField] private int _imdbThreshold = 0;
        [SerializeField] private int _metacriticThreshold = 0;
        private string _blockingMode = "blur";
        private int _lastRTThreshold = 0;
        private int _lastIMDbThreshold = 0;
        private int _lastMCThreshold = 0;
        private string _lastBlockingMode = "blur";

        #endregion

        #region External Data Management

        [Tooltip("The VideoFeedManager used to capture input frames.")]
        [MustBeAssigned][SerializeField] private VideoFeedManager _videoFeedManager;

        [SerializeField] public Canvas _settingsCanvas;
        private Toggle _rottenTomatesEnabledToggle;
        private Toggle _imdbEnabledToggle;
        private Toggle _metacriticEnabledToggle;

        private bool _rottenTomatoesEnabled;
        private bool _imdbEnabled;
        private bool _metacriticEnabled;

        private List<string> _selectedCountries;
        private bool _countryExcludeShow; // 0 for exclude, 1 for show only

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

            _rottenTomatesEnabledToggle = _settingsCanvas.GetComponentsInChildren<Toggle>().FirstOrDefault(t => t.name == "RottenTomatoesSliderEnabled");
            _imdbEnabledToggle = _settingsCanvas.GetComponentsInChildren<Toggle>().FirstOrDefault(t => t.name == "IMDbSliderEnabled");
            _metacriticEnabledToggle = _settingsCanvas.GetComponentsInChildren<Toggle>().FirstOrDefault(t => t.name == "MetacriticSliderEnabled");

            _selectedCountries = new List<string>();
        }

        public void DisplayModels(List<DetectedObject> objects, Camera referenceCamera)
        {
            Profiler.BeginSample("ObjectDisplayManager.DisplayModels");

            _camera = referenceCamera;

            UpdateSettingsFromUI();
            bool settingsChanged = _lastRTThreshold != _rottenTomatoesThreshold || _lastBlockingMode != _blockingMode || _lastIMDbThreshold != _imdbThreshold || _lastMCThreshold != _metacriticThreshold;
            Debug.Log($"ObjectDisplayManager::DisplayModels - _lastBlockingMode = {_lastBlockingMode}, _blockingMode = {_blockingMode}, settingsChanged = {settingsChanged}");
            _lastRTThreshold = _rottenTomatoesThreshold;
            _lastIMDbThreshold = _imdbThreshold;
            _lastMCThreshold = _metacriticThreshold;
            _lastBlockingMode = _blockingMode;


            Dictionary<int, int> objectCounts = new();
            HashSet<int> activeThisFrame = new HashSet<int>();

            foreach (var obj in objects)
            {
                if (obj.CocoName == null) continue;

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

                activeThisFrame.Add(obj.TrackID);

                (Vector3 spawnPosition, Quaternion spawnRotation, float hitConfidence) = GetObjectWorldCoordinates(obj);

                if (!objectCounts.TryAdd(obj.CocoClass, 1))
                {
                    objectCounts[obj.CocoClass]++;
                }

                if (modelList.TryGetValue(obj.TrackID, out var existingModel))
                {
                    // Already got object for this movie, move it
                    UpdateModel(obj, obj.TrackID, spawnPosition, spawnRotation, existingModel, _environmentRaycastManager != null && _environmentRaycastManager.isActiveAndEnabled && hitConfidence >= 0.5f, settingsChanged);
                }
                else
                {
                    // Dont have object for this move, make a new one 
                    if (ModelCount >= MaxModelCount) continue;

                    if (IsDuplicate(spawnPosition, modelList.Values.ToDictionary(go => go.GetInstanceID(), go => go))) continue;

                    var model = Instantiate(_cocoModels[obj.CocoName]);
                    modelList.Add(obj.TrackID, model);
                    UpdateModel(obj, obj.TrackID, spawnPosition, spawnRotation, model, _environmentRaycastManager != null && _environmentRaycastManager.isActiveAndEnabled && hitConfidence >= 0.5f, false);
                    ModelCount++;

                    // Ray Interaction
                    var interactable = model.GetComponentInChildren<RayInteractable>();

                    if (interactable != null)
                    {
                        interactable.WhenSelectingInteractorViewAdded += _ =>
                        {
                            var state = model.GetComponent<MovieDisplayState>();
                            var currentMovieID = state != null ? state.CurrentMovieID : null;
                            if (!string.IsNullOrEmpty(currentMovieID)) OnModelSelected(model, currentMovieID);
                        };
                    }
                    else
                    {
                        Debug.LogWarning("ObjectDisplayManager::DisplayModels - No RayInteractable found!");
                    }

                    // Set up UI
                    Canvas modelCanvas = model.GetComponentInChildren<Canvas>(true);

                    if (modelCanvas != null)
                    {
                        SetUiVisible(model, false);

                        Button[] buttons = modelCanvas.GetComponentsInChildren<Button>();
                        Button restoreButton = buttons.FirstOrDefault(b => b.name == "RestoreButton");

                        if (restoreButton != null)
                        {
                            Debug.Log("ObjectDisplayManager::DisplayModels - RestoreButton found!");
                            restoreButton.onClick.AddListener(ExitFocusMode);
                        }
                        else
                        {
                            Debug.Log("ObjectDisplayManager::DisplayModels - No RestoreButton found!");
                        }
                    }
                }
            }

            foreach (var entry in _activeModels)
            {
                var entryDict = entry.Value;
                var toRemove = entryDict.Where(kvp => !activeThisFrame.Contains(kvp.Key)).Select(kvp => kvp.Key).ToList();

                foreach (var idToRemove in toRemove)
                {
                    Destroy(entryDict[idToRemove]);
                    entryDict.Remove(idToRemove);
                    ModelCount--;
                }
            }

            Profiler.EndSample();
        }

        private void OnModelSelected(GameObject selectedModel, string movieID)
        {
            Debug.Log($"ObjectDisplayManager::DisplayModels - TRIGGER PRESSED ON MOVIE!");

            isFocusMode = true;
            _currentFocusedObject = selectedModel;


            foreach (var classGroup in _activeModels.Values)
            {
                foreach (var model in classGroup.Values)
                {
                    if (model != null)
                    {
                        model.SetActive(false);
                    }
                }
            }

            if (selectedModel != null)
            {
                selectedModel.SetActive(true);
                var canvas = selectedModel.GetComponentInChildren<Canvas>(true);
                if (canvas)
                {
                    var TMProMeshes = canvas.GetComponentsInChildren<TMP_Text>();
                    TMP_Text titleText = TMProMeshes.FirstOrDefault(t => t.name == "UITitle");
                    TMP_Text detailsText = TMProMeshes.FirstOrDefault(t => t.name == "UIDetails");
                    UpdateMovieDetails(titleText, detailsText, movieID);
                    SetUiVisible(selectedModel, true);
                    var uiRoot = GetUiRoot(canvas);
                    uiRoot.GetComponent<SpawnFacingUser>().FaceUser();
                }
            }
        }

        public void ExitFocusMode()
        {
            Debug.Log("ObjectDisplayManager::ExitFocusMode - Exiting focus mode");
            if (_currentFocusedObject != null)
            {
                SetUiVisible(_currentFocusedObject, false);

                _currentFocusedObject = null;

                foreach (var classGroup in _activeModels.Values)
                {
                    foreach (var model in classGroup.Values)
                    {
                        if (model != null) model.SetActive(true);
                    }
                }

                isFocusMode = false;
            }
        }

        private void UpdateSettingsFromUI()
        {
            _rottenTomatoesEnabled = _rottenTomatesEnabledToggle.isOn;
            _imdbEnabled = _imdbEnabledToggle.isOn;
            _metacriticEnabled = _metacriticEnabledToggle.isOn;

            if (_rottenTomatoesEnabled)
            {
                var slider = _settingsCanvas.GetComponentsInChildren<Slider>().FirstOrDefault(n => n.name == "RottenTomatoesThresholdSlider");
                if (slider)
                {
                    _rottenTomatoesThreshold = (int)slider.value;
                }
            }
            else _rottenTomatoesThreshold = 0;
            
            if (_imdbEnabled)
            {
                var slider = _settingsCanvas.GetComponentsInChildren<Slider>().FirstOrDefault(n => n.name == "IMDbThresholdSlider");
                if (slider)
                {
                    _imdbThreshold = (int)slider.value;
                }
            }
            else _imdbThreshold = 0;

            if (_metacriticEnabled)
            {
                var slider = _settingsCanvas.GetComponentsInChildren<Slider>().FirstOrDefault(n => n.name == "MetacriticThresholdSlider");
                if (slider)
                {
                    _metacriticThreshold = (int)slider.value;
                }
            }
            else _metacriticThreshold = 0;

            List<string> blockingModes = new List<string>() { "blur", "desaturated", "black" };
            var blockingModeDropdown = _settingsCanvas.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(d => d.name == "BlockingModeDropdown");
            if (blockingModeDropdown)
            {
                var blockingModeIndex = blockingModeDropdown.value;
                _blockingMode = blockingModes[blockingModeIndex];
            }

            var countrySelectDropdown = _settingsCanvas.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(d => d.name == "CountryDropdown");
            if (countrySelectDropdown)
            {
                var countryDropdownManager = countrySelectDropdown.GetComponentInChildren<CountryDropdownManager>();
                _selectedCountries = countryDropdownManager.GetSelectedCountries();
            }

            var countryExcludeShowToggle = _settingsCanvas.GetComponentsInChildren<Toggle>().FirstOrDefault(t => t.name == "ToggleExcludeShowSwitch");
            if (countryExcludeShowToggle)
            {
                _countryExcludeShow = countryExcludeShowToggle.isOn;
            }
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
            scaleFactor *= 1f - ScaleDampener;
            if (float.IsInfinity(scaleFactor)) scaleFactor = 1f;
            Debug.Log($"Scale Factor for {obj.CocoName}: {scaleFactor}");
            Vector3 scaleVector = new(scaleFactor, scaleFactor, scaleFactor);
            model.transform.localScale = Vector3.Scale(model.transform.localScale, scaleVector);
        }

        private void UpdatePosterVisuals(GameObject model, DetectedObject obj, int rottenTomatoesScore, int imdbScore, int metacriticScore, ProductionCountry[] productionCountries)
        {
            string crop = obj?.Crop;

            var posterObject = model.transform.Find("posterObject");
            var posterBorder = model.transform.Find("posterBorder");

            bool belowRatingThreshold = (rottenTomatoesScore < _rottenTomatoesThreshold && _rottenTomatoesEnabled) || (imdbScore < _imdbThreshold && _imdbEnabled) || (metacriticScore < _metacriticThreshold && _metacriticEnabled);

            bool hiddenCountry = false;
            foreach (ProductionCountry productionCountry in productionCountries)
            {
                if (_countryExcludeShow)
                {
                    // _countryExcludeShow is 1, so we show only the selected countries
                    hiddenCountry = true;
                    if (_selectedCountries.Contains(productionCountry.iso_3166_1))
                    {
                        hiddenCountry = false;
                        break;
                    }
                } else
                {
                    // _countryExcludeShow is 0, so we hide the selected countries
                    if (_selectedCountries.Contains(productionCountry.iso_3166_1))
                    {
                        hiddenCountry = true;
                        break;
                    }
                }
            }

            if (belowRatingThreshold || hiddenCountry)
            {
                Texture2D texture = null;

                if (!string.IsNullOrEmpty(crop))
                {    
                    if (obj.CurrentTexture != null)
                    {
                        texture = obj.CurrentTexture;
                    }
                    else
                    {
                        byte[] imageBytes = Convert.FromBase64String(crop);
                        texture = new Texture2D(32, 64);
                        if (!texture.LoadImage(imageBytes))
                        {
                            Destroy(texture);
                            return;
                        }

                        if (obj != null)
                        {
                            if (obj.CurrentTexture != null)
                            {
                                Destroy(obj.CurrentTexture);
                            }
                            obj.CurrentTexture = texture;
                        }
                    }
                }

                if (posterObject != null)
                {
                    posterObject.gameObject.SetActive(true);
                    var posterObjectMaterial = posterObject.gameObject.GetComponent<Renderer>().material;

                    if (_blockingMode == "blur" || _blockingMode == "desaturated")
                    {
                        posterObjectMaterial.SetColor("_BaseColor", Color.white);
                        posterObjectMaterial.SetTexture("_BaseMap", texture);
                        posterObjectMaterial.SetTexture("_EmissionMap", texture);
                    }
                    else
                    {
                        posterObjectMaterial.SetColor("_BaseColor", Color.black);
                        posterObjectMaterial.SetTexture("_BaseMap", null);
                        posterObjectMaterial.SetTexture("_EmissionMap", null);
                    }
                }

                if (posterBorder != null)
                {
                    posterBorder.gameObject.SetActive(true);
                    var posterBorderMaterial = posterBorder.gameObject.GetComponent<Renderer>().material;

                    if (_blockingMode == "blur" || _blockingMode == "desaturated")
                    {
                        posterBorderMaterial.SetColor("_BaseColor", Color.white);
                        posterBorderMaterial.SetTexture("_BaseMap", texture);
                        posterBorderMaterial.SetTexture("_EmissionMap", texture);
                    }
                    else
                    {
                        posterBorderMaterial.SetColor("_BaseColor", Color.black);
                        posterBorderMaterial.SetTexture("_BaseMap", null);
                        posterBorderMaterial.SetTexture("_EmissionMap", null);
                    }
                }
            }
            else
            {
                if (posterObject != null)
                {
                    posterObject.gameObject.SetActive(false);
                }
                
                if (posterBorder != null) 
                {
                    var posterBorderMaterial = posterBorder.gameObject.GetComponent<Renderer>().material;

                    posterBorderMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.75f));
                    posterBorderMaterial.SetTexture("_BaseMap", null);
                    posterBorderMaterial.SetTexture("_EmissionMap", null);
                }
            }
        }

        private void UpdateModel(DetectedObject obj, int id, Vector3 newPosition, Quaternion newRotation, GameObject model, bool useRaycastNormal, bool forceUpdate = false)
        {
            model.transform.SetPositionAndRotation(newPosition, newRotation);

            if (!useRaycastNormal) model.transform.LookAt(_camera.transform);

            model.name = $"{obj.CocoName} {id}";

            TextMeshPro label = model.GetComponentInChildren<TextMeshPro>();
            if (label != null)
            {
                var state = model.GetComponent<MovieDisplayState>() ?? model.AddComponent<MovieDisplayState>();
                Debug.Log($"ObjectDisplayManager::UpdateModel - forceUpdate = {forceUpdate}");
                if (state.CurrentMovieID != obj.MovieID)
                {
                    var posterObject = model.transform.Find("posterObject");
                    if (posterObject != null)
                    {
                        posterObject.gameObject.SetActive(false);
                    }
                    state.CurrentMovieID = obj.MovieID;
                    state.RequestVersion++;
                    state.CachedRottenTomatoesScore = -1;
                    state.CachedIMDbScore = -1;
                    state.CachedMetacriticScore = -1;

                    Debug.Log($"ObjectDisplayManager::UpdateModel - Fetching movie name...");

                    label.text = "Loading...";

                    string movieId = obj.MovieID;
                    string crop = obj.Crop;
                    int trackId = obj.TrackID;
                    float conf = obj.MovieConfidence;
                    _ = UpdateMovieLabel(label, movieId, model, obj, crop, state.RequestVersion, state, trackId, conf);
                }
                else 
                {
                    UpdatePosterVisuals(model, obj, state.CachedRottenTomatoesScore, state.CachedIMDbScore, state.CachedMetacriticScore, state.CachedProductionCountries);
                }
            }
            else
            {
                Debug.LogError("ObjectDisplayManager::UpdateModel - Failed to get TMPro object!");
            }

            RescaleObject(obj, model);

            if (!isFocusMode || model == _currentFocusedObject)
            {
                model.SetActive(true);
            }
        }

        private async Task UpdateMovieLabel(TextMeshPro label, string movieID, GameObject model, DetectedObject obj, string crop, int requestVersion, MovieDisplayState state, int id = -2, float confidence = 0f)
        {
            var tmdbMovieInfo = await TMDb.GetMovieInfo(movieID);
            var omdbMovieInfo = await OMDb.GetOMDbInfo(tmdbMovieInfo.imdb_id);
            var movieName = tmdbMovieInfo.original_title;

            if (requestVersion != state.RequestVersion) return;

            if (label != null && movieName != null)
            {
                int rottenTomatoesInt = 100;
                int imdbInt = 100;
                int metacriticInt = 100;

                var rt = omdbMovieInfo?.Ratings?.FirstOrDefault(r => r.Source == "Rotten Tomatoes")?.Value;
                if (!string.IsNullOrEmpty(rt))
                {
                    int.TryParse(rt.Replace("%", ""), out rottenTomatoesInt);
                }

                var imdb = omdbMovieInfo?.Ratings?.FirstOrDefault(r => r.Source == "Internet Movie Database")?.Value;
                if (!string.IsNullOrEmpty(imdb))
                {
                    float imdbFloat;
                    float.TryParse(imdb.Substring(0, 3), out imdbFloat);
                    imdbInt = (int)(imdbFloat * 10);
                }

                var metacritic = omdbMovieInfo?.Ratings?.FirstOrDefault(r => r.Source == "Metacritic")?.Value;
                if (!string.IsNullOrEmpty(metacritic))
                {
                    int.TryParse(metacritic.Replace("/100", ""), out metacriticInt);
                }

                var productionCountries = tmdbMovieInfo?.production_countries;

                state.CachedRottenTomatoesScore = rottenTomatoesInt;
                state.CachedIMDbScore = imdbInt;
                state.CachedMetacriticScore = metacriticInt;
                state.CachedProductionCountries = productionCountries;

                UpdatePosterVisuals(model, obj, rottenTomatoesInt, imdbInt, metacriticInt, productionCountries);
                label.text = $"TrackID: {id.ToString()}\n{movieName} - {confidence.ToString("0.00")}";
            }
            else if (movieName == null)
            {
                Debug.Log($"ObjectDisplayManager::UpdateMovieLabel - Couldn't find ID {movieID}");
                label.text = "Not Found";
            }
        }

        private async void UpdateMovieDetails(TMP_Text title, TMP_Text details, string movieID)
        {
            var tmdbMovieInfo = await TMDb.GetMovieInfo(movieID);
            var omdbMovieInfo = await OMDb.GetOMDbInfo(tmdbMovieInfo.imdb_id);
            if (title != null && tmdbMovieInfo != null && tmdbMovieInfo != null)
            {
                title.text = tmdbMovieInfo.original_title;

                var genres = string.Join(", ", tmdbMovieInfo.genres.Select(x => x.name));
                var productionCompanyNames = string.Join("\n • ", tmdbMovieInfo.production_companies.Select(x => $"{x.name} ({x.origin_country})"));
                var productionCountries = string.Join(", ", tmdbMovieInfo.production_countries.Select(x => x.name));
                string rottenTomatoes;
                try
                {
                    rottenTomatoes = omdbMovieInfo.Ratings[1].Value;
                }
                catch
                {
                    rottenTomatoes = "N/A";
                }

                details.text = $"Rating: {tmdbMovieInfo.vote_average}\nRotten Tomatoes: {rottenTomatoes}\tIMDb: {omdbMovieInfo.imdbRating}\tMetacritic: {omdbMovieInfo.Metascore}\nGenres: {genres}\nProduction Companies:\n • {productionCompanyNames}\nProduction Countries: {productionCountries}\nBox Office: {omdbMovieInfo.BoxOffice}";
            }
            else if (tmdbMovieInfo == null)
            {
                Debug.Log($"ObjectDisplayManager::UpdateMovieDetails - Couldn't find ID {movieID}");
                title.text = "Not Found";
            }
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

        private Transform GetUiRoot(Canvas canvas)
        {
            return canvas.transform.parent;
        }

        private void SetUiVisible(GameObject model, bool visible)
        {
            var canvas = model.GetComponentInChildren<Canvas>(true);
            if (!canvas) return;

            var uiRoot = GetUiRoot(canvas);
            uiRoot.gameObject.SetActive(visible);
        }

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
                            _camera.ScreenPointToRay(screenPoint), out var hit))
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
                    rayPoints[i, j] = new Vector2(currentX, currentY);
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
                var ray = _camera.ScreenPointToRay(screenPoint);
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

            float normalizedX = (coordinates.x / feedDimensions.Width) - 0.5f;
            float normalizedY = (coordinates.y / feedDimensions.Height) - 0.5f;

            var XSlider = _settingsCanvas.GetComponentsInChildren<Slider>().FirstOrDefault(s => s.name == "XScaleSlider");
            var YSlider = _settingsCanvas.GetComponentsInChildren<Slider>().FirstOrDefault(s => s.name == "YScaleSlider");
            normalizedX *= XSlider.value / 100f;
            normalizedY *= YSlider.value / 100f;

            float screenCenterX = _camera.scaledPixelWidth / 2f;
            float screenCenterY = _camera.scaledPixelHeight / 2f;

            var newX = screenCenterX + (normalizedX * feedDimensions.Width) - 55f;
            var newY = screenCenterY - (normalizedY * feedDimensions.Height) - 190f;

            return new Vector2(newX, newY);
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