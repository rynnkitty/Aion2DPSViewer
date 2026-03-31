using Aion2DPSViewer.Core;
using Aion2DPSViewer.Forms;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Aion2DPSViewer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        bool createdNew;
        using Mutex mutex = new Mutex(true, "Aion2DPSViewer_SingleInstance", out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Aion2DPSViewer가 이미 실행 중입니다.", "Aion2DPSViewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            string msg = e.ExceptionObject?.ToString() ?? "Unknown error";
            Console.Error.WriteLine("[fatal] " + msg);
            try { ErrorReporter.SendErrorReport("[fatal] " + msg); } catch { }
        };

        Application.ThreadException += (s, e) =>
        {
            string msg = e.Exception?.ToString() ?? "Unknown error";
            Console.Error.WriteLine("[ui-thread] " + msg);
            try { ErrorReporter.SendErrorReport("[ui-thread] " + msg); } catch { }
        };

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        // Set up logging to file
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aion2Info");
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, "error.log");
        var tee = new TeeTextWriter(Console.Error, new StreamWriter(logPath, append: true) { AutoFlush = true });
        Console.SetError(tee);

        // Check Npcap
        if (!NpcapInstaller.IsInstalled())
        {
            var result = MessageBox.Show(
                "Npcap가 설치되어 있지 않습니다.\n패킷 캡처를 위해 Npcap 설치가 필요합니다.\n\n지금 설치하시겠습니까?",
                "Npcap 필요",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                NpcapInstaller.InstallAsync().Wait();
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        FileCache.SeedFromEmbeddedResource("calc_js_cache.js", "Aion2DPSViewer.calc_js_cache.js");
        FileCache.SeedFromEmbeddedResource("formula_cache.json", "Aion2DPSViewer.formula_cache.json");
        FileCache.SeedFromEmbeddedResource("skill_priorities_cache.json", "Aion2DPSViewer.skill_priorities_cache.json");

        Application.Run(new OverlayForm());
    }
}
