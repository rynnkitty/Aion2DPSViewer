using System;
using System.IO;
using System.Text.Json;

namespace Aion2DPSViewer.Core;

public static class FileCache
{
    private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aion2Info");

    static FileCache() => Directory.CreateDirectory(CacheDir);

    public static void SeedFromEmbeddedResource(string cacheFilename, string resourceName)
    {
        string str = CachePath(cacheFilename);
        if (File.Exists(str))
            return;
        try
        {
            using (var manifestResourceStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (manifestResourceStream == null)
                    return;
                using (var streamReader = new StreamReader(manifestResourceStream))
                {
                    string end = streamReader.ReadToEnd();
                    File.WriteAllText(str, end);
                    Console.Error.WriteLine($"[cache] 임베디드 리소스에서 {cacheFilename} 생성");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[cache] 리소스 추출 실패 ({cacheFilename}): {ex.Message}");
        }
    }

    public static string CachePath(string filename) => Path.Combine(CacheDir, filename);

    public static void SaveCache(string filename, JsonElement data)
    {
        try
        {
            var data1 = new
            {
                updated = DateTime.UtcNow.ToString("o"),
                data = data
            };
            File.WriteAllText(CachePath(filename), JsonSerializer.Serialize(data1));
        }
        catch { }
    }

    public static void SaveCacheRaw(string filename, string json)
    {
        try
        {
            string str = $"{{\"updated\":\"{DateTime.UtcNow:o}\",\"data\":{json}}}";
            File.WriteAllText(CachePath(filename), str);
        }
        catch { }
    }

    public static JsonElement? LoadCache(string filename)
    {
        try
        {
            JsonElement jsonElement;
            if (JsonDocument.Parse(File.ReadAllText(CachePath(filename))).RootElement.TryGetProperty("data", out jsonElement))
                return jsonElement;
        }
        catch { }
        return null;
    }

    public static bool CacheExists(string filename) => File.Exists(CachePath(filename));

    public static void WriteRaw(string filename, string content)
    {
        try
        {
            File.WriteAllText(CachePath(filename), content);
        }
        catch { }
    }

    public static string? ReadRaw(string filename)
    {
        string str = CachePath(filename);
        return !File.Exists(str) ? null : File.ReadAllText(str);
    }
}
