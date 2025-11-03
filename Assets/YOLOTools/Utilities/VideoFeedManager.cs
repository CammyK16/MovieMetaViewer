using UnityEngine;
using UnityEngine.Events;

namespace YOLOTools.Utilities
{
    public abstract class VideoFeedManager : MonoBehaviour
    {
        public UnityEvent NewFrame { get; } = new UnityEvent();

        public abstract Texture2D GetTexture();

        public abstract FeedDimensions GetFeedDimensions();
    }

    public class FeedDimensions
    {
        private readonly int _width;
        private readonly int _height;

        public int Width { get => _width; }
        public int Height { get => _height; }

        public FeedDimensions(int width, int height)
        {
            _width = width;
            _height = height;
        }
    }

}