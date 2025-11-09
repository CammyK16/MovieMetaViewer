using UnityEngine;
using System;
using UnityEngine.Networking;
using System.Threading.Tasks;
using static TMDbTools.TMDb_API_KEY;
using System.Collections.Concurrent;

namespace TMDbTools
{
    public class TMDb
    {
        public static ConcurrentDictionary<string, MovieInfo> knownIds = new ConcurrentDictionary<string, MovieInfo>();

        private const string API_KEY = TMDB_API_KEY;
        
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

            if (knownIds.ContainsKey(movieID))
            {
                Debug.Log($"TMDbTools::GetMovieInfo - Found ID {movieID} in dictionary");
                return knownIds[movieID];
            } 
                
            using (UnityWebRequest request = UnityWebRequest.Get($"https://api.themoviedb.org/3/movie/{movieID}?language=en-US"))
            {
                request.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
                request.SetRequestHeader("accept", "application/json");

                Debug.Log($"TMDbTools::GetMovieInfo - Sending request for id {movieID}");
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("TMDbTools::GetMovieInfo - Got response");
                    string jsonResponse = request.downloadHandler.text;
                    MovieInfo info = JsonUtility.FromJson<MovieInfo>(jsonResponse);
                    Debug.Log($"TMDbTools::GetMovieInfo - response title = {info.original_title}");
                    knownIds.GetOrAdd(movieID, info);
                    return knownIds[movieID];
                } else
                {
                    Debug.LogError($"TMDbTools::GetMovieInfo - Error: {request.error}");
                    return null;
                }
            }
        }

        public async static Task<string> GetMovieNameFromID(string movieID)
        {
            Debug.Log("TMDbTools::GetMovieNameFromID - Getting movie details...");
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
        public string iso_3116_1;
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
