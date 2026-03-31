using Aion2DPSViewer.Core;
using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aion2DPSViewer.Forms;

public class DpsDetailForm : Form
{
    private Microsoft.Web.WebView2.WinForms.WebView2 _webView;
    private bool _ready;
    private string? _pendingData;
    private string? _pendingTheme;
    private bool _resizing;
    private string _resizeDir = "";
    private Point _resizeCursorStart;
    private Rectangle _resizeBoundsStart;
    private Timer _moveTimer;
    private Timer? _saveDebounce;
    private int _uiScale = 100;
    private int _fontScale = 100;

    public DpsDetailForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(400, 200);
        AppSettings instance = AppSettings.Instance;
        Size = new Size(instance.DetailPanelWidth, instance.DetailPanelHeight);
        BackColor = Color.FromArgb(1, 0, 1);
        _moveTimer = new Timer() { Interval = 16 };
        _moveTimer.Tick += new EventHandler(MoveTimer_Tick);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams createParams = base.CreateParams;
            createParams.ExStyle |= 0x08000080;
            return createParams;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 20)
        {
            m.Result = new IntPtr(1);
        }
        else
        {
            base.WndProc(ref m);
        }
    }

    public async Task InitAsync()
    {
        Microsoft.Web.WebView2.WinForms.WebView2 webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
        webView2.Dock = DockStyle.Fill;
        webView2.DefaultBackgroundColor = Color.Transparent;
        _webView = webView2;
        Controls.Add(_webView);
        await WebViewHelper.InitAsync(_webView, "Aion2Info_WebView2_Detail");
        EmbeddedWebServer.Setup(_webView.CoreWebView2);
        _webView.CoreWebView2.NavigateToString(GetHtml());
        _webView.CoreWebView2.WebMessageReceived += new EventHandler<CoreWebView2WebMessageReceivedEventArgs>(WebView_WebMessageReceived);
        _webView.CoreWebView2.NavigationCompleted += new EventHandler<CoreWebView2NavigationCompletedEventArgs>(WebView_NavigationCompleted);
    }

    private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg;
        try { msg = e.TryGetWebMessageAsString() ?? e.WebMessageAsJson; }
        catch { msg = e.WebMessageAsJson; }
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(msg);
            string? type = node?["type"]?.GetValue<string>();
            if (type == "resize-start")
            {
                string dir = node?["dir"]?.GetValue<string>() ?? "";
                BeginInvoke((Action)(() => StartResize(dir)));
            }
            else if (type == "mouse-up")
            {
                BeginInvoke((Action)(() => StopResize()));
            }
            else if (type == "close")
            {
                BeginInvoke((Action)(() => Hide()));
            }
        }
        catch { }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _ready = true;
        if (_pendingTheme != null)
        {
            PostData("applyTheme", _pendingTheme);
            _pendingTheme = null;
        }
        if (_pendingData != null)
        {
            PostData("updateData", _pendingData);
            _pendingData = null;
        }
        ApplyFontSize();
    }

    private void ApplyLayered() => Win32Native.ApplyLayered(Handle);

    private void StartResize(string direction)
    {
        _resizing = true;
        _resizeDir = direction;
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
        SaveSize();
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

    private void SaveSize()
    {
        _saveDebounce?.Stop();
        if (_saveDebounce == null)
        {
            _saveDebounce = new Timer() { Interval = 500 };
            _saveDebounce.Tick += new EventHandler(SaveDebounce_Tick);
        }
        _saveDebounce.Start();
    }

    private void SaveDebounce_Tick(object? sender, EventArgs e)
    {
        _saveDebounce?.Stop();
        AppSettings instance = AppSettings.Instance;
        instance.DetailPanelWidth = Width;
        instance.DetailPanelHeight = Height;
        instance.Save();
    }

    public void UpdateScale(int scalePercent)
    {
        _uiScale = scalePercent;
        ApplyFontSize();
    }

    public void UpdateFontScale(int scalePercent)
    {
        _fontScale = scalePercent;
        ApplyFontSize();
    }

    private void ApplyFontSize()
    {
        if (_webView?.CoreWebView2 == null || !_ready)
            return;
        string factor = ((double)_uiScale / 100.0 * (double)_fontScale / 100.0).ToString("F2", CultureInfo.InvariantCulture);
        BeginInvoke((Action)(() => _webView.CoreWebView2.ExecuteScriptAsync($"document.documentElement.style.fontSize = 'calc(11px * {factor})'")));
    }

    public void ApplyTheme(string themeJson)
    {
        if (_ready)
            PostData("applyTheme", themeJson);
        else
            _pendingTheme = themeJson;
    }

    public void UpdateData(string json)
    {
        if (_ready)
            PostData("updateData", json);
        else
            _pendingData = json;
    }

    private void PostData(string func, string json)
    {
        if (_webView?.CoreWebView2 == null)
            return;
        string escaped = json.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        BeginInvoke((Action)(() => _webView.CoreWebView2.ExecuteScriptAsync($"{func}('{escaped}')")));
    }

    public void PositionNear(Form parent)
    {
        Rectangle workingArea = Screen.FromControl(parent).WorkingArea;
        int num1 = parent.Right + 4;
        int num2 = parent.Top;
        if (num1 + Width > workingArea.Right)
            num1 = parent.Left - Width - 4;
        if (num2 + Height > workingArea.Bottom)
            num2 = workingArea.Bottom - Height;
        Location = new Point(Math.Max(workingArea.Left, num1), Math.Max(workingArea.Top, num2));
    }

    private static string GetHtml()
    {
        using Stream? s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aion2DPSViewer.detail.html");
        if (s == null) return "<html><body style='color:red'>detail.html not found</body></html>";
        using StreamReader r = new StreamReader(s, System.Text.Encoding.UTF8);
        return r.ReadToEnd();
    }
}
