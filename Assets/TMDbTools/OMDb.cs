using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using static TMDbTools.OMDb_API_KEY;

namespace TMDbTools
{
    public class OMDb
    {
        private const string API_KEY = OMDB_API_KEY;

        public async static Task<OMDbMovieInfo> GetOMDbInfo(string imdbID)
        {
            if (string.IsNullOrEmpty(imdbID))
            {
                throw new ArgumentNullException("imdbID cannot be null");
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
                    return info;
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