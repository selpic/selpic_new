using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace selpic_new
{
    static class PrintEngine
    {
        public static async Task<bool> PrintPDF(string filePath, PrintJob job, Config config)
        {
            try
            {
                Log.Write($"인쇄: {Path.GetFileName(filePath)} | {job.Color} | {job.Side} | 매수={job.Copies}");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return await GhostScriptPrint(filePath, job, config);

                // Mac: 시뮬레이션
                await Task.Delay(1000);
                Log.Write("인쇄 완료 (시뮬레이션)");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("인쇄", ex);
                return false;
            }
        }

        static async Task<bool> GhostScriptPrint(string filePath, PrintJob job, Config config)
        {
            try
            {
                string gsPath = config.GhostScriptPath;

                if (!File.Exists(gsPath))
                {
                    Log.Error($"GhostScript 없음: {gsPath}");
                    return false;
                }

                // 컬러/흑백
                string colorOpt = job.Color == "color"
                    ? "-dUseCIEColor"
                    : "-sColorConversionStrategy=Gray -dProcessColorModel=/DeviceGray";

                // 단면/양면
                string duplexOpt;
                if (job.Side != "single")
                    duplexOpt = job.Bind == "short"
                        ? "-dDuplex=true -dTumble=true"
                        : "-dDuplex=true -dTumble=false";
                else
                    duplexOpt = "-dDuplex=false";

                // 페이지 범위
                string pageOpt = (job.PageRange == "" || job.PageRange == "all")
                    ? "" : $"-sPageList={job.PageRange}";

                string arguments = $"-dPrinted -dBATCH -dNOPAUSE -dNOSAFER -q " +
                    $"-sDEVICE=mswinpr2 \"-sOutputFile=%printer%{config.PrinterType}\" " +
                    $"-sPAPERSIZE=a4 -dFIXEDMEDIA -dPDFFitPage " +
                    $"-dNumCopies={job.Copies} {colorOpt} {duplexOpt} {pageOpt} \"{filePath}\"";

                Log.Write($"GS: {arguments}");

                var psi = new ProcessStartInfo
                {
                    FileName = gsPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    Log.Error("GhostScript 프로세스 시작 실패");
                    return false;
                }

                bool exited = await Task.Run(() => process.WaitForExit(120000));
                if (!exited)
                {
                    process.Kill();
                    Log.Error("GhostScript 타임아웃 (2분)");
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    Log.Error($"GhostScript 실패 (ExitCode={process.ExitCode}): {error}");
                    return false;
                }

                Log.Write("인쇄 완료");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("GhostScript", ex);
                return false;
            }
        }
    }
}