using System;
using System.Security.Cryptography;
using System.Text;

namespace Aion2DPSViewer.Core;

internal static class Secrets
{
    private static readonly byte[] K = Convert.FromBase64String("xzXagq09Hx0mD7FmWQcqxgyCMKgUZf8URJWk2QnTabY=");
    private static readonly byte[] V = Convert.FromBase64String("+3KSMp4foBYb+osBRHkYcQ==");
    private static readonly string[] E = new string[8]
    {
        "",
        "XPROJX1bKotfrbUBWhesYaDMNqeT8nMCycQ/wy3uf3w=",
        "YcZ/AisyZQH5iymsZQE/Kil6pyaaG+DOaVnJqSvdfnKU+AwavMFPztwb0hEpbOCgXFQHLFjcNaVkwZ1Qt/f54PenaS14JZGggGvmj1pRwyE=",
        "YcZ/AisyZQH5iymsZQE/KtgiTb5V0Jn3v6KiBDW3U3ZkHXcQ4aLIkMQ/OcmpLAZ2",
        "",
        "",
        "YcZ/AisyZQH5iymsZQE/Kl7WQZS0L0M6BKURkJTgiVw=",
        "I/mGQmi9GOYMfFcMZVidfYpcwfXD17//pfddOO540Pt4xngt546yXFrU2YR0n9NuKB0njBENz+VnOiVRRdzvyMAmPACAHHBdLfN4Jz5/HF0="
    };

    public static string WebhookUrl => D(0);
    public static string PlayncBaseUrl => D(1);
    public static string FormulaUrl => D(2);
    public static string SkillApi => D(3);
    public static string GitHubOwner => D(4);
    public static string GitHubRepo => D(5);
    public static string FormulaReferer => D(6);
    public static string ApiHmacKey => D(7);

    private static string D(int i)
    {
        byte[] inputBuffer = Convert.FromBase64String(E[i]);
        using (Aes aes = Aes.Create())
        {
            aes.Key = K;
            aes.IV = V;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using (ICryptoTransform decryptor = aes.CreateDecryptor())
                return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length));
        }
    }
}
