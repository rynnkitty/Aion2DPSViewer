using Aion2DPSViewer.Core;
using Aion2DPSViewer.Dps;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Api;

public class CombatUploader
{
    private static readonly HttpClient Http = new HttpClient()
    {
        Timeout = TimeSpan.FromSeconds(15.0)
    };
    private const string ApiUrl = "https://a2viewer-api.p1442cs9.workers.dev/api/v1/combat";
    private const int MaxRetryQueue = 10;
    private readonly ConcurrentQueue<CombatRecord> _retryQueue = new ConcurrentQueue<CombatRecord>();
    private readonly SemaphoreSlim _uploadLock = new SemaphoreSlim(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Upload(CombatRecord record)
    {
        if (record.ElapsedSeconds < 10.0 || record.TotalPartyDamage <= 0L)
            return;
        Task.Run((Func<Task>)(() => UploadAsync(record)));
    }

    private async Task UploadAsync(CombatRecord record)
    {
        if (!await _uploadLock.WaitAsync(0))
            return;
        try
        {
            CombatRecord queued;
            while (_retryQueue.TryDequeue(out queued))
            {
                if (!await SendAsync(queued))
                {
                    EnqueueRetry(queued);
                    break;
                }
            }
            if (await SendAsync(record))
                return;
            EnqueueRetry(record);
        }
        finally
        {
            _uploadLock.Release();
        }
    }

    private async Task<bool> SendAsync(CombatRecord record)
    {
        try
        {
            string str = JsonSerializer.Serialize(new
            {
                Id = record.Id,
                Timestamp = record.Timestamp,
                ElapsedSeconds = record.ElapsedSeconds,
                TotalPartyDamage = record.TotalPartyDamage,
                Target = record.Target,
                TargetMaxHp = record.TargetMaxHp,
                Players = record.Players,
                Timeline = record.Timeline,
                HitLog = record.HitLog,
                BossDebuffs = record.BossDebuffs,
                DungeonId = record.DungeonId
            }, JsonOpts);
            StringContent stringContent = new StringContent(str, Encoding.UTF8, "application/json");
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string hmac = ComputeHmac(timestamp, str);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = stringContent
            };
            request.Headers.Add("X-Uploader-Id", AppSettings.Instance.UploaderId);
            request.Headers.Add("X-Client-Version", GetClientVersion());
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add("X-Signature", hmac);
            HttpResponseMessage response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[upload] 전투 기록 업로드 성공: {record.Id} ({record.Target})");
                return true;
            }
            string errBody = "";
            try
            {
                errBody = await response.Content.ReadAsStringAsync();
            }
            catch
            {
            }
            Console.Error.WriteLine($"[upload] 업로드 실패: {(int)response.StatusCode} {response.ReasonPhrase} | {errBody}");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[upload] 업로드 예외: " + ex.Message);
            return false;
        }
    }

    private void EnqueueRetry(CombatRecord record)
    {
        if (_retryQueue.Count >= 10)
        {
            CombatRecord combatRecord;
            _retryQueue.TryDequeue(out combatRecord);
        }
        _retryQueue.Enqueue(record);
        Console.Error.WriteLine($"[upload] 재시도 큐 추가 ({_retryQueue.Count}/{10})");
    }

    private static string ComputeHmac(string timestamp, string body)
    {
        byte[] keyBytes = HexToBytes(Secrets.ApiHmacKey);
        byte[] data = Encoding.UTF8.GetBytes(timestamp + body);
        byte[] hash;
        using (var hmac = new HMACSHA256(keyBytes))
            hash = hmac.ComputeHash(data);
        return BytesToHex(hash).ToLowerInvariant();
    }

    // Polyfill for Convert.FromHexString (not available in .NET 4.8)
    private static byte[] HexToBytes(string hex)
    {
        if (hex == null || hex.Length % 2 != 0)
            return Array.Empty<byte>();
        byte[] result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return result;
    }

    // Polyfill for Convert.ToHexString (not available in .NET 4.8)
    private static string BytesToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "";
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    private static string GetClientVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
