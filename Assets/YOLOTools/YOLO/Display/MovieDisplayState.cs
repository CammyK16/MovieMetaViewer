using UnityEngine;
using TMDbTools;

namespace YOLOTools.YOLO.Display
{
    public class MovieDisplayState : MonoBehaviour
    {
        public string CurrentMovieID;
        public int RequestVersion;
        public int CachedRottenTomatoesScore;
        public int CachedIMDbScore;
        public int CachedMetacriticScore;
        public ProductionCountry[] CachedProductionCountries;
        public Genre[] CachedGenres;
    }
}