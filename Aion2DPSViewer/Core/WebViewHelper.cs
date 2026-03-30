using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Core;

internal static class WebViewHelper
{
    internal static async Task InitAsync(Microsoft.Web.WebView2.WinForms.WebView2 webView, string tempDirName, string? browserArgs = null)
    {
        string dataFolder = Path.Combine(Path.GetTempPath(), tempDirName);
        try
        {
            await TryInitAsync(webView, dataFolder, browserArgs);
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x8007139F) || ex.InnerException?.HResult == unchecked((int)0x8007139F))
        {
            Console.Error.WriteLine("[webview] 초기화 실패 (0x8007139F) — 데이터 폴더 초기화 후 재시도");
            try { Directory.Delete(dataFolder, recursive: true); } catch { }
            await TryInitAsync(webView, dataFolder, browserArgs);
        }
    }

    private static async Task TryInitAsync(Microsoft.Web.WebView2.WinForms.WebView2 webView, string dataFolder, string? browserArgs)
    {
        CoreWebView2EnvironmentOptions options = string.IsNullOrEmpty(browserArgs)
            ? new CoreWebView2EnvironmentOptions()
            : new CoreWebView2EnvironmentOptions(browserArgs);
        await webView.EnsureCoreWebView2Async(
            await CoreWebView2Environment.CreateAsync(
                userDataFolder: dataFolder,
                options: options));
        CoreWebView2Settings settings = webView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
    }
}
