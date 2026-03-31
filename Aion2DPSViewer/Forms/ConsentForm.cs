using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aion2DPSViewer.Forms;

public sealed class ConsentForm : Form
{
    public const string RequiredVersion = "1.0";
    private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

    public bool Agreed { get; private set; }

    public ConsentForm()
    {
        Text = "";
        Size = new Size(500, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(10, 10, 15);
        Microsoft.Web.WebView2.WinForms.WebView2 webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
        webView2.Dock = DockStyle.Fill;
        _webView = webView2;
        Controls.Add(_webView);
        Load += new EventHandler(ConsentForm_Load);
    }

    private async void ConsentForm_Load(object? sender, EventArgs e)
    {
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.WebMessageReceived += new EventHandler<CoreWebView2WebMessageReceivedEventArgs>(WebView_WebMessageReceived);
        _webView.CoreWebView2.NavigateToString(BuildHtml());
    }

    private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg;
        try { msg = e.TryGetWebMessageAsString() ?? e.WebMessageAsJson; }
        catch { msg = e.WebMessageAsJson; }
        if (msg == "agree")
        {
            Agreed = true;
            BeginInvoke((Action)(() => Close()));
        }
        else if (msg == "decline")
        {
            Agreed = false;
            BeginInvoke((Action)(() => Close()));
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !Agreed)
            Agreed = false;
        base.OnFormClosing(e);
    }

    private static string BuildHtml()
    {
        return "<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n<style>\n  * { margin: 0; padding: 0; box-sizing: border-box; }\n  body {\n    font-family: 'Pretendard', 'Segoe UI', -apple-system, sans-serif;\n    background: #0a0a0f;\n    color: #e0e0ea;\n    padding: 32px 28px 24px;\n    display: flex;\n    flex-direction: column;\n    height: 100vh;\n    user-select: none;\n  }\n  .logo { text-align: center; margin-bottom: 8px; }\n  .logo span { font-size: 22px; font-weight: 700; color: #5b7fff; }\n  h1 {\n    text-align: center;\n    font-size: 16px;\n    font-weight: 600;\n    color: #c0c0d0;\n    margin-bottom: 20px;\n  }\n  .content {\n    flex: 1;\n    overflow-y: auto;\n    background: #12121a;\n    border: 1px solid #1e1e2e;\n    border-radius: 10px;\n    padding: 20px 22px;\n    font-size: 13px;\n    line-height: 1.7;\n    color: #b0b0c0;\n  }\n  .content::-webkit-scrollbar { width: 6px; }\n  .content::-webkit-scrollbar-track { background: transparent; }\n  .content::-webkit-scrollbar-thumb { background: #2a2a3a; border-radius: 3px; }\n  .section { margin-bottom: 16px; }\n  .section-title {\n    font-size: 12px;\n    font-weight: 700;\n    color: #5b7fff;\n    text-transform: uppercase;\n    letter-spacing: 1px;\n    margin-bottom: 6px;\n    padding-bottom: 4px;\n    border-bottom: 1px solid #1e1e2e;\n  }\n  .section ul {\n    list-style: none;\n    padding-left: 0;\n  }\n  .section li {\n    position: relative;\n    padding-left: 14px;\n    margin-bottom: 3px;\n  }\n  .section li::before {\n    content: '\\u2022';\n    position: absolute;\n    left: 0;\n    color: #5b7fff;\n  }\n  .buttons {\n    display: flex;\n    gap: 12px;\n    justify-content: center;\n    margin-top: 20px;\n    flex-shrink: 0;\n  }\n  button {\n    padding: 10px 32px;\n    border: none;\n    border-radius: 8px;\n    font-size: 14px;\n    font-weight: 600;\n    cursor: pointer;\n    transition: all 0.15s;\n  }\n  .btn-agree {\n    background: #5b7fff;\n    color: #fff;\n  }\n  .btn-agree:hover { background: #4a6ef0; transform: translateY(-1px); }\n  .btn-decline {\n    background: #1e1e2e;\n    color: #808090;\n    border: 1px solid #2a2a3a;\n  }\n  .btn-decline:hover { background: #2a2a3a; color: #a0a0b0; }\n</style>\n</head>\n<body>\n  <div class=\"logo\"><span>Aion2Info</span></div>\n  <h1>개인정보 수집·이용 동의</h1>\n  <div class=\"content\">\n    <div class=\"section\">\n      <div class=\"section-title\">수집 항목</div>\n      <ul>\n        <li>게임 내 캐릭터 닉네임, 서버 정보</li>\n        <li>전투력, DPS, 스킬 사용 내역</li>\n        <li>파티 구성 및 전투 기록 (보스, 시간, 피해량)</li>\n      </ul>\n    </div>\n    <div class=\"section\">\n      <div class=\"section-title\">수집 목적</div>\n      <ul>\n        <li>전투 DPS 통계 분석 및 시각화</li>\n        <li>웹사이트(a2viewer.co.kr) 랭킹·통계 제공</li>\n      </ul>\n    </div>\n    <div class=\"section\">\n      <div class=\"section-title\">보유 기간</div>\n      <ul>\n        <li>서비스 운영 기간 동안 보유</li>\n      </ul>\n    </div>\n    <div class=\"section\">\n      <div class=\"section-title\">제3자 제공</div>\n      <ul>\n        <li>수집된 정보는 닉네임 마스킹(부분 비공개) 처리되어 웹사이트에 표시됩니다</li>\n        <li>마스킹 없는 원본 정보는 제3자에게 제공하지 않습니다</li>\n      </ul>\n    </div>\n  </div>\n  <div class=\"buttons\">\n    <button class=\"btn-decline\" onclick=\"window.chrome.webview.postMessage('decline')\">동의하지 않습니다</button>\n    <button class=\"btn-agree\" onclick=\"window.chrome.webview.postMessage('agree')\">동의합니다</button>\n  </div>\n</body>\n</html>";
    }
}
