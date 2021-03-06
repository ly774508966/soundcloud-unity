﻿using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

namespace SoundCloud
{

public class SCWeb : MonoBehaviour
{
    private const string CONNECT_URL = "https://soundcloud.com/connect/?client_id={0}&redirect_uri={1}&response_type=code";
    private const string RESOLVE_URL = "http://api.soundcloud.com/resolve?url={0}&client_id={1}";
    private const string REDIRECT_URL = "http://localhost:{0}/";

    private const int LISTEN_PORT = 8080;
    private const int REDIRECT_LIMIT = 3;

    public bool authenticated { get; private set; }

    protected void Awake()
    {
        authenticated = false;
    }

    public IEnumerator ProcessResolveURL(string url, Action<bool, string> callback)
    {
        bool success = false;
        WWW response = null;
        string resolvedURL = string.Empty;
        string request = string.Format(RESOLVE_URL, url, SCConfig.CLIENT_ID);

        yield return StartCoroutine(WebRequest(request, (retVal) => response = retVal));

        if (string.IsNullOrEmpty(response.error))
        {
            // Construct base URL
            SCGeneric data = new SCGeneric();
            data.Deserialize(response.text);

            if (!string.IsNullOrEmpty(data.uri))
            {
                success = true;
                resolvedURL = data.uri + "?client_id=" + SCConfig.CLIENT_ID;
            }

            // TODO: Cache data so we don't retrieve it again
        }

        if (callback != null)
            callback(success, resolvedURL);
    }

    public IEnumerator WebRequest(string uri, Action<WWW> callback)
    {
        WWW www = new WWW(uri);
        yield return www;

        int redirects = 0;
        while (string.IsNullOrEmpty(www.error) &&
               www.responseHeaders.ContainsKey("STATUS") &&
               www.responseHeaders["STATUS"].Contains("302") &&
               redirects <= REDIRECT_LIMIT)
        {
            // If there's a redirect, follow it.
            // WWW seems to do this automatically at times, but we'll do this just in case it doesn't.
            www = new WWW(www.responseHeaders["LOCATION"]);
            yield return www;

            ++redirects;
        }

        if (!string.IsNullOrEmpty(www.error))
        {
            Debug.Log(www.error);
            yield break;
        }

        if (callback != null)
            callback(www);
    }

    public IEnumerator WebRequestObject<T>(string uri, Action<T> callback) where T : DataObject<T>, new()
    {
        T target = null;
        WWW response = null;
        yield return StartCoroutine(WebRequest(uri, (retVal) => response = retVal));

        if (string.IsNullOrEmpty(response.error))
        {
            target = new T();
            target.Deserialize(response.text);
        }

        if (callback != null)
            callback(target);
    }

    public IEnumerator WebRequestFile(string uri, string outputFilename, Action<string> callback)
    {
        string tempFile = string.Empty;
        WWW response = null;
        yield return StartCoroutine(WebRequest(uri, (retVal) => response = retVal));

        if (string.IsNullOrEmpty(response.error))
        {
            tempFile = SCManager.WORKING_DIRECTORY + Path.DirectorySeparatorChar + outputFilename;
            File.WriteAllBytes(tempFile, response.bytes);
        }

        if (callback != null)
            callback(tempFile);
    }

    public IEnumerator WebRequestAudioClip(string uri, Action<AudioClip> callback)
    {
        AudioClip clip = null;
        WWW response = null;
        yield return StartCoroutine(WebRequest(uri, (retVal) => response = retVal));

        if (string.IsNullOrEmpty(response.error))
        {
            clip = response.audioClip;
        }

        if (callback != null)
            callback(clip);
    }

    public IEnumerator WebRequestTexture(string uri, Action<Texture2D> callback)
    {
        Texture2D texture = null;
        WWW response = null;
        yield return StartCoroutine(WebRequest(uri, (retVal) => response = retVal));

        if (string.IsNullOrEmpty(response.error))
        {
            texture = response.texture;
        }

        if (callback != null)
            callback(texture);
    }

    public IEnumerator AuthenticateUser(Action<bool> callback)
    {
        // TODO: Check if there's already have a saved OAuth token.

        // Build local redirect listner address and the connection URL.
        string uriPrefix = string.Format(REDIRECT_URL, LISTEN_PORT);
        string connectUrl = string.Format(CONNECT_URL, SCConfig.CLIENT_ID, WWW.EscapeURL(uriPrefix + "unity-game-authentication"));

        // Create redirect listner.
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(uriPrefix);
        listener.Start();

        // Execute the listner on its own thread.
        Thread authListener = new Thread(
            () =>
            {
                HttpListenerContext context = listener.GetContext(); // This call blocks until there's a hit.
                ProcessAuthRequest(context);
                listener.Stop();
            }
        );
        authListener.Start();

        // Open SoundCloud's app connect page.
        Application.OpenURL(connectUrl);

        // TODO: Add Authentication Timeout and Cancel.
        while (!authenticated)
        {
            yield return 0;
        }

        if (callback != null)
            callback(authenticated);
    }

    private void ProcessAuthRequest(HttpListenerContext context)
    {
        HttpListenerRequest req = context.Request;
        HttpListenerResponse res = context.Response;

        Debug.Log(req.Url);
        // TODO: Parse OAuth code from url and save it to a file.

        using (Stream outputStream = res.OutputStream)
        {
            // TODO: Prepare a nice looking redirect page in a separate HTML file. Load it into response buffer.
            string responseString = "<HTML><BODY>Authenticated! You can now return to your game.</BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            outputStream.Write(buffer, 0, buffer.Length);
        }

        authenticated = true;
    }
}

}