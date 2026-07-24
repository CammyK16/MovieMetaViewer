using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using static TMDbTools.OMDb_API_KEY;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TMDbTools
{
    public class OMDb
    {
        public static ConcurrentDictionary<string, OMDbMovieInfo> knownIds = new ConcurrentDictionary<string, OMDbMovieInfo>();
        private static readonly object KnownIdsLoadLock = new object();
        private const string CacheFileName = "imdb_to_omdb_map.json";
        private const string CacheResourcesPath = "TMDbTools/imdb_to_omdb_map";

        private const string API_KEY = OMDB_API_KEY;

        static OMDb()
        {
            EnsureKnownIdsLoaded();
        }

        private static void EnsureKnownIdsLoaded()
        {
            if (!knownIds.IsEmpty)
            {
                //Debug.Log($"OMDb::EnsureKnownIdsLoaded - Cache already populated ({knownIds.Count} entries)");
                return;
            }

            //Debug.Log("OMDb::EnsureKnownIdsLoaded - Cache empty, attempting local cache load");

            lock (KnownIdsLoadLock)
            {
                if (!knownIds.IsEmpty)
                {
                    //Debug.Log($"OMDb::EnsureKnownIdsLoaded - Cache was populated by another thread ({knownIds.Count} entries)");
                    return;
                }

                LoadKnownIdsFromLocalCache();

                if (knownIds.IsEmpty)
                {
                    //Debug.LogWarning("OMDb::EnsureKnownIdsLoaded - Local cache load completed but dictionary is still empty");
                }
                else
                {
                    //Debug.Log($"OMDb::EnsureKnownIdsLoaded - Cache ready with {knownIds.Count} entries");
                }
            }
        }

        private static void LoadKnownIdsFromLocalCache()
        {
            if (!TryLoadCacheJson(out string json))
            {
                //Debug.LogWarning("OMDb::LoadKnownIdsFromLocalCache - Cache not found. On Android, place the JSON at Assets/Resources/TMDbTools/imdb_to_omdb_map.json");
                return;
            }

            try
            {
                JObject root = JObject.Parse(json);
                //Debug.Log($"OMDb::LoadKnownIdsFromLocalCache - Parsed root with {root.Count} entries");

                int addedCount = 0;
                int skippedCount = 0;
                foreach (JProperty entry in root.Properties())
                {
                    if (entry.Value is not JObject omdbJson)
                    {
                        skippedCount++;
                        continue;
                    }

                    OMDbMovieInfo info = ParseOmdbMovieInfo(omdbJson);
                    if (info == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    string key = !string.IsNullOrEmpty(info.imdbId) ? info.imdbId : entry.Name;
                    if (knownIds.TryAdd(key, info))
                    {
                        addedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                //Debug.Log($"OMDb::LoadKnownIdsFromLocalCache - Loaded {addedCount} movies from local cache, skipped {skippedCount}");
            }
            catch (Exception ex)
            {
                //Debug.LogError($"OMDb::LoadKnownIdsFromLocalCache - Failed to parse cache: {ex.Message}");
            }
        }

        private static bool TryLoadCacheJson(out string json)
        {
            TextAsset resourceAsset = Resources.Load<TextAsset>(CacheResourcesPath);
            if (resourceAsset != null && !string.IsNullOrWhiteSpace(resourceAsset.text))
            {
                //Debug.Log($"OMDb::LoadKnownIdsFromLocalCache - Loaded cache via Resources at {CacheResourcesPath}");
                json = resourceAsset.text;
                return true;
            }

            string cachePath = Path.Combine(Application.dataPath, "TMDbTools", CacheFileName);
            //Debug.Log($"OMDb::LoadKnownIdsFromLocalCache - Resources miss, checking file path {cachePath}");

            if (File.Exists(cachePath))
            {
                json = File.ReadAllText(cachePath);
                //Debug.Log($"OMDb::LoadKnownIdsFromLocalCache - Loaded cache via File at {cachePath}");
                return true;
            }

            json = null;
            return false;
        }

        private static OMDbMovieInfo ParseOmdbMovieInfo(JObject omdbJson)
        {
            return new OMDbMovieInfo
            {
                Title = omdbJson.Value<string>("Title"),
                Year = omdbJson.Value<string>("Year"),
                Rated = omdbJson.Value<string>("Rated"),
                Released = omdbJson.Value<string>("Released"),
                Runtime = omdbJson.Value<string>("Runtime"),
                Genre = omdbJson.Value<string>("Genre"),
                Director = omdbJson.Value<string>("Director"),
                Writer = omdbJson.Value<string>("Writer"),
                Actors = omdbJson.Value<string>("Actors"),
                Plot = omdbJson.Value<string>("Plot"),
                Language = omdbJson.Value<string>("Language"),
                Country = omdbJson.Value<string>("Country"),
                Awards = omdbJson.Value<string>("Awards"),
                Poster = omdbJson.Value<string>("Poster"),
                Ratings = omdbJson["Ratings"]?.ToObject<OMDbRatings[]>() ?? Array.Empty<OMDbRatings>(),
                Metascore = omdbJson.Value<string>("Metascore"),
                imdbRating = omdbJson.Value<string>("imdbRating"),
                imdbVotes = omdbJson.Value<string>("imdbVotes"),
                imdbId = omdbJson.Value<string>("imdbID") ?? omdbJson.Value<string>("imdbId"),
                Type = omdbJson.Value<string>("Type"),
                Dvd = omdbJson.Value<string>("DVD") ?? omdbJson.Value<string>("Dvd"),
                BoxOffice = omdbJson.Value<string>("BoxOffice")
            };
        }

        public async static Task<OMDbMovieInfo> GetOMDbInfo(string imdbID)
        {
            if (string.IsNullOrEmpty(imdbID))
            {
                throw new ArgumentNullException("imdbID cannot be null");
            }

            //Debug.Log($"OMDb::GetOMDbInfo - Request start for id {imdbID}");

            // Fail-safe to support offline testing if the static load path was skipped.
            EnsureKnownIdsLoaded();

            if (knownIds.ContainsKey(imdbID))
            {
                //Debug.Log($"OMDb::GetOMDbInfo - Found ID {imdbID} in dictionary");
                return knownIds[imdbID];
            }

            //Debug.Log($"OMDb::GetOMDbInfo - ID {imdbID} not found in dictionary, requesting from OMDb API");

            using (UnityWebRequest request = UnityWebRequest.Get($"https://www.omdbapi.com/?i={imdbID}&apikey={API_KEY}"))
            {
                request.SetRequestHeader("accept", "application/json");

                //Debug.Log($"OMDb::GetOMDbInfo - Sending request for id {imdbID}");
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    //Debug.Log("OMDb::GetOMDbInfo - Got response");
                    string jsonResponse = request.downloadHandler.text;
                    OMDbMovieInfo info = JsonUtility.FromJson<OMDbMovieInfo>(jsonResponse);
                    if (info == null)
                    {
                        //Debug.LogWarning($"OMDb::GetOMDbInfo - Response parsed to null for id {imdbID}");
                        return null;
                    }

                    knownIds.GetOrAdd(imdbID, info);
                    //Debug.Log($"OMDb::GetOMDbInfo - Added/updated cache for id {imdbID}. Cache size now {knownIds.Count}");
                    return knownIds[imdbID];
                } else
                {
                    //Debug.LogError($"OMDb::GetOMDbInfo - Error: {request.error}");
                    return null;
                }
            }
        }
    }

    [Serializable]
    public class OMDbRatings
    {
        public string Source;
        public string Value;
    }


    [Serializable]
    public class OMDbMovieInfo
    {
        public string Title;
        public string Year;
        public string Rated;
        public string Released;
        public string Runtime;
        public string Genre;
        public string Director;
        public string Writer;
        public string Actors;
        public string Plot;
        public string Language;
        public string Country;
        public string Awards;
        public string Poster;
        public OMDbRatings[] Ratings;
        public string Metascore;
        public string imdbRating;
        public string imdbVotes;
        public string imdbId;
        public string Type;
        public string Dvd;
        public string BoxOffice;
    }
}