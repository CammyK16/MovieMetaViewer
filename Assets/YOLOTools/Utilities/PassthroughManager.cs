using Trev3d.Quest.ScreenCapture;
using UnityEngine;
using UnityEngine.Events;

namespace YOLOTools.Utilities
{
    public class PassthroughManager : VideoFeedManager
    {
        [SerializeField] private QuestScreenCaptureTextureManager _screenCaptureTextureManager;
        private Texture2D _currentTexture;

        public UnityEvent ScreenCaptureStart = new();

        public PassthroughManager(QuestScreenCaptureTextureManager screenCaptureTextureManager)
        {
            _screenCaptureTextureManager = screenCaptureTextureManager;
        }

        private void Start()
        {
            _screenCaptureTextureManager.OnNewFrame.AddListener(OnNewFrame);
            _screenCaptureTextureManager.OnScreenCaptureStarted.AddListener(OnScreenCaptureStart);
        }

        public override Texture2D GetTexture()
        {
            return _currentTexture;
        }

        private void OnNewFrame()
        {
            _currentTexture = _screenCaptureTextureManager.ScreenCaptureTexture;
            NewFrame.Invoke();
        }
        private void OnScreenCaptureStart() => ScreenCaptureStart.Invoke();

        public override FeedDimensions GetFeedDimensions()
        {
            return new FeedDimensions(1024, 1024);
        }
    }
}

