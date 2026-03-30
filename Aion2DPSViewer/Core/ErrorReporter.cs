using Aion2DPSViewer.Dps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Core;

public class ErrorReporter
{
    private static readonly string Version = typeof(ErrorReporter).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(15.0) };
    private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "A2Viewer", "a2viewer.log");
    private static readonly object LogLock = new object();

    private static string WebhookUrl => Secrets.WebhookUrl;

    public static void Clear()
    {
        try
        {
            string directoryName = Path.GetDirectoryName(LogPath);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);
            File.WriteAllText(LogPath, "");
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            lock (LogLock)
            {
                string directoryName = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
        }
        catch { }
    }

    public static string ReadLog()
    {
        try
        {
            return File.Exists(LogPath) ? File.ReadAllText(LogPath) : "";
        }
        catch
        {
            return "";
        }
    }

    private static byte[]? ReadPacketLog()
    {
        try
        {
            string packetLogDirectory = DpsMeter.PacketLogDirectory;
            if (!Directory.Exists(packetLogDirectory))
                return null;
            string[] files = Directory.GetFiles(packetLogDirectory, "packets_*.log");
            if (files.Length == 0)
                return null;
            Array.Sort<string>(files);
            string[] strArray = files;
            using (FileStream fileStream = new FileStream(strArray[strArray.Length - 1], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    fileStream.CopyTo(memoryStream);
                    byte[] array = memoryStream.ToArray();
                    return array.Length != 0 ? array : null;
                }
            }
        }
        catch
        {
            return null;
        }
    }

    private static void AttachExtraLogs(List<(string name, byte[] data, string contentType)> files)
    {
        byte[]? numArray1 = ReadPacketLog();
        if (numArray1 != null)
            files.Add(("packet_log.log", numArray1, "text/plain"));
    }

    public static async Task<(bool success, string? error)> SendErrorReport(string? userMessage)
    {
        try
        {
            string str1 = ReadLog();
            string str2 = string.Join("\n", new string[]
            {
                $"A2Viewer v{Version} (Native/.NET)",
                $".NET {Environment.Version} | {RuntimeInformation.OSDescription}",
                !string.IsNullOrEmpty(userMessage) ? "메시지: " + userMessage : ""
            }).TrimEnd('\n');
            string content = $"**에러 리포트** `v{Version}`\n```\n{str2}\n```";
            var files = new List<(string, byte[], string)>();
            files.Add(("a2viewer.log", Encoding.UTF8.GetBytes(str1), "text/plain"));
            AttachExtraLogs(files);
            return await SendToDiscord(content, files) ? (true, (string?)null) : (false, "Discord 전송 실패");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task SendCrashReport(Exception ex)
    {
        try
        {
            Log($"[CRASH] {ex}");
            string str1 = string.Join("\n", new string[]
            {
                $"A2Viewer v{Version} (Native/.NET)",
                $".NET {Environment.Version} | {RuntimeInformation.OSDescription}",
                $"예외: {ex.GetType().Name}: {ex.Message}",
                $"시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            });
            string content = $"**크래시 리포트** `v{Version}`\n```\n{str1}\n```";
            string str2 = ReadLog();
            var files = new List<(string, byte[], string)>();
            files.Add(("a2viewer.log", Encoding.UTF8.GetBytes(str2), "text/plain"));
            AttachExtraLogs(files);
            await SendToDiscord(content, files);
        }
        catch { }
    }

    private static async Task<bool> SendToDiscord(string content, List<(string name, byte[] data, string contentType)>? files = null)
    {
        bool successStatusCode;
        using (MultipartFormDataContent form = new MultipartFormDataContent())
        {
            form.Add(new StringContent(JsonSerializer.Serialize(new { content }), Encoding.UTF8, "application/json"), "payload_json");
            if (files != null)
            {
                for (int index = 0; index < files.Count; ++index)
                {
                    var file = files[index];
                    ByteArrayContent content1 = new ByteArrayContent(file.data);
                    content1.Headers.ContentType = new MediaTypeHeaderValue(file.contentType);
                    form.Add(content1, $"files[{index}]", file.name);
                }
            }
            successStatusCode = (await Http.PostAsync(WebhookUrl, form)).IsSuccessStatusCode;
        }
        return successStatusCode;
    }
}
