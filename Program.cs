using System;
using System.IO;
using System.Threading.Tasks;

namespace selpic_new
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Log.Init(baseDir);

            var config = Config.Load();

            // --setup: 강제 설정 모드
            if (args.Length > 0 && args[0] == "--setup")
            {
                ShowSetupWindow(config);
                return;
            }

            // --console: 콘솔 모드 (GUI 없이)
            if (args.Length > 0 && args[0] == "--console")
            {
                ConsoleMode(config).GetAwaiter().GetResult();
                return;
            }

            // 설정 없으면 설정창 표시
            if (config.BranchCode == "")
            {
                ShowSetupWindow(config);
                // 설정 완료 후 GUI 시작
                config = Config.Load();
                if (config.BranchCode == "")
                    return; // 취소됨
            }

            // 기본: GUI 모드
            var app = new System.Windows.Application();
            app.Run(new MainWindow());
        }

        static void ShowSetupWindow(Config config)
        {
            var app = new System.Windows.Application();
            var setupWindow = new SetupWindow(config);
            app.Run(setupWindow);
        }

        static async Task ConsoleMode(Config config)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempFolder = Path.Combine(baseDir, "temp");
            Directory.CreateDirectory(tempFolder);

            Log.Write("=== SELPIC PDF 프린터 (콘솔) ===");
            Log.Write($"지점: {config.BranchCode} ({config.BranchName})");
            Console.WriteLine("종료: Ctrl+C");
            Console.WriteLine(new string('=', 40));

            bool isPrinting = false;
            string lastStatus = "";

            while (true)
            {
                try
                {
                    if (!isPrinting)
                    {
                        string status = PrinterStatus.GetStatus(config.PrinterType);
                        if (status != lastStatus)
                        {
                            Log.Write($"프린터: {status} — {PrinterStatus.LastDetail}");
                            lastStatus = status;
                        }
                        await ApiClient.ReportPrinterStatus(config.BranchCode, status, PrinterStatus.GetRemain());

                        if (status == PrinterStatus.READY)
                        {
                            var job = await ApiClient.GetPrintJob(config.BranchCode);
                            if (job != null)
                            {
                                isPrinting = true;
                                string? fp = await ApiClient.DownloadPDF(job.PdfUrl, job.Id, tempFolder);
                                if (fp != null)
                                {
                                    bool ok = await PrintEngine.PrintPDF(fp, job, config);
                                    if (ok)
                                    {
                                        await ApiClient.ReportPrintResult(job.Id, job.PrintType);
                                        try { File.Delete(fp); } catch { }
                                    }
                                }
                                isPrinting = false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("루프", ex);
                    isPrinting = false;
                }

                await Task.Delay(config.PollInterval * 1000);
            }
        }
    }
}