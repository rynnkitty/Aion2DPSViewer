using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aion2DPSViewer.Core;

public class NpcapInstaller
{
    private const string NpcapDownloadUrl = "https://npcap.com/dist/npcap-1.87.exe";
    private const string InstallerName = "npcap-installer.exe";

    public static bool IsInstalled() => FindDllDirectory() != null;

    public static string? FindDllDirectory()
    {
        string dllDirectory = Path.Combine(Environment.SystemDirectory, "Npcap");
        if (Directory.Exists(dllDirectory) && File.Exists(Path.Combine(dllDirectory, "wpcap.dll")))
            return dllDirectory;
        return File.Exists(Path.Combine(Environment.SystemDirectory, "wpcap.dll")) ? Environment.SystemDirectory : null;
    }

    public static async Task InstallAsync()
    {
        await EnsureInstalledAsync();
    }

    public static async Task<bool> EnsureInstalledAsync()
    {
        if (IsInstalled())
            return true;
        if (MessageBox.Show(
            "A2Viewer는 패킷 캡처를 위해 Npcap이 필요합니다.\n\n다운로드하여 설치하시겠습니까?\n(설치 화면에서 'WinPcap API-compatible Mode'를 체크해주세요)",
            "A2Viewer — Npcap 설치 필요",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) != DialogResult.Yes)
        {
            MessageBox.Show("Npcap 없이는 A2Viewer를 사용할 수 없습니다.\n\n수동 설치: https://npcap.com", "A2Viewer", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return false;
        }
        try
        {
            string installerPath = Path.Combine(Path.GetTempPath(), "npcap-installer.exe");
            using (HttpClient http = new HttpClient() { Timeout = TimeSpan.FromMinutes(3.0) })
            {
                HttpResponseMessage response = await http.GetAsync(NpcapDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                using (FileStream fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await contentStream.CopyToAsync(fileStream);
                }
            }
            Process process = Process.Start(new ProcessStartInfo()
            {
                FileName = installerPath,
                UseShellExecute = true
            });
            if (process == null)
            {
                MessageBox.Show("Npcap 설치 프로그램을 실행할 수 없습니다.", "A2Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            process.WaitForExit();
            try { File.Delete(installerPath); } catch { }
            if (!IsInstalled())
            {
                MessageBox.Show("Npcap 설치가 완료되지 않았습니다.\n\n수동 설치: https://npcap.com", "A2Viewer", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Npcap 다운로드/설치 중 오류가 발생했습니다.\n\n{ex.Message}\n\n수동 설치: https://npcap.com", "A2Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
