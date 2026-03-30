using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aion2DPSViewer.Core;

public class Updater
{
    private static readonly string CurrentVersion = typeof(Updater).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    public static readonly string UpdateLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "A2Viewer", "a2viewer_update.log");
    private static readonly HttpClient Http;
    private static readonly HttpClient DownloadHttp;

    private static string GitHubOwner => Secrets.GitHubOwner;
    private static string GitHubRepo => Secrets.GitHubRepo;
    private static string UpdateUrl => $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    static Updater()
    {
        HttpClient httpClient1 = new HttpClient();
        httpClient1.Timeout = TimeSpan.FromSeconds(10.0);
        httpClient1.DefaultRequestHeaders.Add("User-Agent", "A2Viewer-Updater");
        httpClient1.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        Http = httpClient1;
        HttpClient httpClient2 = new HttpClient();
        httpClient2.Timeout = TimeSpan.FromMinutes(10.0);
        httpClient2.DefaultRequestHeaders.Add("User-Agent", "A2Viewer-Updater");
        DownloadHttp = httpClient2;
    }

    public static async Task<UpdateInfo?> CheckForUpdate()
    {
        JsonElement rootElement = JsonDocument.Parse(await Http.GetStringAsync(UpdateUrl)).RootElement;
        JsonElement property = rootElement.GetProperty("tag_name");
        string latest = (property.GetString() ?? "").TrimStart('v');
        if (string.IsNullOrEmpty(latest) || CompareVersions(CurrentVersion, latest) < 1)
            return null;
        string? str1 = null;
        string str2 = $"A2Viewer-{latest}.exe";
        JsonElement jsonElement1;
        if (rootElement.TryGetProperty("assets", out jsonElement1) && jsonElement1.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement enumerate in jsonElement1.EnumerateArray())
            {
                property = enumerate.GetProperty("name");
                string str3 = property.GetString() ?? "";
                if (str3.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    property = enumerate.GetProperty("browser_download_url");
                    str1 = property.GetString();
                    str2 = str3;
                    break;
                }
            }
        }
        property = rootElement.GetProperty("html_url");
        string str4 = property.GetString() ?? "";
        JsonElement jsonElement2;
        string str5 = rootElement.TryGetProperty("body", out jsonElement2) ? jsonElement2.GetString() ?? "" : "";
        return new UpdateInfo()
        {
            Version = latest,
            CurrentVersion = CurrentVersion,
            DownloadUrl = str1 ?? str4,
            ReleaseUrl = str4,
            ReleaseNotes = str5,
            AssetName = str2
        };
    }

    public static async Task<string> DownloadUpdate(UpdateInfo info, Action<int>? onProgress = null)
    {
        string dest = Path.Combine(Path.GetTempPath(), info.AssetName);
        using (HttpResponseMessage response = await DownloadHttp.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            long totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
            long downloaded = 0;
            using (Stream contentStream = await response.Content.ReadAsStreamAsync())
            using (FileStream fileStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 8192))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloaded += bytesRead;
                    if (totalBytes > 0)
                        onProgress?.Invoke((int)(downloaded * 100L / totalBytes));
                }
            }
        }
        return dest;
    }

    public static void InstallUpdate(string filePath)
    {
        string str1 = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(str1))
            return;
        string str2 = Path.Combine(Path.GetTempPath(), "a2viewer_update.cmd");
        string updateLogPath = UpdateLogPath;
        int processId = Process.GetCurrentProcess().Id;
        try { File.Delete(updateLogPath); } catch { }
        string str3 = $"@echo off\nchcp 65001 >nul 2>&1\nset \"LOG={updateLogPath}\"\nset \"SRC={filePath}\"\nset \"DST={str1}\"\nset \"PID={processId}\"\n\necho [%TIME%] Update start: %SRC% -^> %DST% >> \"%LOG%\"\n\nREM 1. 프로세스 종료 대기 (최대 30초)\nset WAIT=0\n:waitloop\nset /a WAIT+=1\nif %WAIT% gtr 30 (\n    echo [%TIME%] Process %PID% still alive after 30s, proceeding >> \"%LOG%\"\n    goto replace\n)\ntasklist /FI \"PID eq %PID%\" /NH 2>nul | findstr /I \"A2Viewer\" >nul\nif errorlevel 1 (\n    echo [%TIME%] Process %PID% exited wait=%WAIT%s >> \"%LOG%\"\n    goto replace\n)\ntimeout /t 1 /nobreak >nul\ngoto waitloop\n\n:replace\nREM 2. 파일 교체 (최대 10회 재시도)\nset OK=0\nset ATTEMPT=0\n:retry\nset /a ATTEMPT+=1\nif %ATTEMPT% gtr 10 goto done\necho [%TIME%] Replace attempt %ATTEMPT% >> \"%LOG%\"\ndel /f /q \"%DST%.old\" >nul 2>&1\nif exist \"%DST%\" (\n    move /y \"%DST%\" \"%DST%.old\" >nul 2>&1\n)\ncopy /y \"%SRC%\" \"%DST%\" >nul 2>&1\nif exist \"%DST%\" (\n    set OK=1\n    echo [%TIME%] File replaced attempt=%ATTEMPT% >> \"%LOG%\"\n    goto done\n)\necho [%TIME%] Attempt %ATTEMPT% failed >> \"%LOG%\"\ntimeout /t 2 /nobreak >nul\ngoto retry\n\n:done\nif \"%OK%\"==\"1\" (\n    echo [%TIME%] Starting new process >> \"%LOG%\"\n    start \"\" \"%DST%\"\n    timeout /t 2 /nobreak >nul\n    del /f /q \"%SRC%\" >nul 2>&1\n    del /f /q \"%DST%.old\" >nul 2>&1\n    echo [%TIME%] Update complete >> \"%LOG%\"\n) else (\n    echo [%TIME%] UPDATE FAILED - restoring original >> \"%LOG%\"\n    if exist \"%DST%.old\" (\n        move /y \"%DST%.old\" \"%DST%\" >nul 2>&1\n    )\n    start \"\" \"%DST%\"\n)\n\ndel /f /q \"{str2}\" >nul 2>&1\nexit /b".Replace("\n", "\r\n");
        File.WriteAllText(str2, str3, new UTF8Encoding(false));
        Console.Error.WriteLine($"[updater] 업데이트 실행: {filePath} → {str1} (PID: {processId})");
        Process.Start(new ProcessStartInfo()
        {
            FileName = str2,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        });
        Task.Delay(500).ContinueWith(_ =>
        {
            Form? openForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (openForm != null && !openForm.IsDisposed)
                openForm.BeginInvoke((Action)(() => Application.Exit()));
            else
                Environment.Exit(0);
        });
    }

    private static int CompareVersions(string current, string latest)
    {
        int[] version1 = ParseVersion(current);
        int[] version2 = ParseVersion(latest);
        for (int index = 0; index < 3; ++index)
        {
            if (version2[index] > version1[index])
                return 1;
            if (version2[index] < version1[index])
                return -1;
        }
        return 0;
    }

    private static int[] ParseVersion(string v)
    {
        string[] strArray = v.TrimStart('v').Split(new[] { '.' }, StringSplitOptions.None);
        int[] version = new int[3];
        for (int index = 0; index < Math.Min(strArray.Length, 3); ++index)
            int.TryParse(strArray[index], out version[index]);
        return version;
    }
}
