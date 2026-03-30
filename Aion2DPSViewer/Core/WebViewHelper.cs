using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Core;

internal static class WebViewHelper
{
    internal static async Task InitAsync(Microsoft.Web.WebView2.WinForms.WebView2 webView, string tempDirName, string? browserArgs = null)
    {
        CoreWebView2EnvironmentOptions options = string.IsNullOrEmpty(browserArgs)
            ? new CoreWebView2EnvironmentOptions()
            : new CoreWebView2EnvironmentOptions(browserArgs);
        await webView.EnsureCoreWebView2Async(
            await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(Path.GetTempPath(), tempDirName),
                options: options));
        CoreWebView2Settings settings = webView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
    }
}
