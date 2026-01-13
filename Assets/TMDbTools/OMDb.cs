using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using static TMDbTools.OMDb_API_KEY;
using System.Collections.Concurrent;

namespace TMDbTools
{
    public class OMDb
    {
        public static ConcurrentDictionary<string, OMDbMovieInfo> knownIds = new ConcurrentDictionary<string, OMDbMovieInfo>();

        private const string API_KEY = OMDB_API_KEY;

        public async static Task<OMDbMovieInfo> GetOMDbInfo(string imdbID)
        {
            if (string.IsNullOrEmpty(imdbID))
            {
                throw new ArgumentNullException("imdbID cannot be null");
            }

            if (knownIds.ContainsKey(imdbID))
            {
                Debug.Log($"OMDb::GetOMDbInfo - Found ID {imdbID} in dictionary");
                return knownIds[imdbID];
            }

            using (UnityWebRequest request = UnityWebRequest.Get($"https://www.omdbapi.com/?i={imdbID}&apikey={API_KEY}"))
            {
                request.SetRequestHeader("accept", "application/json");

                Debug.Log($"OMDb::GetOMDbInfo - Sending request for id {imdbID}");
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("OMDb::GetOMDbInfo - Got response");
                    string jsonResponse = request.downloadHandler.text;
                    OMDbMovieInfo info = JsonUtility.FromJson<OMDbMovieInfo>(jsonResponse);
                    knownIds.GetOrAdd(imdbID, info);
                    return knownIds[imdbID];
                } else
                {
                    Debug.LogError($"OMDb::GetOMDbInfo - Error: {request.error}");
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