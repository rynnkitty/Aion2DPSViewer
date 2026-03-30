using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Aion2DPSViewer.Core;

public static class EmbeddedWebServer
{
    private static readonly Dictionary<string, (byte[] data, string mime)> _cache = new Dictionary<string, (byte[], string)>();
    private const string Host = "a2viewer.local";

    public static void Setup(CoreWebView2 core)
    {
        LoadResources();
        core.AddWebResourceRequestedFilter("https://a2viewer.local/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += new EventHandler<CoreWebView2WebResourceRequestedEventArgs>(OnResourceRequested);
    }

    private static void LoadResources()
    {
        Assembly assembly = typeof(EmbeddedWebServer).Assembly;
        string str1 = "A2Viewer.web.";
        foreach (string manifestResourceName in assembly.GetManifestResourceNames())
        {
            if (manifestResourceName.StartsWith(str1))
            {
                string str2 = manifestResourceName;
                int length = str1.Length;
                string path = ConvertResourceNameToPath(str2.Substring(length, str2.Length - length));
                using (Stream manifestResourceStream = assembly.GetManifestResourceStream(manifestResourceName))
                {
                    if (manifestResourceStream != null)
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            manifestResourceStream.CopyTo(memoryStream);
                            byte[] array = memoryStream.ToArray();
                            string mimeType = GetMimeType(path);
                            _cache["/" + path] = (array, mimeType);
                        }
                    }
                }
            }
        }
        Console.Error.WriteLine($"[embedded] {_cache.Count}개 웹 리소스 로드");
        foreach (string key in _cache.Keys)
        {
            if (key.Contains("font", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine("[embedded]   font: " + key);
        }
    }

    private static string ConvertResourceNameToPath(string resourceName)
    {
        string[] strArray = new string[] { "assets", "fonts" };
        foreach (string str1 in strArray)
        {
            if (resourceName.StartsWith(str1 + "."))
            {
                string str2 = str1;
                string str3 = resourceName;
                int num = str1.Length + 1;
                string str4 = str3.Substring(num, str3.Length - num);
                return $"{str2}/{str4}";
            }
        }
        return resourceName;
    }

    private static void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        string str1 = Uri.UnescapeDataString(new Uri(e.Request.Uri).AbsolutePath);
        if (str1 == "/" || str1 == "")
            str1 = "/index.html";
        (byte[], string) valueTuple = default;
        if (!_cache.TryGetValue(str1, out valueTuple))
        {
            string str2 = str1.Replace('-', '_');
            _cache.TryGetValue(str2, out valueTuple);
        }
        if (valueTuple.Item1 != null)
        {
            if (str1.Contains("font", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine($"[embedded] 서빙: {str1} ({valueTuple.Item1.Length} bytes)");
            MemoryStream Content = new MemoryStream(valueTuple.Item1);
            CoreWebView2 coreWebView2 = (CoreWebView2)sender;
            string Headers = $"Content-Type: {valueTuple.Item2}\nAccess-Control-Allow-Origin: *";
            e.Response = coreWebView2.Environment.CreateWebResourceResponse(Content, 200, "OK", Headers);
        }
        else
        {
            if (!str1.Contains("font", StringComparison.OrdinalIgnoreCase))
                return;
            Console.Error.WriteLine("[embedded] 미발견: " + str1);
        }
    }

    private static string GetMimeType(string path)
    {
        string lower = Path.GetExtension(path).ToLower();
        switch (lower)
        {
            case ".js": return "application/javascript; charset=utf-8";
            case ".css": return "text/css; charset=utf-8";
            case ".html": return "text/html; charset=utf-8";
            case ".json": return "application/json";
            case ".png": return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".svg": return "image/svg+xml";
            case ".woff": return "font/woff";
            case ".woff2": return "font/woff2";
            default: return "application/octet-stream";
        }
    }
}
