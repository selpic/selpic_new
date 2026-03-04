using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace selpic_new
{
    static class ApiClient
    {
        static readonly HttpClient client = new HttpClient();

        // 지점코드 검색
        public static async Task<string> SearchBranch(string code)
        {
            try
            {
                string url = $"https://kiosk.selpic.co.kr/result/info/{code}";
                string response = await client.GetStringAsync(url);

                Log.Write($"SearchBranch 응답: {response}");

                using JsonDocument doc = JsonDocument.Parse(response);
                JsonElement root = doc.RootElement;

                // result가 boolean 또는 string일 수 있음
                bool isTrue = false;
                if (root.TryGetProperty("result", out JsonElement resultEl))
                {
                    if (resultEl.ValueKind == JsonValueKind.True)
                        isTrue = true;
                    else if (resultEl.ValueKind == JsonValueKind.String)
                        isTrue = resultEl.GetString() == "true";
                }

                if (isTrue && root.TryGetProperty("name", out JsonElement nameEl))
                    return nameEl.GetString() ?? "";

                return "";
            }
            catch (Exception ex)
            {
                Log.Error("SearchBranch", ex);
                return "";
            }
        }

        // 인쇄 Job 가져오기
        public static async Task<PrintJob?> GetPrintJob(string branchCode)
        {
            try
            {
                string url = $"https://kiosk.selpic.co.kr/result/printOrder/{branchCode}";
                HttpResponseMessage response = await client.GetAsync(url);

                // 404 등 서버 에러는 작업 없음으로 처리
                if (!response.IsSuccessStatusCode)
                    return null;

                string body = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(body);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("reboot", out JsonElement rebootEl) && rebootEl.GetBoolean())
                {
                    Log.Write("리부팅 요청");
                    return null;
                }

                bool result = root.GetProperty("result").GetBoolean();
                if (!result) return null;

                string printType = root.GetProperty("print_type").GetString() ?? "";
                if (printType != "D") return null;

                var job = new PrintJob
                {
                    Id = root.GetProperty("id").GetInt64(),
                    PrintType = printType,
                    GoodsType = root.GetProperty("goods_type").GetString() ?? "",
                    PSize = root.GetProperty("psize").GetString() ?? "",
                    PdfUrl = root.GetProperty("url").GetString() ?? ""
                };

                if (root.TryGetProperty("pdf_options", out JsonElement optEl))
                {
                    job.Color = optEl.GetProperty("color").GetString() ?? "bw";
                    job.Side = optEl.GetProperty("side").GetString() ?? "single";
                    job.Bind = optEl.GetProperty("bind").GetString() ?? "long";
                    job.Copies = optEl.GetProperty("copies").GetInt32();
                    job.PageRange = optEl.GetProperty("page_range").GetString() ?? "all";
                }

                Log.Write($"Job: ID={job.Id} | {job.Color} | {job.Side} | 매수={job.Copies}");
                return job;
            }
            catch (Exception ex)
            {
                Log.Error("Job 조회", ex);
                return null;
            }
        }

        // PDF 다운로드
        public static async Task<string?> DownloadPDF(string pdfUrl, long jobId, string tempFolder)
        {
            try
            {
                string filePath = Path.Combine(tempFolder, $"{jobId}.pdf");
                byte[] pdfBytes = await client.GetByteArrayAsync(pdfUrl);
                await File.WriteAllBytesAsync(filePath, pdfBytes);
                Log.Write($"다운로드: {pdfBytes.Length:N0} bytes");
                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error("다운로드", ex);
                return null;
            }
        }

        // 인쇄 결과 보고
        public static async Task<bool> ReportPrintResult(long jobId, string printType)
        {
            try
            {
                var data = new { photo_id = jobId.ToString(), print_type = printType, ad_type = "", ad_id = "" };
                string json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage res = await client.PostAsync(
                    "https://kiosk.selpic.co.kr/result/printFinish", content);
                string body = await res.Content.ReadAsStringAsync();
                Log.Write($"보고: {body}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("보고", ex);
                return false;
            }
        }

        // 프린터 상태 서버 전송
        public static async Task ReportPrinterStatus(string branchCode, string status, string remain)
        {
            try
            {
                string errCode = status == PrinterStatus.READY ? "0" : "1";
                var data = new { err = errCode, robot_id = branchCode, r_cnt = remain, t_cnt = "500" };
                string json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await client.PostAsync("https://kiosk.selpic.co.kr/set/statUpdate", content);
            }
            catch { }  // 상태 전송 실패는 무시
        }

        // 에러 전송
        public static async Task SendError(string branchCode, string message)
        {
            try
            {
                var data = new { robot_id = branchCode, msg = message };
                string json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await client.PostAsync("https://kiosk.selpic.co.kr/set/errorReport", content);
                Log.Write($"에러 전송: {message}");
            }
            catch (Exception ex)
            {
                Log.Error("에러 전송", ex);
            }
        }
    }
}