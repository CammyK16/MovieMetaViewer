using System.Net;
using System.Text;
using System;
using UnityEngine;
using UnityEngine.Networking;
using YOLOTools.Utilities;
using System.Collections;

public class PiCameraManager : IPCameraManager
{
    public PiCameraManager(string imageUrl, bool downloadAsImage, string username, string password) : base(imageUrl, downloadAsImage, username, password)
    {
    }

    new protected void Update()
    {
        if (!_gettingFrame)
        {
            _gettingFrame = true;
            StartCoroutine(GetLatestImageFrame());
        }
    }

    new protected IEnumerator GetLatestImageFrame()
    {
        _webRequest = UnityWebRequestTexture.GetTexture(_imageUrl);

        var auth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
        _webRequest.SetRequestHeader("Authorization", auth);
        _webRequest.certificateHandler = new ForceCertificate();
        yield return _webRequest.SendWebRequest();
        Texture2D tempTexture = null;

        try
        {
            if (_webRequest.result != UnityWebRequest.Result.Success) throw new Exception("Web request failed");
            if (!_webRequest.GetResponseHeaders()["Content-Type"].StartsWith("image/")) throw new IPCameraException("Invalid URL: The resource is not an image");
            tempTexture = DownloadHandlerTexture.GetContent(_webRequest);
            _currentTexture = ImageRotator.RotateImage(tempTexture, -90);

        }
        catch (Exception e)
        {
            Debug.Log($"Response from: {_imageUrl} was {_webRequest.error}");
            Debug.Log(e);
        }
        finally
        {
            if (tempTexture != null) Destroy(tempTexture);
            _webRequest = null;
            _gettingFrame = false; 
        }
    }

    public override FeedDimensions GetFeedDimensions()
    {
        return _currentTexture == null ? new FeedDimensions(1024, 1024) : new FeedDimensions(_currentTexture.width, _currentTexture.height);
    }
}
