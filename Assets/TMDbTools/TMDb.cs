using UnityEngine;
using System;
using UnityEngine.Networking;
using System.Threading.Tasks;
using static TMDbTools.TMDb_API_KEY;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TMDbTools
{
    public class TMDb
    {
        public static ConcurrentDictionary<string, MovieInfo> knownIds = new ConcurrentDictionary<string, MovieInfo>();
        private static readonly object KnownIdsLoadLock = new object();
        private const string CacheFileName = "movie_id_to_imdb_data_map.json";
        private const string CacheResourcesPath = "TMDbTools/movie_id_to_imdb_data_map";

        private const string API_KEY = TMDB_API_KEY;

        static TMDb()
        {
            EnsureKnownIdsLoaded();
        }

        private static void EnsureKnownIdsLoaded()
        {
            if (!knownIds.IsEmpty)
            {
                // Debug.Log($"TMDbTools::EnsureKnownIdsLoaded - Cache already populated ({knownIds.Count} entries)");
                // Debug.Log($"TMDbTools::EnsureKnownIdsLoaded - Printing first 5 entries for debugging:");
                int count = 0;
                foreach (var entry in knownIds)
                {
                    // Debug.Log($"TMDbTools::EnsureKnownIdsLoaded - Key: {entry.Key}, Title: {entry.Value.original_title}");
                    count++;
                    if (count >= 5) break;
                } 
                return;
            }

            // Debug.Log("TMDbTools::EnsureKnownIdsLoaded - Cache empty, attempting local cache load");

            lock (KnownIdsLoadLock)
            {
                if (!knownIds.IsEmpty)
                {
                    // Debug.Log($"TMDbTools::EnsureKnownIdsLoaded - Cache was populated by another thread ({knownIds.Count} entries)");
                    return;
                }

                LoadKnownIdsFromLocalCache();

                if (knownIds.IsEmpty)
                {
                    // Debug.LogWarning("TMDbTools::EnsureKnownIdsLoaded - Local cache load completed but dictionary is still empty");
                }
                else
                {
                    // Debug.Log($"TMDbTools::EnsureKnownIdsLoaded - Cache ready with {knownIds.Count} entries");
                }
            }
        }

        private static void LoadKnownIdsFromLocalCache()
        {
            if (!TryLoadCacheJson(out string json))
            {
                // Debug.LogWarning("TMDbTools::LoadKnownIdsFromLocalCache - Cache not found. On Android, place the JSON at Assets/Resources/TMDbTools/movie_id_to_imdb_data_map.json");
                return;
            }

            try
            {
                JObject root = JObject.Parse(json);
                // Debug.Log($"TMDbTools::LoadKnownIdsFromLocalCache - Parsed root with {root.Count} entries");

                int addedCount = 0;
                int skippedCount = 0;
                foreach (JProperty entry in root.Properties())
                {
                    if (entry.Value is not JObject movieJson)
                    {
                        skippedCount++;
                        continue;
                    }

                    MovieInfo info = ParseMovieInfo(movieJson);
                    if (info == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    string key = entry.Name;
                    if (knownIds.TryAdd(key, info))
                    {
                        addedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                // Debug.Log($"TMDbTools::LoadKnownIdsFromLocalCache - Loaded {addedCount} movies from local cache, skipped {skippedCount}");
            }
            catch (Exception ex)
            {
                // Debug.Log($"TMDbTools::LoadKnownIdsFromLocalCache - Failed to parse cache: {ex.Message}");
            }
        }

        private static bool TryLoadCacheJson(out string json)
        {
            TextAsset resourceAsset = Resources.Load<TextAsset>(CacheResourcesPath);
            if (resourceAsset != null && !string.IsNullOrWhiteSpace(resourceAsset.text))
            {
                // Debug.Log($"TMDbTools::LoadKnownIdsFromLocalCache - Loaded cache via Resources at {CacheResourcesPath}");
                json = resourceAsset.text;
                return true;
            }

            string cachePath = Path.Combine(Application.dataPath, "TMDbTools", CacheFileName);
            // Debug.Log($"TMDbTools::LoadKnownIdsFromLocalCache - Resources miss, checking file path {cachePath}");

            if (File.Exists(cachePath))
            {
                json = File.ReadAllText(cachePath);
                // Debug.Log($"TMDbTools::LoadKnownIdsFromLocalCache - Loaded cache via File at {cachePath}");
                return true;
            }

            json = null;
            return false;
        }

        private static MovieInfo ParseMovieInfo(JObject movieJson)
        {
            return new MovieInfo
            {
                adult = movieJson.Value<bool?>("adult") ?? false,
                backdrop_path = movieJson.Value<string>("backdrop_path"),
                belongs_to_collection = movieJson["belongs_to_collection"]?.Type == JTokenType.Null
                    ? null
                    : movieJson["belongs_to_collection"]?.ToString(Newtonsoft.Json.Formatting.None),
                budget = movieJson.Value<int?>("budget") ?? 0,
                genres = movieJson["genres"]?.ToObject<Genre[]>() ?? Array.Empty<Genre>(),
                homepage = movieJson.Value<string>("homepage"),
                id = movieJson.Value<int?>("id") ?? 0,
                imdb_id = movieJson.Value<string>("imdb_id"),
                original_language = movieJson.Value<string>("original_language"),
                original_title = movieJson.Value<string>("original_title"),
                overview = movieJson.Value<string>("overview"),
                popularity = Mathf.RoundToInt(movieJson.Value<float?>("popularity") ?? 0f),
                poster_path = movieJson.Value<string>("poster_path"),
                production_companies = movieJson["production_companies"]?.ToObject<ProductionCompany[]>() ?? Array.Empty<ProductionCompany>(),
                production_countries = movieJson["production_countries"]?.ToObject<ProductionCountry[]>() ?? Array.Empty<ProductionCountry>(),
                release_date = movieJson.Value<string>("release_date"),
                revenue = movieJson.Value<int?>("revenue") ?? 0,
                runtime = movieJson.Value<int?>("runtime") ?? 0,
                spoken_languages = movieJson["spoken_languages"]?.ToObject<SpokenLanguage[]>() ?? Array.Empty<SpokenLanguage>(),
                status = movieJson.Value<string>("status"),
                tagline = movieJson.Value<string>("tagline"),
                title = movieJson.Value<string>("title"),
                video = movieJson.Value<bool?>("video") ?? false,
                vote_average = Mathf.RoundToInt(movieJson.Value<float?>("vote_average") ?? 0f),
                vote_count = movieJson.Value<int?>("vote_count") ?? 0
            };
        }
        
        /// <summary>
        /// Queries TMDb to retrieve all movie information for a given ID
        /// </summary>
        /// <param name="movieID">The movie ID to be queried</param>
        /// <returns>Returns a <see cref="MovieInfo"/> object with all movie information</returns>  
        public async static Task<MovieInfo> GetMovieInfo(string movieID)
        {
            if (string.IsNullOrEmpty(movieID))
            {
                throw new ArgumentNullException("movieID cannot be null");
            }

            // Debug.Log($"TMDbTools::GetMovieInfo - Request start for id {movieID}");

            // Fail-safe to support offline testing if the static load path was skipped.
            EnsureKnownIdsLoaded();

            if (knownIds.ContainsKey(movieID))
            {
                // Debug.Log($"TMDbTools::GetMovieInfo - Found ID {movieID} in dictionary");
                return knownIds[movieID];
            }

            // Debug.Log($"TMDbTools::GetMovieInfo - ID {movieID} not found in dictionary, requesting from TMDb API");
                
            using (UnityWebRequest request = UnityWebRequest.Get($"https://api.themoviedb.org/3/movie/{movieID}?language=en-US"))
            {
                request.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
                request.SetRequestHeader("accept", "application/json");

                // Debug.Log($"TMDbTools::GetMovieInfo - Sending request for id {movieID}");
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Debug.Log("TMDbTools::GetMovieInfo - Got response");
                    string jsonResponse = request.downloadHandler.text;
                    MovieInfo info = JsonUtility.FromJson<MovieInfo>(jsonResponse);
                    if (info == null)
                    {
                        // Debug.LogWarning($"TMDbTools::GetMovieInfo - Response parsed to null for id {movieID}");
                        return null;
                    }

                    // Debug.Log($"TMDbTools::GetMovieInfo - response title = {info.original_title}");
                    knownIds.GetOrAdd(movieID, info);
                    // Debug.Log($"TMDbTools::GetMovieInfo - Added/updated cache for id {movieID}. Cache size now {knownIds.Count}");
                    return knownIds[movieID];
                } else
                {
                    // Debug.LogError($"TMDbTools::GetMovieInfo - Error: {request.error}");
                    return null;
                }
            }
        }

        public async static Task<string> GetMovieNameFromID(string movieID)
        {
            // Debug.Log("TMDbTools::GetMovieNameFromID - Getting movie details...");
            MovieInfo info = await GetMovieInfo(movieID);
            return info?.original_title;
        }
    }

    [Serializable]
    public class Genre
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class ProductionCompany
    {
        public int id;
        public string logo_path;
        public string name;
        public string origin_country;
    }

    [Serializable]
    public class ProductionCountry
    {
        public string iso_3166_1;
        public string name;
    }
    
    [Serializable]
    public class SpokenLanguage
    {
        public string english_name;
        public string iso_639_1;
        public string name;
    }

    [Serializable]
    public class MovieInfo
    {
        public bool adult;
        public string backdrop_path;
        public string belongs_to_collection;
        public int budget;
        public Genre[] genres;
        public string homepage;
        public int id;
        public string imdb_id;
        public string original_language;
        public string original_title;
        public string overview;
        public int popularity;
        public string poster_path;
        public ProductionCompany[] production_companies;
        public ProductionCountry[] production_countries;
        public string release_date;
        public int revenue;
        public int runtime;
        public SpokenLanguage[] spoken_languages;
        public string status;
        public string tagline;
        public string title;
        public bool video;
        public int vote_average;
        public int vote_count;
    }    
}
