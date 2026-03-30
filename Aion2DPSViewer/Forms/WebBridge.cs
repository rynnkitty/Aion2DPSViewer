using Aion2DPSViewer.Core;
using Aion2DPSViewer.Dps;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aion2DPSViewer.Forms;

public class WebBridge
{
    private readonly OverlayForm _form;
    private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;
    private DpsMeter? _dpsMeter;
    internal static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public event Action<int>? TextScaleChanged;

    public event Action<int>? FontScaleChanged;

    public WebBridge(OverlayForm form, Microsoft.Web.WebView2.WinForms.WebView2 webView)
    {
        _form = form;
        _webView = webView;
    }

    public void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
        JsonObject? jsonObject;
        try
        {
            jsonObject = JsonNode.Parse(json)?.AsObject();
        }
        catch
        {
            return;
        }
        if (jsonObject == null)
            return;
        string? str1 = jsonObject["type"]?.GetValue<string>();
        if (str1 == null)
            return;
        int? callId = jsonObject["_callId"]?.GetValue<int>();
        JsonNode? data = jsonObject["data"];
        switch (str1.Length)
        {
            case 8:
                switch (str1[0])
                {
                    case 'm':
                        if (str1 != "mouse-up")
                            return;
                        _form.BeginInvoke((Action)(() => _form.StopMoveResize()));
                        return;
                    case 'u':
                        if (str1 != "ui-ready")
                            return;
                        Console.Error.WriteLine("[bridge] ui-ready 수신");
                        _form.BeginInvoke((Action)(() => _form.OnUiReady()));
                        return;
                    default:
                        return;
                }
            case 9:
                if (str1 != "dps-reset")
                    break;
                _dpsMeter?.Reset();
                break;
            case 10:
                if (str1 != "drag-start")
                    break;
                _form.BeginInvoke((Action)(() => _form.StartDrag()));
                break;
            case 11:
                switch (str1[4])
                {
                    case 'c':
                        if (str1 != "set-compact")
                            return;
                        JsonNode? jsonNode1 = data;
                        bool isCompact = (jsonNode1 != null ? (jsonNode1.GetValue<bool>() ? 1 : 0) : 0) != 0;
                        _form.BeginInvoke((Action)(() => _form.SetCompact(isCompact)));
                        return;
                    case 'o':
                        if (str1 != "get-opacity")
                        {
                            if (str1 != "set-opacity")
                                return;
                            JsonNode? jsonNode2 = data;
                            int opacity = jsonNode2 != null ? jsonNode2.GetValue<int>() : 50;
                            AppSettings.Instance.Opacity = opacity;
                            AppSettings.Instance.Save();
                            _form.BeginInvoke((Action)(() => _form.SetOpacity(opacity)));
                            return;
                        }
                        Reply(callId, AppSettings.Instance.Opacity);
                        return;
                    case 'v':
                        if (str1 != "get-version")
                            return;
                        Reply(callId, typeof(WebBridge).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
                        return;
                    default:
                        return;
                }
            case 12:
                switch (str1[1])
                {
                    case 'e':
                        if (str1 != "resize-start")
                            return;
                        string dir = data?.GetValue<string>() ?? "";
                        _form.BeginInvoke((Action)(() => _form.StartResize(dir)));
                        return;
                    case 'l':
                        if (str1 != "close-window")
                            return;
                        _form.BeginInvoke((Action)(() => Application.Exit()));
                        return;
                    default:
                        return;
                }
            case 13:
                switch (str1[0])
                {
                    case 'g':
                        if (str1 != "get-shortcuts")
                            return;
                        Reply(callId, AppSettings.Instance.Shortcuts);
                        return;
                    case 'o':
                        if (str1 != "open-settings")
                            return;
                        Console.Error.WriteLine("[bridge] open-settings 수신");
                        _form.BeginInvoke((Action)(() => _form.OpenSettings()));
                        return;
                    case 's':
                        if (str1 != "set-shortcuts" || data == null)
                            return;
                        ShortcutSettings? shortcutSettings = data.Deserialize<ShortcutSettings>(JsonOpts);
                        if (shortcutSettings == null)
                            return;
                        AppSettings.Instance.Shortcuts = shortcutSettings;
                        AppSettings.Instance.Save();
                        _form.BeginInvoke((Action)(() => _form.ReregisterHotkeys()));
                        SendToJs("shortcuts-updated", AppSettings.Instance.Shortcuts);
                        return;
                    default:
                        return;
                }
            case 14:
                switch (str1[0])
                {
                    case 'd':
                        if (str1 != "dps-detail-pin" || data == null)
                            return;
                        _form.BeginInvoke((Action)(() => _form.PinDpsDetail(data.ToJsonString())));
                        return;
                    case 'g':
                        if (str1 != "get-text-scale")
                            return;
                        Reply(callId, AppSettings.Instance.TextScale);
                        return;
                    case 's':
                        if (str1 != "set-text-scale")
                        {
                            if (str1 != "set-font-scale")
                            {
                                if (str1 != "set-active-tab")
                                    return;
                                string tab = data?.GetValue<string>() ?? "party";
                                _form.BeginInvoke((Action)(() => _form.SetDpsTabActive(tab == "dps")));
                                return;
                            }
                            JsonNode? jsonNode3 = data;
                            int num = jsonNode3 != null ? jsonNode3.GetValue<int>() : 100;
                            AppSettings.Instance.FontScale = num;
                            AppSettings.Instance.Save();
                            FontScaleChanged?.Invoke(num);
                            return;
                        }
                        JsonNode? jsonNode4 = data;
                        int num1 = jsonNode4 != null ? jsonNode4.GetValue<int>() : 100;
                        AppSettings.Instance.TextScale = num1;
                        AppSettings.Instance.Save();
                        TextScaleChanged?.Invoke(num1);
                        return;
                    default:
                        return;
                }
            case 16:
                switch (str1[4])
                {
                    case 'c':
                        if (str1 != "set-click-locked")
                            return;
                        JsonNode? jsonNode5 = data;
                        bool locked = (jsonNode5 != null ? (jsonNode5.GetValue<bool>() ? 1 : 0) : 0) != 0;
                        _form.BeginInvoke((Action)(() => _form.SetClickLocked(locked)));
                        return;
                    case 'i':
                        if (str1 != "set-ignore-mouse")
                            return;
                        JsonNode? jsonNode6 = data;
                        bool ignore = (jsonNode6 != null ? (jsonNode6.GetValue<bool>() ? 1 : 0) : 1) != 0;
                        _form.BeginInvoke((Action)(() => _form.SetForceInteractive(!ignore)));
                        return;
                    case 'm':
                        if (str1 != "resume-shortcuts")
                            return;
                        _form.BeginInvoke((Action)(() => _form.ResumeHotkeys()));
                        return;
                    case 'o':
                        if (str1 != "get-overlay-only")
                        {
                            if (str1 != "set-overlay-only")
                                return;
                            JsonNode? jsonNode7 = data;
                            bool data1 = jsonNode7 == null || jsonNode7.GetValue<bool>();
                            AppSettings.Instance.OverlayOnlyWhenAion = data1;
                            AppSettings.Instance.Save();
                            SendToJs("overlay-only-changed", (object)data1);
                            return;
                        }
                        Reply(callId, AppSettings.Instance.OverlayOnlyWhenAion);
                        return;
                    default:
                        return;
                }
            case 17:
                switch (str1[1])
                {
                    case 'e':
                        if (str1 != "send-error-report")
                            return;
                        Task.Run((Func<Task>)(async () =>
                        {
                            (bool flag2, string str4) = await ErrorReporter.SendErrorReport(data?.GetValue<string>());
                            _form.BeginInvoke((Action)(() => Reply(callId, (object)new
                            {
                                success = flag2,
                                error = str4
                            })));
                        }));
                        return;
                    case 'u':
                        if (str1 != "suspend-shortcuts")
                            return;
                        _form.BeginInvoke((Action)(() => _form.SuspendHotkeys()));
                        return;
                    default:
                        return;
                }
            case 18:
                switch (str1[0])
                {
                    case 'g':
                        if (str1 != "get-tracked-skills")
                        {
                            if (str1 != "get-combat-records")
                                return;
                            Reply(callId, _dpsMeter?.GetCombatRecords());
                            return;
                        }
                        Reply(callId, AppSettings.Instance.TrackedSkills);
                        return;
                    case 'l':
                        if (str1 != "load-combat-record")
                            return;
                        string? id = data?.GetValue<string>();
                        Reply(callId, !string.IsNullOrEmpty(id) ? (object?)_dpsMeter?.GetCombatRecord(id) : null);
                        return;
                    case 's':
                        if (str1 != "set-tracked-skills" || data == null)
                            return;
                        Dictionary<string, List<string>>? dictionary = data.Deserialize<Dictionary<string, List<string>>>(JsonOpts);
                        if (dictionary == null)
                            return;
                        AppSettings.Instance.TrackedSkills = dictionary;
                        AppSettings.Instance.Save();
                        return;
                    default:
                        return;
                }
            case 19:
                switch (str1[4])
                {
                    case '-':
                        if (str1 != "stop-viewing-record")
                            return;
                        EmitCurrentDps();
                        return;
                    case 'a':
                        if (str1 != "set-auto-tab-switch")
                            return;
                        AppSettings instance1 = AppSettings.Instance;
                        JsonNode? jsonNode8 = data;
                        int num2 = jsonNode8 != null ? (jsonNode8.GetValue<bool>() ? 1 : 0) : 1;
                        instance1.AutoTabSwitch = num2 != 0;
                        AppSettings.Instance.Save();
                        return;
                    case 'k':
                        if (str1 != "get-known-dp-skills")
                            return;
                        Reply(callId, AppSettings.Instance.KnownDpSkills);
                        return;
                    default:
                        return;
                }
            case 24:
                if (str1 != "set-keep-self-on-refresh")
                    break;
                AppSettings instance2 = AppSettings.Instance;
                JsonNode? jsonNode9 = data;
                int num3 = jsonNode9 != null ? (jsonNode9.GetValue<bool>() ? 1 : 0) : 1;
                instance2.KeepSelfOnRefresh = num3 != 0;
                AppSettings.Instance.Save();
                break;
            case 25:
                if (str1 != "set-keep-party-on-refresh")
                    break;
                AppSettings instance3 = AppSettings.Instance;
                JsonNode? jsonNode10 = data;
                int num4 = jsonNode10 != null ? (jsonNode10.GetValue<bool>() ? 1 : 0) : 1;
                instance3.KeepPartyOnRefresh = num4 != 0;
                AppSettings.Instance.Save();
                break;
        }
    }

    public void BindDpsMeter(DpsMeter meter)
    {
        _dpsMeter = meter;
        meter.DpsUpdated += snapshot =>
        {
            try
            {
                _form.BeginInvoke((Action)(() =>
                {
                    SendToJs("dps-update", (object)snapshot);
                    _form.UpdatePinnedDetail(snapshot);
                }));
            }
            catch
            {
            }
        };
        meter.CombatStarted += () =>
        {
            try
            {
                _form.BeginInvoke((Action)(() => SendToJs("combat-started", null)));
            }
            catch
            {
            }
        };
        meter.CombatRecordSaved += record =>
        {
            try
            {
                CombatRecordSummary summary = new CombatRecordSummary()
                {
                    Id = record.Id,
                    Timestamp = record.Timestamp,
                    ElapsedSeconds = record.ElapsedSeconds,
                    TotalPartyDamage = record.TotalPartyDamage,
                    Target = record.Target,
                    PlayerCount = record.Players.Count
                };
                _form.BeginInvoke((Action)(() => SendToJs("combat-record-saved", (object)summary)));
            }
            catch
            {
            }
        };
    }

    public void EmitCurrentDps()
    {
        if (_dpsMeter == null)
            return;
        DpsSnapshot? data = _dpsMeter.BuildCurrentSnapshot();
        if (data == null)
            return;
        SendToJs("dps-update", data);
    }

    public void SendToJs(string type, object? data)
    {
        if (_webView?.CoreWebView2 == null)
            return;
        string msg = JsonSerializer.Serialize(new
        {
            type = type,
            data = data
        }, JsonOpts);
        _form.BeginInvoke((Action)(() => _webView.CoreWebView2.PostWebMessageAsString(msg)));
    }

    private void Reply(int? callId, object? result)
    {
        if (!callId.HasValue)
            return;
        string msg = JsonSerializer.Serialize(new
        {
            _responseId = callId,
            result = result
        }, JsonOpts);
        _form.BeginInvoke((Action)(() => _webView.CoreWebView2.PostWebMessageAsString(msg)));
    }
}
