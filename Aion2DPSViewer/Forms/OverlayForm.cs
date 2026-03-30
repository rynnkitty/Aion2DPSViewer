using Aion2DPSViewer.Api;
using Aion2DPSViewer.Core;
using Aion2DPSViewer.Dps;
using Aion2DPSViewer.Packet;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aion2DPSViewer.Forms;

public class OverlayForm : Form
{
    private Microsoft.Web.WebView2.WinForms.WebView2 _webView;
    private WebBridge _bridge;
    private PacketSniffer? _sniffer;
    private DpsMeter? _dpsMeter;
    private TrayManager _tray;
    private HotkeyManager _hotkeys;
    private ForegroundWatcher _fgWatcher;
    private bool _dragging;
    private Point _dragCursorStart;
    private Point _dragFormStart;
    private bool _resizing;
    private string _resizeDir = "";
    private Point _resizeCursorStart;
    private Rectangle _resizeBoundsStart;
    private System.Windows.Forms.Timer _moveTimer;
    private System.Windows.Forms.Timer _hitTestTimer;
    private bool _manuallyHidden;
    private bool _initialShowDone;
    private System.Windows.Forms.Timer? _uiReadyFallback;
    private readonly PartyTracker _partyTracker = new PartyTracker();
    private CharacterService? _charService;
    private DpsDetailForm? _dpsDetailForm;
    private bool _dpsDetailPinned;
    private int _dpsDetailPinnedEntity;
    private bool _dpsDetailMaskNick;
    private System.Windows.Forms.Timer? _saveDebounce;
    private const int TITLEBAR_HEIGHT = 30;
    private const int RESIZE_EDGE = 6;
    private bool _forceInteractive;
    private bool _isCompact;
    private bool _dpsTabActive;
    private bool _clickLocked;
    private SettingsForm? _settingsForm;
    private const int WM_NCHITTEST = 132;
    private const int HTTRANSPARENT = -1;
    private const int WM_ERASEBKGND = 20;
    private const int WM_HOTKEY = 786;

    public OverlayForm()
    {
        Aion2DPSViewer.Core.WindowState windowState = AppSettings.Instance.WindowState;
        Text = "A2Viewer";
        Size = new Size(windowState.Width, windowState.Height);
        Location = new Point(windowState.X, windowState.Y);
        MinimumSize = new Size(300, 200);
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(1, 0, 1);
        _moveTimer = new System.Windows.Forms.Timer() { Interval = 16 };
        _moveTimer.Tick += new EventHandler(MoveTimer_Tick);
        _hitTestTimer = new System.Windows.Forms.Timer() { Interval = 50 };
        _hitTestTimer.Tick += new EventHandler(HitTestTimer_Tick);
        _hitTestTimer.Start();
        Load += new EventHandler(OverlayForm_Load);
        FormClosing += new FormClosingEventHandler(OverlayForm_FormClosing);
        Move += new EventHandler(OverlayForm_Move);
        Resize += new EventHandler(OverlayForm_Resize);
    }

    private async void OverlayForm_Load(object? sender, EventArgs e)
    {
        try
        {
            await InitAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[init] 초기화 실패: {ex}");
            MessageBox.Show("초기화 실패:\n" + ex.Message, "A2Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OverlayForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        Cleanup();
    }

    private void OverlayForm_Move(object? sender, EventArgs e)
    {
        SaveWindowState();
        UpdateDpsDetailPosition();
        UpdateSettingsPosition();
    }

    private void OverlayForm_Resize(object? sender, EventArgs e)
    {
        SaveWindowState();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams createParams = base.CreateParams;
            createParams.ExStyle |= 0x08000080;
            return createParams;
        }
    }

    protected override bool ShowWithoutActivation => true;

    private async Task InitAsync()
    {
        Microsoft.Web.WebView2.WinForms.WebView2 webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
        webView2.Dock = DockStyle.Fill;
        webView2.DefaultBackgroundColor = Color.Transparent;
        _webView = webView2;
        Controls.Add(_webView);
        string browserArgs = "--autoplay-policy=no-user-gesture-required";
        string gpuMode = AppSettings.Instance.GpuMode;
        if (gpuMode == "off")
            browserArgs += " --disable-gpu";
        else if (gpuMode == "compositing-off")
            browserArgs += " --disable-gpu-compositing";
        await WebViewHelper.InitAsync(_webView, "A2Viewer_WebView2", browserArgs);
        EmbeddedWebServer.Setup(_webView.CoreWebView2);
        AppSettings instance = AppSettings.Instance;
        string str1 = JsonSerializer.Serialize(new
        {
            version = typeof(OverlayForm).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            opacity = instance.Opacity,
            shortcuts = instance.Shortcuts,
            overlayOnly = instance.OverlayOnlyWhenAion,
            textScale = instance.TextScale,
            fontScale = instance.FontScale,
            trackedSkills = instance.TrackedSkills,
            knownDpSkills = instance.KnownDpSkills,
            keepPartyOnRefresh = instance.KeepPartyOnRefresh,
            keepSelfOnRefresh = instance.KeepSelfOnRefresh,
            autoTabSwitch = instance.AutoTabSwitch
        }, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        string str2 = instance.ThemeJson != null ? $"window.__THEME__ = {instance.ThemeJson};" : "";
        string documentCreatedAsync = await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($"window.__SETTINGS__ = {str1}; {str2}");
        _bridge = new WebBridge(this, _webView);
        _webView.CoreWebView2.WebMessageReceived += new EventHandler<CoreWebView2WebMessageReceivedEventArgs>(_bridge.OnWebMessage);
        _bridge.TextScaleChanged += scalePercent =>
        {
            _dpsDetailForm?.UpdateScale(scalePercent);
        };
        _bridge.FontScaleChanged += fontPercent =>
        {
            _dpsDetailForm?.UpdateFontScale(fontPercent);
        };
        _charService = new CharacterService(
            (type, data) => _bridge?.SendToJs(type, data),
            action => BeginInvoke(action),
            () => _partyTracker.Epoch);
        _webView.CoreWebView2.NavigateToString(BuildOverlayHtml());
        _uiReadyFallback = new System.Windows.Forms.Timer() { Interval = 3000 };
        _uiReadyFallback.Tick += new EventHandler(UiReadyFallback_Tick);
        _uiReadyFallback.Start();
        _tray = new TrayManager(this);
        _hotkeys = new HotkeyManager(this);
        _hotkeys.RegisterFromSettings();
        _fgWatcher = new ForegroundWatcher("aion2");
        _fgWatcher.ActiveChanged += new Action<bool>(OnForegroundChanged);
        _fgWatcher.Start();
        StartSnifferAsync();
    }

    private void UiReadyFallback_Tick(object? sender, EventArgs e)
    {
        _uiReadyFallback?.Stop();
        if (!_initialShowDone)
            OnUiReady();
    }

    public void OnUiReady()
    {
        _uiReadyFallback?.Stop();
        if (_initialShowDone)
            return;
        _initialShowDone = true;
        if (AppSettings.Instance.OverlayOnlyWhenAion && !_fgWatcher.CheckNow())
            return;
        ApplyLayered();
        Show();
        TopMost = false;
        TopMost = true;
    }

    public void HideOverlay()
    {
        Hide();
        if (_settingsForm == null || _settingsForm.IsDisposed || !_settingsForm.Visible || _settingsForm.ContainsFocus)
            return;
        _settingsForm.Hide();
    }

    public void ShowOverlay()
    {
        _manuallyHidden = false;
        Show();
        BringToFront();
        if (_settingsForm == null || _settingsForm.IsDisposed || _settingsForm.Visible)
            return;
        _settingsForm.Show();
    }

    private void OnForegroundChanged(bool active)
    {
        if (!AppSettings.Instance.OverlayOnlyWhenAion)
            return;
        BeginInvoke((Action)(() =>
        {
            if (active && !_manuallyHidden)
            {
                ShowOverlay();
            }
            else
            {
                if (active)
                    return;
                HideOverlay();
            }
        }));
    }

    public void ToggleVisibility()
    {
        if (Visible)
        {
            _manuallyHidden = true;
            HideOverlay();
        }
        else
        {
            _manuallyHidden = false;
            ShowOverlay();
        }
    }

    public void ToggleCompact() => _bridge?.SendToJs("toggle-compact", null);

    public void TriggerClearShortcut()
    {
        _charService?.ClearCache();
        _bridge?.SendToJs("clear-shortcut", null);
    }

    public void TriggerSwitchTab() => _bridge?.SendToJs("switch-tab", null);

    public void StartDrag()
    {
        _dragging = true;
        _resizing = false;
        _dragCursorStart = Cursor.Position;
        _dragFormStart = Location;
        _moveTimer.Start();
    }

    public void StartResize(string direction)
    {
        _resizing = true;
        _dragging = false;
        _resizeDir = direction;
        _resizeCursorStart = Cursor.Position;
        _resizeBoundsStart = Bounds;
        _moveTimer.Start();
    }

    public void StopMoveResize()
    {
        bool wasResizing = _resizing;
        _dragging = false;
        _resizing = false;
        _moveTimer.Stop();
        if (!wasResizing || _webView == null)
            return;
        _webView.DefaultBackgroundColor = Color.Transparent;
        _webView.Visible = false;
        BeginInvoke((Action)(() =>
        {
            if (_webView == null)
                return;
            _webView.Visible = true;
            _webView.Invalidate();
        }));
    }

    private void MoveTimer_Tick(object? sender, EventArgs e)
    {
        if ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.None)
        {
            StopMoveResize();
        }
        else
        {
            Point position = Cursor.Position;
            if (_dragging)
                Location = new Point(_dragFormStart.X + position.X - _dragCursorStart.X, _dragFormStart.Y + position.Y - _dragCursorStart.Y);
            else if (_resizing)
            {
                int num1 = position.X - _resizeCursorStart.X;
                int num2 = position.Y - _resizeCursorStart.Y;
                Rectangle resizeBoundsStart = _resizeBoundsStart;
                int num3 = resizeBoundsStart.X;
                int num4 = resizeBoundsStart.Y;
                int num5 = resizeBoundsStart.Width;
                int num6 = resizeBoundsStart.Height;
                Size minimumSize;
                if (_resizeDir.Contains("right"))
                {
                    minimumSize = MinimumSize;
                    num5 = Math.Max(minimumSize.Width, resizeBoundsStart.Width + num1);
                }
                if (_resizeDir.Contains("left"))
                {
                    minimumSize = MinimumSize;
                    num5 = Math.Max(minimumSize.Width, resizeBoundsStart.Width - num1);
                    num3 = resizeBoundsStart.X + resizeBoundsStart.Width - num5;
                }
                if (_resizeDir.Contains("bottom"))
                {
                    minimumSize = MinimumSize;
                    num6 = Math.Max(minimumSize.Height, resizeBoundsStart.Height + num2);
                }
                if (_resizeDir.Contains("top"))
                {
                    minimumSize = MinimumSize;
                    num6 = Math.Max(minimumSize.Height, resizeBoundsStart.Height - num2);
                    num4 = resizeBoundsStart.Y + resizeBoundsStart.Height - num6;
                }
                if (_resizeDir.Contains("left") || _resizeDir.Contains("top"))
                {
                    Win32Native.SetWindowPos(Handle, IntPtr.Zero, num3, num4, num5, num6, 28U);
                    Invalidate(true);
                }
                else
                    SetBounds(num3, num4, num5, num6);
            }
            else
                _moveTimer.Stop();
        }
    }

    private void SaveWindowState()
    {
        if (!_initialShowDone || !Visible)
            return;
        _saveDebounce?.Stop();
        if (_saveDebounce == null)
        {
            _saveDebounce = new System.Windows.Forms.Timer() { Interval = 500 };
            _saveDebounce.Tick += new EventHandler(SaveDebounce_Tick);
        }
        _saveDebounce.Start();
    }

    private void SaveDebounce_Tick(object? sender, EventArgs e)
    {
        _saveDebounce?.Stop();
        AppSettings.Instance.WindowState = new Aion2DPSViewer.Core.WindowState()
        {
            X = Location.X,
            Y = Location.Y,
            Width = Width,
            Height = Height
        };
        AppSettings.Instance.Save();
    }

    private async void StartSnifferAsync()
    {
        _sniffer?.Stop();
        _sniffer = new PacketSniffer();
        SetupSnifferEvents(_sniffer);
        StartDpsMeter(_sniffer);
        try
        {
            await _sniffer.StartAsync();
            Console.Error.WriteLine("[app] 스니퍼 시작 성공");
            _bridge?.SendToJs("preload-done", null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[app] 스니퍼 시작 실패: " + ex.ToString());
            _bridge?.SendToJs("sniffer-error", "스니퍼 시작 실패 — Npcap 설치를 확인하세요.");
        }
    }

    private void SendCharacterLoading(
        string nickname,
        int? serverId,
        string? serverName,
        string type)
    {
        _charService?.SendCharacterLoading(nickname, serverId, serverName, type);
    }

    private void SetSelf(string nickname, int? serverId, string? serverName)
    {
        _partyTracker.SetSelf(nickname, serverId, serverName);
    }

    private void OnPartyDisbanded()
    {
        _partyTracker.Disband();
        _charService?.ClearCache();
        _dpsMeter?.ClearDungeonId();
        _bridge?.SendToJs("party-clear", null);
        _bridge?.SendToJs("status", (object)new
        {
            type = "active",
            text = "파티 해산"
        });
    }

    private void SetupSnifferEvents(PacketSniffer s)
    {
        s.PartyList += members => BeginInvoke((Action)(() =>
        {
            _bridge?.SendToJs("party-sync", _partyTracker.OnPartySync(members));
            _bridge?.SendToJs("status", (object)new
            {
                type = "active",
                text = $"파티 {members.Count}명"
            });
            foreach (PartyMember member in members)
                Console.Error.WriteLine($"[party] 목록: {member.Nickname} CP={member.CombatPower} (패킷)");
        }));
        s.PartyUpdate += members => BeginInvoke((Action)(() =>
        {
            List<PartyMember> newMembers = new List<PartyMember>();
            _bridge?.SendToJs("party-sync", _partyTracker.OnPartyUpdate(members, out newMembers));
            foreach (PartyMember partyMember in newMembers)
            {
                Console.Error.WriteLine($"[party] 신규: {partyMember.Nickname} CP={partyMember.CombatPower} (패킷) → API 조회");
                SendCharacterLoading(partyMember.Nickname, partyMember.ServerId, partyMember.ServerName, "party");
            }
        }));
        s.PartyRequest += m => BeginInvoke((Action)(() => SendCharacterLoading(m.Nickname, m.ServerId, m.ServerName, "party_request")));
        s.PartyAccept += m => BeginInvoke((Action)(() => SendCharacterLoading(m.Nickname, m.ServerId, m.ServerName, "party_accept")));
        s.PartyLeft += () => BeginInvoke(new Action(OnPartyDisbanded));
        s.PartyEjected += () => BeginInvoke(new Action(OnPartyDisbanded));
        s.DungeonDetected += (id, stage) => BeginInvoke((Action)(() =>
        {
            if (id == 0)
            {
                _dpsMeter?.ClearDungeonId();
            }
            else
            {
                _bridge?.SendToJs("status", (object)new
                {
                    type = "active",
                    text = DungeonMap.GetName(id)
                });
                _dpsMeter?.SetDungeonId(id);
            }
        }));
        s.PingUpdated += ping => BeginInvoke((Action)(() => _bridge?.SendToJs("ping-update", (object)ping)));
        s.ServerPortReset += () => BeginInvoke((Action)(() =>
        {
            Console.Error.WriteLine("[app] 서버 포트 리셋 — 재감지 대기");
            _bridge?.SendToJs("ping-update", (object)-1);
            _bridge?.SendToJs("sniffer-error", "연결 대기 중...");
        }));
    }

    public void SetDpsTabActive(bool active) => _dpsTabActive = active;

    public void SetClickLocked(bool locked) => _clickLocked = locked;

    private async void EnsureDpsDetailForm()
    {
        if (_dpsDetailForm != null && !_dpsDetailForm.IsDisposed)
            return;
        _dpsDetailForm = new DpsDetailForm();
        _dpsDetailForm.FormClosing += new FormClosingEventHandler(DpsDetailForm_FormClosing);
        await _dpsDetailForm.InitAsync();
        _dpsDetailForm.UpdateScale(AppSettings.Instance.TextScale);
        _dpsDetailForm.UpdateFontScale(AppSettings.Instance.FontScale);
        if (AppSettings.Instance.ThemeJson == null)
            return;
        _dpsDetailForm.ApplyTheme(AppSettings.Instance.ThemeJson);
    }

    private void DpsDetailForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _dpsDetailPinned = false;
        _dpsDetailPinnedEntity = 0;
    }

    public void PinDpsDetail(string json)
    {
        EnsureDpsDetailForm();
        int num = 0;
        bool flag = false;
        try
        {
            using (JsonDocument jsonDocument = JsonDocument.Parse(json))
            {
                JsonElement rootElement = jsonDocument.RootElement;
                JsonElement jsonElement1;
                if (rootElement.TryGetProperty("entityId", out jsonElement1))
                    num = jsonElement1.GetInt32();
                rootElement = jsonDocument.RootElement;
                JsonElement jsonElement2;
                if (rootElement.TryGetProperty("maskNick", out jsonElement2))
                    flag = jsonElement2.GetBoolean();
            }
        }
        catch
        {
        }
        if (_dpsDetailPinned && _dpsDetailPinnedEntity == num)
        {
            _dpsDetailPinned = false;
            _dpsDetailPinnedEntity = 0;
            _dpsDetailForm?.Hide();
        }
        else
        {
            _dpsDetailPinned = true;
            _dpsDetailPinnedEntity = num;
            _dpsDetailMaskNick = flag;
            _dpsDetailForm?.UpdateData(json);
            _dpsDetailForm?.PositionNear(this);
            if (_dpsDetailForm == null || _dpsDetailForm.Visible)
                return;
            _dpsDetailForm.Show(this);
        }
    }

    public void UpdatePinnedDetail(DpsSnapshot snapshot)
    {
        if (!_dpsDetailPinned || _dpsDetailPinnedEntity == 0 || _dpsDetailForm == null || _dpsDetailForm.IsDisposed || !_dpsDetailForm.Visible)
            return;
        int pinnedEntity = _dpsDetailPinnedEntity;
        ActorDps? actorDps = snapshot.Players?.Find(a => a.EntityId == pinnedEntity);
        if (actorDps == null)
            return;
        _dpsDetailForm.UpdateData(JsonSerializer.Serialize(new
        {
            EntityId = actorDps.EntityId,
            Name = actorDps.Name,
            JobCode = actorDps.JobCode,
            ServerId = actorDps.ServerId,
            CombatScore = actorDps.CombatScore,
            CombatPower = actorDps.CombatPower,
            TotalDamage = actorDps.TotalDamage,
            Dps = actorDps.Dps,
            PartyDps = actorDps.PartyDps,
            WallDps = actorDps.WallDps,
            DamagePercent = actorDps.DamagePercent,
            BossHpPercent = actorDps.BossHpPercent,
            CritRate = actorDps.CritRate,
            HealTotal = actorDps.HealTotal,
            TopSkills = actorDps.TopSkills,
            BuffUptime = actorDps.BuffUptime,
            maskNick = _dpsDetailMaskNick
        }, WebBridge.JsonOpts));
    }

    public void HideDpsDetail()
    {
        if (_dpsDetailPinned)
            return;
        _dpsDetailForm?.Hide();
    }

    public async void OpenSettings()
    {
        Console.Error.WriteLine($"[settings] OpenSettings 호출 — _settingsForm={(_settingsForm != null ? $"Visible={_settingsForm.Visible},IsDisposed={_settingsForm.IsDisposed}" : "null")}");
        if (_settingsForm != null)
        {
            if (_settingsForm.IsDisposed)
            {
                Console.Error.WriteLine("[settings] 기존 폼 Disposed → null 처리");
                _settingsForm = null;
            }
            else
            {
                if (_settingsForm.Visible)
                {
                    Console.Error.WriteLine("[settings] 기존 폼 Visible → BringToFront");
                    _settingsForm.BringToFront();
                    return;
                }
                Console.Error.WriteLine("[settings] 기존 폼 Hidden → Close + null 처리");
                try
                {
                    _settingsForm.Close();
                }
                catch
                {
                }
                _settingsForm = null;
            }
        }
        Console.Error.WriteLine("[settings] 새 SettingsForm 생성");
        SettingsForm form = new SettingsForm();
        _settingsForm = form;
        form.SettingsSaved += new Action<SettingsData>(OnSettingsSaved);
        form.SuspendShortcuts += new Action(SuspendHotkeys);
        form.ResumeShortcuts += new Action(ResumeHotkeys);
        form.ThemeChanged += new Action<string>(OnThemeChanged);
        form.ScalePreview += (ts, fs) => _bridge?.SendToJs("settings-sync", (object)new
        {
            textScale = ts,
            fontScale = fs
        });
        form.GpuOptionChanged += () =>
        {
            if (MessageBox.Show("GPU 설정이 변경되었습니다. 적용하려면 프로그램을 재시작해야 합니다.\n\n지금 재시작하시겠습니까?", "A2Viewer 재시작 필요", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Process.Start(Application.ExecutablePath);
            Application.Exit();
        };
        form.FormClosed += (s, e) =>
        {
            Console.Error.WriteLine($"[settings] FormClosed 이벤트 — _settingsForm==form: {_settingsForm == form}");
            if (_settingsForm != form)
                return;
            _settingsForm = null;
        };
        try
        {
            await form.InitAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[settings] InitAsync 실패: " + ex.Message);
            _settingsForm = null;
            form.Dispose();
            return;
        }
        Console.Error.WriteLine($"[settings] InitAsync 완료 — IsDisposed={form.IsDisposed}, _settingsForm==form: {_settingsForm == form}");
        if (form.IsDisposed)
            return;
        if (_settingsForm != form)
            return;
        form.PositionNear(this);
        form.Show();
        form.BringToFront();
        Console.Error.WriteLine("[settings] Show 완료");
    }

    private void OnSettingsSaved(SettingsData data)
    {
        ReregisterHotkeys();
        _bridge?.SendToJs("shortcuts-updated", AppSettings.Instance.Shortcuts);
        _bridge?.SendToJs("overlay-only-changed", (object)AppSettings.Instance.OverlayOnlyWhenAion);
        _bridge?.SendToJs("tracked-skills-updated", AppSettings.Instance.TrackedSkills);
        _dpsDetailForm?.UpdateScale(data.TextScale);
        _dpsDetailForm?.UpdateFontScale(data.FontScale);
        _bridge?.SendToJs("settings-sync", (object)new
        {
            textScale = data.TextScale,
            fontScale = data.FontScale,
            dpsPercentMode = data.DpsPercentMode,
            scoreFormat = data.ScoreFormat,
            dpsTimeMode = data.DpsTimeMode
        });
    }

    private void OnThemeChanged(string themeJson)
    {
        AppSettings.Instance.ThemeJson = themeJson;
        AppSettings.Instance.Save();
        _bridge?.SendToJs("theme-changed", JsonSerializer.Deserialize<object>(themeJson));
        _dpsDetailForm?.ApplyTheme(themeJson);
    }

    private void UpdateDpsDetailPosition()
    {
        if (_dpsDetailForm == null || _dpsDetailForm.IsDisposed || !_dpsDetailForm.Visible)
            return;
        _dpsDetailForm.PositionNear(this);
    }

    private void UpdateSettingsPosition()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed || !_settingsForm.Visible)
            return;
        _settingsForm.PositionNear(this);
    }

    public void SetForceInteractive(bool force)
    {
        _forceInteractive = force;
        if (!force)
            return;
        Win32Native.SetForegroundWindow(Handle);
        _webView.Focus();
    }

    private void SetClickThrough(bool transparent)
    {
        Win32Native.SetClickThrough(Handle, transparent);
    }

    private void HitTestTimer_Tick(object? sender, EventArgs e)
    {
        if (!_initialShowDone || !Visible)
            return;
        if (_forceInteractive || _dragging || _resizing)
            SetClickThrough(false);
        else if (_isCompact)
            SetClickThrough(true);
        else if (!_clickLocked)
        {
            SetClickThrough(false);
        }
        else
        {
            Point client = PointToClient(Cursor.Position);
            if (!ClientRectangle.Contains(client))
            {
                SetClickThrough(true);
            }
            else
            {
                bool isEdge;
                if (client.Y >= 30 && client.X >= 6)
                {
                    int x = client.X;
                    int rightEdge = ClientSize.Width - 6;
                    if (x <= rightEdge && client.Y >= 6)
                    {
                        int y = client.Y;
                        int bottomEdge = ClientSize.Height - 6;
                        isEdge = y > bottomEdge;
                    }
                    else
                        isEdge = true;
                }
                else
                    isEdge = true;
                SetClickThrough(!isEdge);
            }
        }
    }

    public void SetOpacity(int percent)
    {
    }

    public void SetCompact(bool compact) => _isCompact = compact;

    private void ApplyLayered() => Win32Native.ApplyLayered(Handle);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = new IntPtr(1);
        }
        else if (m.Msg == WM_NCHITTEST)
        {
            if (!_initialShowDone || !Visible)
            {
                base.WndProc(ref m);
            }
            else if (_forceInteractive || _dragging || _resizing)
            {
                base.WndProc(ref m);
            }
            else if (_isCompact)
            {
                m.Result = new IntPtr(HTTRANSPARENT);
            }
            else if (!_clickLocked)
            {
                base.WndProc(ref m);
            }
            else
            {
                IntPtr lparam = m.LParam;
                int num1 = (int)(short)(lparam.ToInt64() & (long)ushort.MaxValue);
                lparam = m.LParam;
                int num2 = (int)(short)(lparam.ToInt64() >> 16 & (long)ushort.MaxValue);
                Point client = PointToClient(new Point(num1, num2));
                if ((client.Y < 30 || client.X < 6 || client.X > ClientSize.Width - 6 || client.Y < 6 ? 1 : (client.Y > ClientSize.Height - 6 ? 1 : 0)) == 0)
                {
                    m.Result = new IntPtr(HTTRANSPARENT);
                }
                else
                {
                    base.WndProc(ref m);
                }
            }
        }
        else
        {
            if (m.Msg == WM_HOTKEY)
                _hotkeys?.ProcessHotkey((int)m.WParam);
            base.WndProc(ref m);
        }
    }

    public void ReregisterHotkeys() => _hotkeys?.RegisterFromSettings();

    public void SuspendHotkeys() => _hotkeys?.Suspend();

    public void ResumeHotkeys() => _hotkeys?.Resume();

    private void StartDpsMeter(PacketSniffer sniffer)
    {
        try
        {
            _dpsMeter?.Dispose();
            _dpsMeter = new DpsMeter();
            _bridge?.BindDpsMeter(_dpsMeter);
            if (_charService != null)
                _charService.DpsMeter = _dpsMeter;
            _dpsMeter.SelfDetected += (nickname, serverId) =>
            {
                string serverName = ServerMap.GetName(serverId) ?? "알 수 없음";
                BeginInvoke((Action)(() =>
                {
                    SetSelf(nickname, serverId, serverName);
                    SendCharacterLoading(nickname, serverId, serverName, "self_login");
                }));
            };
            sniffer.RawPacket += (src, dst, payload, seq) => _dpsMeter?.FeedPacket(src, dst, payload, seq);
            if (sniffer.ServerPort != 0)
                _dpsMeter.Start(sniffer.ServerPort);
            else
                sniffer.ServerPortDetected += port =>
                {
                    try
                    {
                        _dpsMeter?.Start(port);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[dps] 포트 감지 후 시작 실패: " + ex.Message);
                    }
                };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] DPS 미터 시작 실패: " + ex.Message);
        }
    }

    private static string BuildOverlayHtml()
    {
        using Stream? s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aion2DPSViewer.overlay.html");
        if (s == null) return "<html><body style='color:red'>overlay.html not found</body></html>";
        using StreamReader r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private void Cleanup()
    {
        _hotkeys?.UnregisterAll();
        _fgWatcher?.Stop();
        _tray?.Dispose();
        _moveTimer?.Stop();
        _hitTestTimer?.Stop();
        _dpsMeter?.Dispose();
    }
}
