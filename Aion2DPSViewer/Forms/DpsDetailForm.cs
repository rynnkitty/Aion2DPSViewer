using Aion2DPSViewer.Core;
using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.Globalization;
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
        await WebViewHelper.InitAsync(_webView, "A2Viewer_WebView2_Detail");
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
        return "<!DOCTYPE html>\r\n<html>\r\n<head>\r\n<meta charset=\"utf-8\">\r\n<style>\r\n@font-face { font-family: 'LINE Seed Sans KR'; font-weight: 400; src: url('https://a2viewer.local/fonts/LINESeedKR-Rg.woff2') format('woff2'); font-display: swap; }\r\n@font-face { font-family: 'LINE Seed Sans KR'; font-weight: 700; src: url('https://a2viewer.local/fonts/LINESeedKR-Bd.woff2') format('woff2'); font-display: swap; }\r\n@font-face { font-family: 'Orbit'; font-weight: 400; src: url('https://a2viewer.local/fonts/Orbit-Regular.woff2') format('woff2'); font-display: swap; }\r\n@font-face { font-family: 'Gmarket Sans'; font-weight: 500; src: url('https://a2viewer.local/fonts/GmarketSans-Medium.woff2') format('woff2'); font-display: swap; }\r\n@font-face { font-family: 'Gmarket Sans'; font-weight: 700; src: url('https://a2viewer.local/fonts/GmarketSans-Bold.woff2') format('woff2'); font-display: swap; }\r\n* { margin: 0; padding: 0; box-sizing: border-box; }\r\n:root {\r\n  --bg-app-rgb: 18, 22, 30;\r\n  --accent: #3498db;\r\n  --accent-rgb: 52, 152, 219;\r\n  --text-primary: #e0e0e0;\r\n  --text-secondary: #8899aa;\r\n  --text-muted: #556;\r\n  --gold: #ffd060;\r\n  --red: #e74c3c;\r\n  --red-rgb: 231, 76, 60;\r\n  --green: #2ecc71;\r\n  --orange: #f39c12;\r\n  --border-accent-rgb: 100, 120, 160;\r\n  --border-radius: 6px;\r\n}\r\nhtml { font-size: 11px; }\r\nbody {\r\n  font-family: 'Segoe UI', 'Malgun Gothic', sans-serif;\r\n  font-size: 1rem;\r\n  color: var(--text-primary);\r\n  background: transparent;\r\n  overflow: auto;\r\n  user-select: none;\r\n}\r\n.panel {\r\n  background: rgba(var(--bg-app-rgb), 0.95);\r\n  border: 1px solid rgba(var(--border-accent-rgb), 0.3);\r\n  border-radius: var(--border-radius);\r\n  padding: 8px;\r\n  height: 100vh;\r\n  display: flex;\r\n  flex-direction: column;\r\n}\r\n.header {\r\n  display: flex;\r\n  gap: 12px;\r\n  align-items: center;\r\n  padding-bottom: 6px;\r\n  border-bottom: 1px solid rgba(var(--border-accent-rgb), 0.2);\r\n  margin-bottom: 6px;\r\n}\r\n.header .name { font-weight: 600; font-size: 1.09rem; flex: 0 0 auto; }\r\nth.col-spec, td.col-spec { text-align: center; padding: 2px 4px; }\r\n.spec-boxes { display: flex; gap: 2px; justify-content: center; }\r\n.spec-box {\r\n  width: 7px; height: 7px; border-radius: 2px;\r\n  background: rgba(80, 90, 110, 0.5);\r\n  border: 1px solid rgba(var(--border-accent-rgb), 0.3);\r\n}\r\n.spec-box.active { background: var(--green); border-color: var(--green); box-shadow: 0 0 3px rgba(61, 220, 132, 0.4); }\r\n.spec-box.stigma { background: var(--gold); border-color: var(--gold); box-shadow: 0 0 3px rgba(240, 192, 64, 0.4); }\r\n.close-btn {\r\n  margin-left: auto;\r\n  background: rgba(255,255,255,0.08);\r\n  border: 1px solid rgba(255,255,255,0.12);\r\n  color: var(--text-secondary);\r\n  font-size: 1.18rem;\r\n  width: 1.82rem; height: 1.82rem;\r\n  border-radius: 4px;\r\n  cursor: pointer;\r\n  display: flex; align-items: center; justify-content: center;\r\n  line-height: 1;\r\n  flex-shrink: 0;\r\n}\r\n.close-btn:hover { background: rgba(var(--red-rgb),0.3); color: var(--red); border-color: rgba(var(--red-rgb),0.4); }\r\n.header .label { color: var(--text-muted); font-size: 0.91rem; }\r\n.header .total { color: var(--text-secondary); }\r\n.header .crit { color: var(--red); }\r\n.header .back { color: var(--orange); }\r\n.header .hard { color: #9b59b6; }\r\n.header .multi { color: #1abc9c; }\r\n.header .heal { color: var(--green); }\r\n.table-wrap {\r\n  flex: 1;\r\n  overflow-y: auto;\r\n  overflow-x: auto;\r\n}\r\ntable {\r\n  width: 100%;\r\n  border-collapse: collapse;\r\n  font-size: 0.95rem;\r\n}\r\nth {\r\n  position: sticky;\r\n  top: 0;\r\n  background: rgba(var(--bg-app-rgb), 0.98);\r\n  color: var(--text-secondary);\r\n  font-weight: 500;\r\n  text-align: right;\r\n  padding: 3px 4px;\r\n  white-space: nowrap;\r\n  border-bottom: 1px solid rgba(var(--border-accent-rgb), 0.2);\r\n}\r\nth.col-skill { text-align: left; min-width: 100px; }\r\ntd {\r\n  padding: 2px 4px;\r\n  text-align: right;\r\n  white-space: nowrap;\r\n  border-bottom: 1px solid rgba(var(--border-accent-rgb), 0.15);\r\n}\r\ntd.col-skill { text-align: left; }\r\n.col-crit { color: var(--red); }\r\n.col-back { color: var(--orange); }\r\n.col-total { color: var(--accent); font-weight: 600; }\r\n.skill-cell { position: relative; display: flex; align-items: center; gap: 3px; }\r\n.skill-icon { position: relative; z-index: 1; width: 18px; height: 18px; border-radius: 3px; flex-shrink: 0; }\r\n.skill-dot { position: relative; z-index: 1; width: 8px; height: 8px; border-radius: 2px; flex-shrink: 0; }\r\n.skill-bar-bg {\r\n  position: absolute; inset: 0;\r\n  background: rgba(40, 50, 70, 0.5);\r\n  border-radius: 2px;\r\n  overflow: hidden;\r\n}\r\n.skill-bar-fill { height: 100%; border-radius: 2px; opacity: 0.35; }\r\n.skill-name {\r\n  position: relative; z-index: 1;\r\n  overflow: hidden; text-overflow: ellipsis;\r\n  padding: 1px 2px; flex: 1; min-width: 0;\r\n}\r\n.table-wrap::-webkit-scrollbar { width: 4px; }\r\n.table-wrap::-webkit-scrollbar-track { background: transparent; }\r\n.table-wrap::-webkit-scrollbar-thumb { background: rgba(var(--border-accent-rgb),0.3); border-radius: 2px; }\r\n.empty { display: flex; align-items: center; justify-content: center; height: 100%; color: var(--text-muted); }\r\n.buff-section { margin-top: 6px; padding-top: 6px; border-top: 1px solid rgba(var(--border-accent-rgb), 0.2); }\r\n.buff-title { color: var(--text-secondary); font-size: 0.91rem; font-weight: 500; margin-bottom: 4px; }\r\n.buff-list { display: flex; flex-direction: column; gap: 2px; }\r\n.buff-row { display: flex; align-items: center; gap: 6px; font-size: 0.91rem; }\r\n.buff-icon { width: 16px; height: 16px; border-radius: 3px; flex-shrink: 0; }\r\n.buff-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-primary); }\r\n.buff-pct { color: var(--text-secondary); width: 42px; text-align: right; flex-shrink: 0; }\r\n.buff-bar-bg { flex: 0 0 80px; height: 6px; background: rgba(40, 50, 70, 0.5); border-radius: 3px; overflow: hidden; }\r\n.buff-bar-fill { height: 100%; border-radius: 3px; background: var(--accent); opacity: 0.7; }\r\n</style>\r\n</head>\r\n<body>\r\n<div class=\"panel\" id=\"panel\">\r\n  <div class=\"empty\" id=\"empty\">데이터 없음</div>\r\n</div>\r\n<script>\r\nfunction fmt(n) { return n.toLocaleString('ko-KR'); }\r\nfunction pct(count, total) {\r\n  return total > 0 ? (count / total * 100).toFixed(1) : '0.0';\r\n}\r\n\r\nconst JOB_COLORS = {\r\n  0:'#e74c3c',1:'#e67e22',2:'#9b59b6',3:'#2ecc71',\r\n  4:'#3498db',5:'#1abc9c',6:'#f1c40f',7:'#e84393'\r\n};\r\n\r\n// 엣지 리사이즈 감지\r\nconst EDGE = 6;\r\nlet _resizing = false;\r\ndocument.addEventListener('mousedown', function(e) {\r\n  const w = document.documentElement.clientWidth;\r\n  const h = document.documentElement.clientHeight;\r\n  const dirs = [];\r\n  if (e.clientX < EDGE) dirs.push('left');\r\n  if (e.clientX > w - EDGE) dirs.push('right');\r\n  if (e.clientY < EDGE) dirs.push('top');\r\n  if (e.clientY > h - EDGE) dirs.push('bottom');\r\n  if (dirs.length > 0) {\r\n    e.preventDefault();\r\n    _resizing = true;\r\n    window.chrome.webview.postMessage({type:'resize-start', dir:dirs.join('-')});\r\n  }\r\n});\r\nwindow.addEventListener('mouseup', function() {\r\n  if (_resizing) {\r\n    _resizing = false;\r\n    window.chrome.webview.postMessage({type:'mouse-up'});\r\n  }\r\n});\r\ndocument.addEventListener('mousemove', function(e) {\r\n  if (_resizing) return;\r\n  const w = document.documentElement.clientWidth;\r\n  const h = document.documentElement.clientHeight;\r\n  const l = e.clientX < EDGE, r = e.clientX > w - EDGE;\r\n  const t = e.clientY < EDGE, b = e.clientY > h - EDGE;\r\n  let c = '';\r\n  if ((t&&l)||(b&&r)) c='nwse-resize';\r\n  else if ((t&&r)||(b&&l)) c='nesw-resize';\r\n  else if (l||r) c='ew-resize';\r\n  else if (t||b) c='ns-resize';\r\n  document.body.style.cursor = c;\r\n});\r\n\r\nfunction updateData(jsonStr) {\r\n  try {\r\n    const d = JSON.parse(jsonStr);\r\n    if (!d || !d.topSkills || d.topSkills.length === 0) {\r\n      document.getElementById('panel').innerHTML = '<div class=\"empty\">데이터 없음</div>';\r\n      return;\r\n    }\r\n    const color = JOB_COLORS[d.jobCode] || '#95a5a6';\r\n    const totalHits = d.topSkills.reduce((a, s) => a + s.hitCount, 0);\r\n    const totalBack = d.topSkills.reduce((a, s) => a + s.backCount, 0);\r\n    const totalHard = d.topSkills.reduce((a, s) => a + s.hardHitCount, 0);\r\n    const totalMulti = d.topSkills.reduce((a, s) => a + s.multiHitCount, 0);\r\n    let html = '<div class=\"header\">';\r\n    html += '<span class=\"name\">' + esc(d.maskNick ? maskNm(d.name) : d.name) + '</span>';\r\n    html += '<span class=\"label\">누적피해:</span><span class=\"total\">' + fmt(d.totalDamage) + '</span>';\r\n    html += '<span class=\"crit\">치명타: ' + d.critRate.toFixed(1) + '%</span>';\r\n    html += '<span class=\"hard\">강타: ' + pct(totalHard, totalHits) + '%</span>';\r\n    html += '<span class=\"multi\">다단: ' + pct(totalMulti, totalHits) + '%</span>';\r\n    html += '<span class=\"back\">후방: ' + pct(totalBack, totalHits) + '%</span>';\r\n    if (d.healTotal > 0) html += '<span class=\"heal\">힐: ' + fmt(d.healTotal) + '</span>';\r\n    html += '<div class=\"close-btn\" onclick=\"window.chrome.webview.postMessage({type:\\'close\\'})\">&times;</div>';\r\n    html += '</div>';\r\n    html += '<div class=\"table-wrap\"><table><thead><tr>';\r\n    html += '<th class=\"col-skill\">스킬</th><th class=\"col-spec\">특화</th><th>타수</th><th>일반</th><th>치명타</th>';\r\n    html += '<th>후방</th><th>강타</th><th>완벽</th><th>다단</th>';\r\n    html += '<th>회피</th><th>막기</th><th>초당</th><th>평균</th>';\r\n    html += '<th>최소</th><th>최대</th><th class=\"col-total\">피해</th>';\r\n    html += '</tr></thead><tbody>';\r\n    for (const s of d.topSkills) {\r\n      html += '<tr>';\r\n      html += '<td class=\"col-skill\"><div class=\"skill-cell\"><div class=\"skill-bar-bg\"><div class=\"skill-bar-fill\" style=\"width:' + s.percent.toFixed(1) + '%;background:' + color + '\"></div></div>';\r\n      if (s.iconUrl) html += '<img class=\"skill-icon\" src=\"' + esc(s.iconUrl) + '\" onerror=\"this.style.display=\\'none\\'\" />';\r\n      else html += '<span class=\"skill-dot\" style=\"background:' + color + '\"></span>';\r\n      html += '<span class=\"skill-name\">' + esc(s.name) + '</span></div></td>';\r\n      html += '<td class=\"col-spec\">';\r\n      if (s.specs && s.specs.length > 0) {\r\n        var isStigma = s.skillType === 'stigma';\r\n        var maxBox = isStigma ? 4 : 5;\r\n        var maxSpec = isStigma ? Math.max.apply(null, s.specs) : 0;\r\n        html += '<div class=\"spec-boxes\">';\r\n        for (var si = 1; si <= maxBox; si++) {\r\n          var on = isStigma ? (si <= maxSpec) : (s.specs.indexOf(si) >= 0);\r\n          html += '<span class=\"spec-box' + (on ? (isStigma ? ' stigma' : ' active') : '') + '\"></span>';\r\n        }\r\n        html += '</div>';\r\n      }\r\n      html += '</td>';\r\n      html += '<td>' + s.hitCount + '</td>';\r\n      html += '<td>' + pct(s.normalCount, s.hitCount) + '%</td>';\r\n      html += '<td class=\"col-crit\">' + pct(s.critCount, s.hitCount) + '%</td>';\r\n      html += '<td class=\"col-back\">' + pct(s.backCount, s.hitCount) + '%</td>';\r\n      html += '<td>' + pct(s.hardHitCount, s.hitCount) + '%</td>';\r\n      html += '<td>' + pct(s.perfectCount, s.hitCount) + '%</td>';\r\n      html += '<td>' + pct(s.multiHitCount, s.hitCount) + '%</td>';\r\n      html += '<td>' + pct(s.evadeCount, s.hitCount) + '%</td>';\r\n      html += '<td>' + pct(s.blockCount, s.hitCount) + '%</td>';\r\n      html += '<td>' + fmt(s.dps) + '</td>';\r\n      html += '<td>' + fmt(s.avgDamage) + '</td>';\r\n      html += '<td>' + fmt(s.minDamage) + '</td>';\r\n      html += '<td>' + fmt(s.maxDamage) + '</td>';\r\n      html += '<td class=\"col-total\">' + fmt(s.totalDamage) + ' (' + s.percent.toFixed(1) + '%)</td>';\r\n      html += '</tr>';\r\n    }\r\n    html += '</tbody></table>';\r\n    if (d.buffUptime && d.buffUptime.length > 0) {\r\n      html += '<div class=\"buff-section\"><div class=\"buff-title\">버프 업타임</div><div class=\"buff-list\">';\r\n      for (const b of d.buffUptime.slice(0, 15)) {\r\n        const p = b.uptimePercent.toFixed(1);\r\n        const sec = b.uptimeSeconds.toFixed(0);\r\n        html += '<div class=\"buff-row\">';\r\n        if (b.iconUrl) html += '<img class=\"buff-icon\" src=\"' + esc(b.iconUrl) + '\" onerror=\"this.style.display=\\'none\\'\" />';\r\n        html += '<span class=\"buff-name\">' + esc(b.name) + '</span>';\r\n        html += '<span class=\"buff-pct\">' + p + '%</span>';\r\n        html += '<div class=\"buff-bar-bg\"><div class=\"buff-bar-fill\" style=\"width:' + Math.min(100, b.uptimePercent).toFixed(1) + '%\"></div></div>';\r\n        html += '<span style=\"color:#667;font-size:0.82rem\">' + sec + 's</span>';\r\n        html += '</div>';\r\n      }\r\n      html += '</div></div>';\r\n    }\r\n    html += '</div>';\r\n    document.getElementById('panel').innerHTML = html;\r\n  } catch(e) { console.error(e); }\r\n}\r\n\r\nfunction esc(s) {\r\n  const d = document.createElement('div');\r\n  d.textContent = s;\r\n  return d.innerHTML;\r\n}\r\n\r\nfunction maskNm(name) {\r\n  if (!name) return name;\r\n  const m = name.match(/^(.+?)(\\[.+\\])$/);\r\n  const nick = m ? m[1] : name;\r\n  if (nick.length <= 1) return '*';\r\n  if (nick.length === 2) return nick[0] + '*';\r\n  return nick[0] + '*'.repeat(nick.length - 1);\r\n}\r\n\r\n// 테마 프리셋 정의 (오버레이와 동일)\r\nconst THEME_PRESETS = {\r\n  midnight: {'--bg-app-rgb':'18, 22, 30','--accent':'#3498db','--accent-rgb':'52, 152, 219','--text-primary':'#e0e0e0','--text-secondary':'#8899aa','--text-muted':'#556','--gold':'#ffd060','--red':'#e74c3c','--red-rgb':'231, 76, 60','--green':'#2ecc71','--orange':'#f39c12','--border-accent-rgb':'100, 120, 160','--border-radius':'6px'},\r\n  abyss: {'--bg-app-rgb':'6, 5, 16','--accent':'#9b7aff','--accent-rgb':'155, 122, 255','--text-primary':'#d8d0e8','--text-secondary':'#8878aa','--text-muted':'#504568','--gold':'#e0c060','--red':'#d04050','--red-rgb':'208, 64, 80','--green':'#60c890','--orange':'#d0a020','--border-accent-rgb':'120, 100, 220','--border-radius':'5px'},\r\n  deva: {'--bg-app-rgb':'8, 14, 24','--accent':'#60c0ff','--accent-rgb':'96, 192, 255','--text-primary':'#d0e0f0','--text-secondary':'#7090b0','--text-muted':'#405060','--gold':'#f0d060','--red':'#e05050','--red-rgb':'224, 80, 80','--green':'#50c880','--orange':'#e0a030','--border-accent-rgb':'80, 150, 240','--border-radius':'5px'},\r\n  asmodian: {'--bg-app-rgb':'16, 8, 14','--accent':'#c070dd','--accent-rgb':'192, 112, 221','--text-primary':'#e0d0e0','--text-secondary':'#a080a0','--text-muted':'#604860','--gold':'#e0c060','--red':'#e04050','--red-rgb':'224, 64, 80','--green':'#60c880','--orange':'#d09030','--border-accent-rgb':'160, 100, 180','--border-radius':'5px'},\r\n  flame: {'--bg-app-rgb':'16, 8, 6','--accent':'#e08040','--accent-rgb':'224, 128, 64','--text-primary':'#f0e0d0','--text-secondary':'#a08070','--text-muted':'#604840','--gold':'#ffc040','--red':'#e05040','--red-rgb':'224, 80, 64','--green':'#70c070','--orange':'#f0a030','--border-accent-rgb':'200, 120, 60','--border-radius':'5px'},\r\n  glass: {'--bg-app-rgb':'0, 0, 0','--accent':'#7ab4ff','--accent-rgb':'122, 180, 255','--text-primary':'#ffffff','--text-secondary':'#a0a8b8','--text-muted':'#606878','--gold':'#ffd060','--red':'#f05050','--red-rgb':'240, 80, 80','--green':'#60d080','--orange':'#f0a000','--border-accent-rgb':'100, 160, 255','--border-radius':'8px'},\r\n};\r\n\r\nfunction applyTheme(jsonStr) {\r\n  try {\r\n    const t = JSON.parse(jsonStr);\r\n    const base = THEME_PRESETS[t.preset] || THEME_PRESETS.midnight;\r\n    const vars = Object.assign({}, base, t.customVars || {});\r\n    const root = document.documentElement;\r\n    for (const [k, v] of Object.entries(vars)) {\r\n      root.style.setProperty(k, v);\r\n    }\r\n    if (t.fontFamily) document.body.style.fontFamily = t.fontFamily;\r\n  } catch(e) { console.error('applyTheme', e); }\r\n}\r\n</script>\r\n</body>\r\n</html>";
    }
}
