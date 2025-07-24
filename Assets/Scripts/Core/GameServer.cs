using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Linq;
using System.IO;
using System.Text;

public class GameServer : MonoBehaviour
{
    public static GameServer Instance { get; private set; }
    
    [Header("Network Config")]
    public int httpPort = 8000;
    public int wsPort = 9000;

    public string roomCode;
    
    private HttpListener _httpServer;
    private WebSocketServer _wsServer;
    private readonly Queue<Action> _mainThreadQueue = new();

    void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        DontDestroyOnLoad(gameObject);
        roomCode = UnityEngine.Random.Range(1000, 9999).ToString();
    }

    void Start()
    {
        Application.runInBackground = true;
        StartHttpServer();
        StartWebSocketServer();
        
    }
    private void OnApplicationFocus(bool hasFocus)
    {
        // This keeps the server responsive even when Unity window loses focus
        Application.runInBackground = true;
    }

    private void OnApplicationQuit()
    {
        StopServers();
    }

    private void StopServers()
    {
        try
        {
            if (_httpServer != null)
            {
                if (_httpServer.IsListening)
                {
                    _httpServer.Stop();
                }
                _httpServer.Close();
                _httpServer = null;
            }
            
            if (_wsServer != null)
            {
                if (_wsServer.IsListening)
                {
                    _wsServer.Stop();
                }
                _wsServer = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error stopping servers: {ex.Message}");
        }
    }
    public string GetRoomCode()
    {
        return roomCode;
    }

    private void StartHttpServer()
    {
        _httpServer = new HttpListener();
        _httpServer.Prefixes.Add($"http://+:{httpPort}/");
        _httpServer.Start();
        _httpServer.BeginGetContext(HandleHttpRequest, null);
    }

    private void StartWebSocketServer()
    {
        _wsServer = new WebSocketServer(wsPort);
        _wsServer.AddWebSocketService<GameSocket>("/game");
        _wsServer.Start();
    }
    // MIME type dictionary
    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".html", "text/html" },
        { ".js", "application/javascript" },
        { ".css", "text/css" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".json", "application/json" },
        { ".ico", "image/x-icon" }
    };

    private void HandleHttpRequest(IAsyncResult result)
    {
        var context = _httpServer.EndGetContext(result);
        var path = context.Request.Url.LocalPath.TrimStart('/');

        try
        {
            if (HandleApiEndpoint(context, path)) return;
            ServeStaticFile(context, path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HTTP] Error handling {path}: {ex.Message}");
            SendResponse(context, 500, "Internal server error");
        }
        finally
        {
            _httpServer.BeginGetContext(HandleHttpRequest, null);
        }
    }

    private bool HandleApiEndpoint(HttpListenerContext context, string path)
    {
        if (path.StartsWith("api/"))
        {
            switch (path)
            {
                case "api/classes":
                    HandleGetClasses(context);
                    return true;
                    
                case "api/inventory":
                    // Prepare for future endpoints
                    SendResponse(context, 501, "Not implemented");
                    return true;
                case "api/items":
                    // Prepare for future endpoints
                    SendResponse(context, 501, "Not implemented");
                    return true;
                    
                default:
                    SendResponse(context, 404, "Endpoint not found");
                    return true;
            }
        }
        return false;
    }

    private void HandleGetClasses(HttpListenerContext context)
    {
        GameServer.Instance.QueueMainThread(() =>
        {
            var classes = ClassManager.Instance.GetAvailableClasses()
                .Select(c => new
                {
                    className   = c.className,
                    iconPath = $"images/classes/{c.name}.png"
                })
                .ToList();

            SendJsonResponse(context, classes);
        });
    }

    private void ServeStaticFile(HttpListenerContext context, string path)
    {
        // Default to index.html if empty path
        if (string.IsNullOrEmpty(path)) path = "index.html";

        string requestedFile = Path.Combine(Application.streamingAssetsPath, "Web", path);
        string normalizedPath = Path.GetFullPath(requestedFile).Replace('\\', '/');
        string webRoot = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "Web")).Replace('\\', '/');

        // Security check - ensure file is within Web directory
        if (!normalizedPath.StartsWith(webRoot))
        {
            Debug.LogError($"Forbidden path attempt: {normalizedPath}");
            SendResponse(context, 403, "Forbidden");
            return;
        }

        // Check if file exists
        if (File.Exists(normalizedPath))
        {
            byte[] fileBytes = File.ReadAllBytes(normalizedPath);
            string mimeType = GetMimeType(Path.GetExtension(normalizedPath));
            
            context.Response.ContentType = mimeType;
            context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
            context.Response.Close();
            Debug.Log($"Served file: {normalizedPath}");
            return;
        }
         // Requested file does NOT exist
        else
        {
            // --- Improved Fallback Logic using Accept header ---
            string acceptHeader = context.Request.Headers["Accept"];
            // Check if the client prefers HTML (indicating likely a page navigation)
            bool wantsHtml = !string.IsNullOrEmpty(acceptHeader) && acceptHeader.Contains("text/html");

            if (wantsHtml)
            {
                // Likely a navigation request for SPA, fallback to index.html is appropriate
                string fallbackPath = Path.Combine(webRoot, "index.html");
                if (File.Exists(fallbackPath))
                {
                    byte[] fileBytes = File.ReadAllBytes(fallbackPath);
                    context.Response.ContentType = "text/html";
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
                    context.Response.Close(); // Ensure response is closed
                    Debug.Log($"[SPA Fallback] Served index.html for route: {path}");
                    return; // Exit after serving fallback
                }
                else
                {
                    // Fallback file itself is missing
                    Debug.LogError($"File not found: {normalizedPath} and fallback index.html also missing.");
                    SendResponse(context, 404, "Not found");
                    return;
                }
            }
            else
            {
                // Likely an asset request (image/css/js), fallback is misleading.
                // Return a proper 404 error.
                Debug.LogWarning($"Asset not found: {normalizedPath}. Client expected: {acceptHeader}");
                SendResponse(context, 404, "Asset not found");
                return; // Exit after sending 404
            }
            // --- End of Improved Fallback Logic ---
        }
    }

    private string GetMimeType(string extension)
    {
        return MimeTypes.TryGetValue(extension.ToLower(), out string mime) 
            ? mime 
            : "application/octet-stream";
    }

    private void SendJsonResponse(HttpListenerContext context, object data)
    {
        string json = JsonConvert.SerializeObject(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        
        context.Response.ContentType = "application/json";
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private void SendResponse(HttpListenerContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }
    public void Broadcast(object data)
    {
        _wsServer.WebSocketServices["/game"].Sessions.Broadcast(JsonConvert.SerializeObject(data));
    }

    public void SendToPlayer(string sessionId, object data)
    {
        _wsServer.WebSocketServices["/game"].Sessions.SendTo(JsonConvert.SerializeObject(data), sessionId);
    }

    public void QueueMainThread(Action action)
    {
        lock (_mainThreadQueue) _mainThreadQueue.Enqueue(action);
    }

    void Update()
    {
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
            {
                var action = _mainThreadQueue.Dequeue();
                if (action != null)
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Exception in queued main thread action: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }
    }
}

public class GameSocket : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e)
    {
        try
        {
            var msg = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Data);
            // Ensure HandleNetworkMessage also handles exceptions internally or wrap the call here
            GameServer.Instance.QueueMainThread(() => {
                try {
                    PlayerManager.Instance.HandleNetworkMessage(ID, msg);
                } catch (Exception ex) {
                    Debug.LogError($"Exception in HandleNetworkMessage for {ID}: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
        catch (Exception ex) // Catches deserialization errors or issues queueing
        {
            Debug.LogError($"Error processing WebSocket message: {ex.Message}\n{ex.StackTrace}");
            // Optionally send an error message back to the client
            // Send(JsonConvert.SerializeObject(new { error = "Invalid message format" }));
        }
    }

    protected override void OnClose(CloseEventArgs e)
    {
        GameServer.Instance.QueueMainThread(() => {
            try {
                PlayerManager.Instance.HandleDisconnect(ID);
            } catch (Exception ex) {
                Debug.LogError($"Exception in HandleDisconnect for {ID}: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }
}