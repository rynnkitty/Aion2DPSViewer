using Aion2DPSViewer.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aion2DPSViewer.Core;

public class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayManager(OverlayForm form)
    {
        _icon = new NotifyIcon()
        {
            Text = "Aion2Info",
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildMenu(form)
        };
        _icon.Click += (EventHandler)((_1, _2) => form.ShowOverlay());
    }

    private static ContextMenuStrip BuildMenu(OverlayForm form)
    {
        ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        contextMenuStrip.Items.Add("열기", null, (EventHandler)((_1, _2) => form.ShowOverlay()));
        contextMenuStrip.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem toolStripMenuItem1 = new ToolStripMenuItem("아이온2 활성화 시에만 오버레이 표시")
        {
            CheckOnClick = true,
            Checked = AppSettings.Instance.OverlayOnlyWhenAion
        };
        toolStripMenuItem1.CheckedChanged += (EventHandler)((s, _) =>
        {
            ToolStripMenuItem toolStripMenuItem2 = (ToolStripMenuItem)s;
            AppSettings.Instance.OverlayOnlyWhenAion = toolStripMenuItem2.Checked;
            AppSettings.Instance.Save();
            if (toolStripMenuItem2.Checked)
                form.HideOverlay();
            else
                form.ShowOverlay();
        });
        contextMenuStrip.Items.Add(toolStripMenuItem1);
        contextMenuStrip.Items.Add(new ToolStripSeparator());
        contextMenuStrip.Items.Add("종료", null, (EventHandler)((_3, _4) => Application.Exit()));
        return contextMenuStrip;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
