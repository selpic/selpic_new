using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace selpic_new
{
    static class AdManager
    {
        static readonly HttpClient client = new HttpClient();
        static string[] adFiles = Array.Empty<string>();
        static int currentIndex = 0;

        // ============================================================
        // screen1now: 현재 재생할 광고 가져오기
        // ============================================================
        public static async Task<string?> GetCurrentAd(string branchCode, string adFolder)
        {
            try
            {
                string url = $"https://kiosk.selpic.co.kr/set/screen1now/{branchCode}";
                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                string body = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(body);
                string data = doc.RootElement.GetProperty("data").GetString() ?? "";

                if (string.IsNullOrEmpty(data) || data == "null")
                    return null;

                // URL에서 파일명 추출
                string fileName = Path.GetFileName(data);
                string filePath = Path.Combine(adFolder, fileName);

                // 로컬에 없으면 다운로드
                if (!File.Exists(filePath))
                {
                    await DownloadFile(data, filePath);
                }

                return File.Exists(filePath) ? filePath : null;
            }
            catch (Exception ex)
            {
                Log.Error("screen1now", ex);
                return null;
            }
        }

        // ============================================================
        // screenData: 광고 목록 동기화
        // ============================================================
        public static async Task SyncAdFiles(string branchCode, string adFolder)
        {
            try
            {
                string url = $"https://kiosk.selpic.co.kr/set/screenData/{branchCode}";
                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                string body = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("storage", out JsonElement storageEl))
                    return;

                // 서버 목록에서 파일명 추출
                var serverFiles = new System.Collections.Generic.List<string>();

                foreach (JsonElement item in storageEl.EnumerateArray())
                {
                    string fileUrl = item.GetString() ?? "";
                    if (string.IsNullOrEmpty(fileUrl)) continue;

                    string fileName = Path.GetFileName(fileUrl);
                    serverFiles.Add(fileName);

                    // 로컬에 없으면 다운로드
                    string filePath = Path.Combine(adFolder, fileName);
                    if (!File.Exists(filePath))
                    {
                        await DownloadFile(fileUrl, filePath);
                    }
                }

                // 서버 목록에 없는 로컬 파일 삭제
                if (serverFiles.Count > 0 && Directory.Exists(adFolder))
                {
                    foreach (string file in Directory.GetFiles(adFolder))
                    {
                        string localName = Path.GetFileName(file);
                        if (!serverFiles.Contains(localName))
                        {
                            try
                            {
                                File.Delete(file);
                                Log.Write($"광고 삭제: {localName}");
                            }
                            catch { }
                        }
                    }
                }

                Log.Write($"광고 동기화: 서버 {serverFiles.Count}개");
            }
            catch (Exception ex)
            {
                Log.Error("screenData 동기화", ex);
            }
        }

        // ============================================================
        // screen2now: 웹 광고 URL 가져오기
        // ============================================================
        public static async Task<string?> GetWebAdUrl(string branchCode)
        {
            try
            {
                string url = $"https://kiosk.selpic.co.kr/set/screen2now/{branchCode}";
                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                string body = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(body);
                string data = doc.RootElement.GetProperty("data").GetString() ?? "";

                if (string.IsNullOrEmpty(data) || data == "null")
                    return null;

                return data;
            }
            catch (Exception ex)
            {
                Log.Error("screen2now", ex);
                return null;
            }
        }

        // ============================================================
        // 파일 다운로드
        // ============================================================
        static async Task DownloadFile(string fileUrl, string filePath)
        {
            try
            {
                string tempPath = filePath + ".tmp";
                byte[] data = await client.GetByteArrayAsync(fileUrl);
                await File.WriteAllBytesAsync(tempPath, data);

                // temp → 정식 위치로 이동
                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(tempPath, filePath);

                Log.Write($"광고 다운로드: {Path.GetFileName(filePath)} ({data.Length:N0} bytes)");
            }
            catch (Exception ex)
            {
                Log.Error($"광고 다운로드 실패: {Path.GetFileName(filePath)}", ex);
            }
        }
    }
}