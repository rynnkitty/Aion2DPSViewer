using Aion2DPSViewer.Core;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aion2DPSViewer.Forms;

public class SettingsForm : Form
{
    private Microsoft.Web.WebView2.WinForms.WebView2 _webView;
    private bool _resizing;
    private string _resizeDir = "";
    private Point _resizeCursorStart;
    private Rectangle _resizeBoundsStart;
    private Timer _moveTimer;

    public event Action<SettingsData>? SettingsSaved;

    public event Action? SuspendShortcuts;

    public event Action? ResumeShortcuts;

    public event Action<string>? ThemeChanged;

    public event Action<int, int>? ScalePreview;

    public event Action? GpuOptionChanged;

    public SettingsForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(280, 400);
        Size = new Size(310, 520);
        BackColor = Color.FromArgb(16, 20, 42);
        _moveTimer = new Timer() { Interval = 16 };
        _moveTimer.Tick += new EventHandler(MoveTimer_Tick);
    }

    private void MoveTimer_Tick(object? sender, EventArgs e)
    {
        if ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.None)
            StopResize();
        else if (!_resizing)
        {
            _moveTimer.Stop();
        }
        else
        {
            Point position = Cursor.Position;
            int num1 = position.X - _resizeCursorStart.X;
            int num2 = position.Y - _resizeCursorStart.Y;
            Rectangle resizeBoundsStart = _resizeBoundsStart;
            int x = resizeBoundsStart.X;
            int y = resizeBoundsStart.Y;
            int width = resizeBoundsStart.Width;
            int height = resizeBoundsStart.Height;
            Size minimumSize;
            if (_resizeDir.Contains("right"))
            {
                minimumSize = MinimumSize;
                width = Math.Max(minimumSize.Width, resizeBoundsStart.Width + num1);
            }
            if (_resizeDir.Contains("left"))
            {
                minimumSize = MinimumSize;
                width = Math.Max(minimumSize.Width, resizeBoundsStart.Width - num1);
                x = resizeBoundsStart.X + resizeBoundsStart.Width - width;
            }
            if (_resizeDir.Contains("bottom"))
            {
                minimumSize = MinimumSize;
                height = Math.Max(minimumSize.Height, resizeBoundsStart.Height + num2);
            }
            if (_resizeDir.Contains("top"))
            {
                minimumSize = MinimumSize;
                height = Math.Max(minimumSize.Height, resizeBoundsStart.Height - num2);
                y = resizeBoundsStart.Y + resizeBoundsStart.Height - height;
            }
            SetBounds(x, y, width, height);
        }
    }

    private void StartResize(string dir)
    {
        _resizing = true;
        _resizeDir = dir;
        _resizeCursorStart = Cursor.Position;
        _resizeBoundsStart = Bounds;
        _moveTimer.Start();
    }

    private void StopResize()
    {
        if (!_resizing)
            return;
        _resizing = false;
        _moveTimer.Stop();
    }

    public async Task InitAsync()
    {
        Microsoft.Web.WebView2.WinForms.WebView2 webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
        webView2.Dock = DockStyle.Fill;
        webView2.DefaultBackgroundColor = Color.FromArgb(16, 20, 42);
        _webView = webView2;
        Controls.Add(_webView);
        await WebViewHelper.InitAsync(_webView, "A2Viewer_WebView2_Settings");
        AppSettings instance = AppSettings.Instance;
        string str1 = JsonSerializer.Serialize(new
        {
            version = typeof(SettingsForm).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            shortcuts = instance.Shortcuts,
            overlayOnly = instance.OverlayOnlyWhenAion,
            textScale = instance.TextScale,
            fontScale = instance.FontScale,
            trackedSkills = instance.TrackedSkills,
            knownDpSkills = instance.KnownDpSkills,
            keepPartyOnRefresh = instance.KeepPartyOnRefresh,
            keepSelfOnRefresh = instance.KeepSelfOnRefresh,
            autoTabSwitch = instance.AutoTabSwitch,
            dpsPercentMode = instance.DpsPercentMode,
            scoreFormat = instance.ScoreFormat,
            dpsTimeMode = instance.DpsTimeMode,
            gpuMode = instance.GpuMode
        }, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        string str2 = instance.ThemeJson != null ? $"window.__THEME__ = {instance.ThemeJson};" : "";
        string documentCreatedAsync = await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($"window.__SETTINGS__ = {str1}; {str2}");
        _webView.CoreWebView2.WebMessageReceived += new EventHandler<CoreWebView2WebMessageReceivedEventArgs>(OnWebMessageReceived);
        _webView.CoreWebView2.NavigateToString(BuildHtml());
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json;
            try
            {
                json = e.TryGetWebMessageAsString() ?? e.WebMessageAsJson;
            }
            catch
            {
                json = e.WebMessageAsJson;
            }
            using (JsonDocument jsonDocument = JsonDocument.Parse(json))
            {
                JsonElement rootElement = jsonDocument.RootElement;
                string? str = rootElement.GetProperty("type").GetString();
                JsonElement jsonElement1;
                int int32 = rootElement.TryGetProperty("_callId", out jsonElement1) ? jsonElement1.GetInt32() : 0;
                if (str == null)
                    return;
                switch (str.Length)
                {
                    case 8:
                        if (str != "mouse-up")
                            break;
                        BeginInvoke((Action)(() => StopResize()));
                        break;
                    case 12:
                        if (str != "resize-start")
                            break;
                        JsonElement jsonElement2;
                        string dir = rootElement.TryGetProperty("data", out jsonElement2) ? jsonElement2.GetString() ?? "" : "";
                        BeginInvoke((Action)(() => StartResize(dir)));
                        break;
                    case 13:
                        switch (str[0])
                        {
                            case 'p':
                                JsonElement jsonElement3;
                                if (str != "preview-scale" || !rootElement.TryGetProperty("data", out jsonElement3))
                                    return;
                                JsonElement jsonElement4;
                                int num1 = jsonElement3.TryGetProperty("textScale", out jsonElement4) ? jsonElement4.GetInt32() : 100;
                                JsonElement jsonElement5;
                                int num2 = jsonElement3.TryGetProperty("fontScale", out jsonElement5) ? jsonElement5.GetInt32() : 100;
                                ScalePreview?.Invoke(num1, num2);
                                return;
                            case 's':
                                if (str != "settings-save")
                                    return;
                                try
                                {
                                    JsonElement jsonElement6;
                                    if (rootElement.TryGetProperty("data", out jsonElement6))
                                    {
                                        SettingsData? data = JsonSerializer.Deserialize<SettingsData>(jsonElement6.GetRawText(), new JsonSerializerOptions()
                                        {
                                            PropertyNameCaseInsensitive = true
                                        });
                                        if (data != null)
                                        {
                                            bool gpuModeChanged = AppSettings.Instance.GpuMode != data.GpuMode;
                                            ApplySettings(data);
                                            SettingsSaved?.Invoke(data);
                                            if (gpuModeChanged)
                                                GpuOptionChanged?.Invoke();
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                }
                                BeginInvoke((Action)(() => Close()));
                                return;
                            case 't':
                                JsonElement jsonElement7;
                                if (str != "theme-changed" || !rootElement.TryGetProperty("data", out jsonElement7))
                                    return;
                                ThemeChanged?.Invoke(jsonElement7.GetRawText());
                                return;
                            default:
                                return;
                        }
                        break;
                    case 14:
                        if (str != "settings-close")
                            break;
                        BeginInvoke((Action)(() => Close()));
                        break;
                    case 16:
                        if (str != "resume-shortcuts")
                            break;
                        ResumeShortcuts?.Invoke();
                        break;
                    case 17:
                        switch (str[1])
                        {
                            case 'e':
                                if (str != "send-error-report")
                                    return;
                                string? userMessage = null;
                                JsonElement jsonElement8;
                                if (rootElement.TryGetProperty("data", out jsonElement8) && jsonElement8.ValueKind == JsonValueKind.String)
                                    userMessage = jsonElement8.GetString();
                                ErrorReporter.SendErrorReport(userMessage);
                                return;
                            case 'o':
                                if (str != "copy-to-clipboard")
                                    return;
                                JsonElement jsonElement9;
                                string text = rootElement.TryGetProperty("data", out jsonElement9) ? jsonElement9.GetString() ?? "" : "";
                                if (string.IsNullOrEmpty(text))
                                    return;
                                BeginInvoke((Action)(() =>
                                {
                                    try
                                    {
                                        Clipboard.SetDataObject(text, true, 3, 200);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            Process.Start(new ProcessStartInfo(text)
                                            {
                                                UseShellExecute = true
                                            });
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }));
                                return;
                            case 'u':
                                if (str != "suspend-shortcuts")
                                    return;
                                SuspendShortcuts?.Invoke();
                                return;
                            default:
                                return;
                        }
                }
            }
        }
        catch
        {
        }
    }

    private static void ApplySettings(SettingsData data)
    {
        AppSettings instance = AppSettings.Instance;
        instance.Shortcuts.Refresh = data.Refresh;
        instance.Shortcuts.Toggle = data.Toggle;
        instance.Shortcuts.Compact = data.Compact;
        instance.Shortcuts.SwitchTab = data.SwitchTab;
        instance.OverlayOnlyWhenAion = data.OverlayOnly;
        instance.TextScale = data.TextScale;
        instance.FontScale = data.FontScale;
        if (data.TrackedSkills != null)
            instance.TrackedSkills = data.TrackedSkills;
        instance.KeepPartyOnRefresh = data.ShowParty;
        instance.KeepSelfOnRefresh = data.ShowSelf;
        instance.AutoTabSwitch = data.AutoTabSwitch;
        instance.DpsPercentMode = data.DpsPercentMode;
        instance.ScoreFormat = data.ScoreFormat;
        instance.DpsTimeMode = data.DpsTimeMode;
        instance.GpuMode = data.GpuMode;
        instance.Save();
    }

    private void Reply(int callId, object? value)
    {
        string json = JsonSerializer.Serialize(new
        {
            _responseId = callId,
            result = value
        });
        BeginInvoke((Action)(() => _webView.CoreWebView2.PostWebMessageAsJson(json)));
    }

    public void PositionNear(Form parent)
    {
        Rectangle workingArea = Screen.FromControl(parent).WorkingArea;
        int num1 = parent.Right + 10;
        int num2 = parent.Top;
        if (num1 + Width > workingArea.Right)
            num1 = parent.Left - Width - 10;
        if (num2 + Height > workingArea.Bottom)
            num2 = workingArea.Bottom - Height;
        Location = new Point(Math.Max(workingArea.Left, num1), Math.Max(workingArea.Top, num2));
    }

    private static string BuildHtml()
    {
        using (Stream? manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aion2DPSViewer.settings.html"))
        {
            if (manifestResourceStream == null)
                return "<html><body>settings.html not found</body></html>";
            using (StreamReader streamReader = new StreamReader(manifestResourceStream))
                return streamReader.ReadToEnd();
        }
    }
}
