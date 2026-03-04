using System;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using QRCoder;

namespace selpic_new
{
    public partial class MainWindow : Window
    {
        private Config config;
        private LayoutConfig layout;
        private DispatcherTimer pollTimer;
        private DispatcherTimer adTimer;
        private DispatcherTimer syncTimer;
        private string adFolder = "";
        private string systemFolder = "";
        private string tempFolder = "";
        private bool isPrinting = false;
        private string lastStatus = "";
        private string currentAdFile = "";
        private bool adWebViewReady = false;
        private ImageSource? originalGuideSource = null;
        private ImageSource? printingSource = null;
        private string currentScreenState = "normal";
        private string currentWebAdUrl = "";

        public MainWindow()
        {
            InitializeComponent();

            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    Log.Write("ESC 종료");
                    Application.Current.Shutdown();
                }
            };

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                Log.Init(baseDir);

                adFolder = Path.Combine(baseDir, "adver");
                systemFolder = Path.Combine(baseDir, "system");
                tempFolder = Path.Combine(baseDir, "temp");

                Directory.CreateDirectory(adFolder);
                Directory.CreateDirectory(systemFolder);
                Directory.CreateDirectory(tempFolder);

                config = Config.Load();

                if (config.BranchCode == "")
                {
                    MessageBox.Show("설정이 필요합니다.\nselpic_new.exe --setup 실행하세요.", "SELPIC");
                    Application.Current.Shutdown();
                    return;
                }

                // 레이아웃 로딩 (default.sys → 기기 타입별)
                layout = LayoutConfig.Load(config.DeviceType);
                ApplyLayout();

                Log.Write("=== SELPIC PDF 프린터 시작 (GUI) ===");
                Log.Write($"지점: {config.BranchCode} ({config.BranchName})");
                Log.Write($"기기타입: Type_{config.DeviceType}");
                Log.Write($"프린터: {config.PrinterType}");

                // 안내 이미지 + QR코드 + 지점번호
                try { LoadGuideScreen(); } catch (Exception ex) { Log.Error("안내 화면", ex); }

                // 시스템 이미지 로딩
                try { LoadSystemImages(); } catch (Exception ex) { Log.Error("시스템 이미지", ex); }

                // 광고 동기화
                InitAds();

                // 웹뷰 광고 (스크린2)
                LoadWebAd();

                // 스크린1 WebView2 초기화 (Window 로드 완료 후)
                Loaded += async (s, e) =>
                {
                    await InitAdWebView();
                };

                // 광고 타이머
                adTimer = new DispatcherTimer();
                adTimer.Interval = TimeSpan.FromSeconds(config.PollInterval);
                adTimer.Tick += AdTimer_Tick;
                adTimer.Start();

                // 광고 동기화 타이머
                syncTimer = new DispatcherTimer();
                syncTimer.Interval = TimeSpan.FromMinutes(5);
                syncTimer.Tick += SyncTimer_Tick;
                syncTimer.Start();

                // 폴링 타이머
                pollTimer = new DispatcherTimer();
                pollTimer.Interval = TimeSpan.FromSeconds(config.PollInterval);
                pollTimer.Tick += PollTimer_Tick;
                pollTimer.Start();

                // 지점명 서버 조회 (비동기)
                LoadBranchName();

                Log.Write("GUI 초기화 완료");
            }
            catch (Exception ex)
            {
                Log.Error("GUI 초기화 실패", ex);
                MessageBox.Show($"초기화 오류: {ex.Message}", "SELPIC");
            }
        }

        // ============================================================
        // 히든 설정 버튼: 비밀번호 입력 후 설정창 열기
        // ============================================================
        void HiddenSetupBtn_Click(object sender, RoutedEventArgs e)
        {
            Topmost = false;
            string input = Interaction.InputBox(
                "비밀번호를 입력하세요:", "SELPIC 설정", "");

            if (input == "7712")
            {
                Log.Write("설정창 진입");

                pollTimer.Stop();
                adTimer.Stop();
                syncTimer.Stop();

                var setupWindow = new SetupWindow(config);
                bool? result = setupWindow.ShowDialog();

                if (result == true)
                {
                    if (setupWindow.ShouldRestart)
                    {
                        Log.Write("설정 저장 → 재시작");
                        System.Diagnostics.Process.Start(
                            System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                    }
                    else
                    {
                        Log.Write("설정 저장 → 종료");
                    }
                    Application.Current.Shutdown();
                }
                else
                {
                    Topmost = true;
                    pollTimer.Start();
                    adTimer.Start();
                    syncTimer.Start();
                    Log.Write("설정창 취소");
                }
            }
            else if (!string.IsNullOrEmpty(input))
            {
                MessageBox.Show("비밀번호가 틀렸습니다.", "SELPIC");
                Topmost = true;
            }
            else
            {
                Topmost = true;
            }
        }

        // ============================================================
        // 레이아웃 적용 (default.sys 기반)
        // ============================================================
        void ApplyLayout()
        {
            // Grid row heights
            Row0.Height = new GridLength(layout.Screen1Height);
            Row1.Height = new GridLength(layout.GuideHeight);
            Row2.Height = new GridLength(layout.Screen2Height);

            // QR 코드 위치/크기
            Canvas.SetLeft(QrImage, layout.QrLeft);
            Canvas.SetTop(QrImage, layout.QrTop);
            QrImage.Width = layout.QrSize;
            QrImage.Height = layout.QrSize;

            // 지점코드 위치/스타일
            Canvas.SetLeft(BranchCodeText, layout.CodeLeft);
            Canvas.SetTop(BranchCodeText, layout.CodeTop);
            BranchCodeText.FontSize = layout.CodeFontSize;
            BranchCodeText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(layout.CodeColor));

            // 지점명 위치/스타일
            Canvas.SetLeft(BranchNameText, layout.NameLeft);
            Canvas.SetTop(BranchNameText, layout.NameTop);
            BranchNameText.FontSize = layout.NameFontSize;
            BranchNameText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(layout.NameColor));

            Log.Write($"레이아웃 적용: Type_{config.DeviceType} " +
                      $"(QR={layout.QrLeft},{layout.QrTop} " +
                      $"Code={layout.CodeLeft},{layout.CodeTop} " +
                      $"Name={layout.NameLeft},{layout.NameTop})");
        }

        // ============================================================
        // 안내 화면: QR코드 + 지점번호
        // ============================================================
        void LoadGuideScreen()
        {
            string guidePath = Path.Combine(systemFolder, "guide.png");
            if (!File.Exists(guidePath))
                guidePath = Path.Combine(systemFolder, "guide.jpg");
            if (!File.Exists(guidePath))
                guidePath = Path.Combine(systemFolder, "guide.gif");
            if (!File.Exists(guidePath))
                guidePath = Path.Combine(systemFolder, "default_btype.png");

            if (File.Exists(guidePath))
            {
                originalGuideSource = LoadImage(guidePath);
                GuideImage.Source = originalGuideSource;
                Log.Write($"안내 이미지 로딩: {guidePath}");
            }

            try
            {
                string qrUrl = $"https://kiosk.selpic.co.kr/result/qrGen/{config.BranchCode}";
                using var qrGenerator = new QRCodeGenerator();
                using var qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.M);
                using var qrCode = new PngByteQRCode(qrData);
                byte[] qrBytes = qrCode.GetGraphic(10);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(qrBytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                QrImage.Source = bitmap;

                Log.Write($"QR코드 생성: {qrUrl}");
            }
            catch (Exception ex)
            {
                Log.Error("QR코드 생성", ex);
            }

            string code = config.BranchCode;
            string last4 = code.Length >= 4 ? code.Substring(code.Length - 4) : code;
            BranchCodeText.Text = last4;
            Log.Write($"지점번호 표시: {last4}");

            BranchNameText.Text = config.BranchName;
        }

        // ============================================================
        // 지점명 서버 조회 (비동기, 실패해도 앱 영향 없음)
        // ============================================================
        async void LoadBranchName()
        {
            try
            {
                string serverName = await ApiClient.SearchBranch(config.BranchCode);
                if (!string.IsNullOrEmpty(serverName))
                {
                    Dispatcher.Invoke(() =>
                    {
                        BranchNameText.Text = serverName;
                    });
                    config.BranchName = serverName;
                    Log.Write($"지점명 (서버): {serverName}");
                }
                else
                {
                    Log.Write($"지점명 조회 실패, 로컬 사용: {config.BranchName}");
                }
            }
            catch (Exception ex)
            {
                Log.Error("지점명 조회", ex);
            }
        }

        // ============================================================
        // 시스템 이미지 로딩 (인쇄중, 에러, 로딩 등)
        // ============================================================
        void LoadSystemImages()
        {
            string printingPath = Path.Combine(systemFolder, "printing_btype.gif");
            if (File.Exists(printingPath))
                printingSource = LoadImage(printingPath);

            string errorPath = Path.Combine(systemFolder, "error_btype.png");
            if (File.Exists(errorPath))
                ErrorImage.Source = LoadImage(errorPath);

            string loadingPath = Path.Combine(systemFolder, "launcher_loading_btype.gif");
            if (File.Exists(loadingPath))
                LoadingImage.Source = LoadImage(loadingPath);
        }

        // ============================================================
        // 중간 화면 상태 전환
        // ============================================================
        void SetScreenState(string state, string message = "")
        {
            if (state == currentScreenState && message == "") return;
            currentScreenState = state;

            Dispatcher.Invoke(() =>
            {
                GuidePanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Collapsed;
                LoadingImage.Visibility = Visibility.Collapsed;

                switch (state)
                {
                    case "normal":
                        GuidePanel.Visibility = Visibility.Visible;
                        GuideImage.Source = originalGuideSource;
                        break;
                    case "printing":
                        // 배경만 인쇄중 이미지로 교체, QR/지점정보는 유지
                        GuidePanel.Visibility = Visibility.Visible;
                        if (printingSource != null)
                            GuideImage.Source = printingSource;
                        Log.Write("화면: 인쇄 중");
                        break;
                    case "error":
                        ErrorPanel.Visibility = Visibility.Visible;
                        ErrorText.Text = message;
                        Log.Write($"화면: 에러 — {message}");
                        break;
                    case "loading":
                        LoadingImage.Visibility = Visibility.Visible;
                        Log.Write("화면: 서버 연결 중");
                        break;
                    case "network":
                        LoadingImage.Visibility = Visibility.Visible;
                        Log.Write("화면: 네트워크 연결 대기");
                        break;
                }
            });
        }

        // ============================================================
        // 네트워크 체크
        // ============================================================
        bool IsNetworkAvailable()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // 스크린1: WebView2 초기화 (영상 재생용)
        // ============================================================
        async System.Threading.Tasks.Task InitAdWebView()
        {
            try
            {
                await AdWebView.EnsureCoreWebView2Async();
                AdWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                AdWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                AdWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                AdWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "selpic.local", adFolder,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                adWebViewReady = true;
                Log.Write("스크린1 WebView2 준비 완료");

                await PlayNextAd();
            }
            catch (Exception ex)
            {
                Log.Error("스크린1 WebView2 초기화", ex);
            }
        }

        // ============================================================
        // 스크린1: 광고 초기화
        // ============================================================
        async void InitAds()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(async () =>
                {
                    await AdManager.SyncAdFiles(config.BranchCode, adFolder);
                });

                await Dispatcher.InvokeAsync(async () =>
                {
                    await PlayNextAd();
                });
            }
            catch (Exception ex)
            {
                Log.Error("광고 초기화", ex);
            }
        }

        // ============================================================
        // 스크린2: 웹뷰 광고
        // ============================================================
        async void LoadWebAd()
        {
            try
            {
                string? webUrl = await System.Threading.Tasks.Task.Run(async () =>
                {
                    return await AdManager.GetWebAdUrl(config.BranchCode);
                });

                if (!string.IsNullOrEmpty(webUrl))
                {
                    currentWebAdUrl = webUrl;
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await WebView.EnsureCoreWebView2Async();
                            WebView.CoreWebView2.Navigate(webUrl);
                            WebView.CoreWebView2.NavigationCompleted += async (s2, e2) =>
                            {
                                await WebView.CoreWebView2.ExecuteScriptAsync(
                                    "document.body.style.overflow='hidden'; document.documentElement.style.overflow='hidden';"
                                );
                            };
                            WebView.Visibility = Visibility.Visible;
                            Screen2Text.Visibility = Visibility.Collapsed;
                            Log.Write($"웹 광고 로딩: {webUrl}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error("WebView2 초기화", ex);
                        }
                    });
                }
                else
                {
                    Log.Write("웹 광고 URL 없음");
                }
            }
            catch (Exception ex)
            {
                Log.Error("웹 광고", ex);
            }
        }

        // ============================================================
        // 스크린2: 웹 광고 URL 변경 감지 및 갱신
        // ============================================================
        async System.Threading.Tasks.Task RefreshWebAd()
        {
            try
            {
                string? newUrl = await System.Threading.Tasks.Task.Run(async () =>
                {
                    return await AdManager.GetWebAdUrl(config.BranchCode);
                });

                string safeNewUrl = newUrl ?? "";

                // URL 변경 없으면 무시
                if (safeNewUrl == currentWebAdUrl) return;

                string oldUrl = currentWebAdUrl;
                currentWebAdUrl = safeNewUrl;

                await Dispatcher.InvokeAsync(async () =>
                {
                    if (!string.IsNullOrEmpty(safeNewUrl))
                    {
                        // 새 URL로 네비게이트
                        try
                        {
                            await WebView.EnsureCoreWebView2Async();
                            WebView.CoreWebView2.Navigate(safeNewUrl);
                            WebView.Visibility = Visibility.Visible;
                            Screen2Text.Visibility = Visibility.Collapsed;
                            Log.Write($"스크린2 URL 변경: {oldUrl} → {safeNewUrl}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error("스크린2 URL 갱신", ex);
                        }
                    }
                    else
                    {
                        // URL 없어짐 → 기본 텍스트로 복원
                        WebView.Visibility = Visibility.Collapsed;
                        Screen2Text.Visibility = Visibility.Visible;
                        Log.Write("스크린2 URL 제거됨 → 기본 화면");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error("스크린2 갱신 체크", ex);
            }
        }

        // ============================================================
        // 스크린1: 다음 광고 재생
        // ============================================================
        async System.Threading.Tasks.Task PlayNextAd()
        {
            try
            {
                string? adPath = await System.Threading.Tasks.Task.Run(async () =>
                {
                    return await AdManager.GetCurrentAd(config.BranchCode, adFolder);
                });

                Dispatcher.Invoke(() =>
                {
                    if (adPath == null || !File.Exists(adPath))
                    {
                        ShowDefaultScreen1();
                        return;
                    }

                    if (adPath == currentAdFile)
                        return;

                    currentAdFile = adPath;
                    string ext = Path.GetExtension(adPath).ToLower();

                    if (ext == ".mp4" || ext == ".avi" || ext == ".wmv")
                    {
                        if (adWebViewReady)
                        {
                            AdImage1.Visibility = Visibility.Collapsed;
                            AdWebView.Visibility = Visibility.Visible;

                            string fileName = Path.GetFileName(adPath);
                            string html = "<!DOCTYPE html><html><head><style>" +
                                "* { margin:0; padding:0; } " +
                                "body { background:#000; overflow:hidden; } " +
                                "video { width:100%; height:100vh; object-fit:fill; }" +
                                "</style></head><body>" +
                                "<video autoplay muted loop>" +
                                "<source src=\"https://selpic.local/" + fileName + "\" type=\"video/mp4\">" +
                                "</video></body></html>";

                            AdWebView.CoreWebView2.NavigateToString(html);
                            Log.Write($"영상 재생 (WebView2): {fileName}");
                        }
                    }
                    else
                    {
                        AdWebView.Visibility = Visibility.Collapsed;
                        AdImage1.Visibility = Visibility.Visible;
                        AdImage1.Source = LoadImage(adPath);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error("광고 재생", ex);
            }
        }

        void ShowDefaultScreen1()
        {
            string defaultPath = Path.Combine(systemFolder, "intro_main.png");
            if (!File.Exists(defaultPath))
                defaultPath = Path.Combine(systemFolder, "intro_main.gif");
            if (!File.Exists(defaultPath))
                defaultPath = Path.Combine(systemFolder, "intro_main.jpg");

            if (File.Exists(defaultPath))
            {
                AdWebView.Visibility = Visibility.Collapsed;
                AdImage1.Visibility = Visibility.Visible;
                AdImage1.Source = LoadImage(defaultPath);
            }
        }

        // ============================================================
        // 타이머: 광고 전환
        // ============================================================
        async void AdTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                await PlayNextAd();
            }
            catch (Exception ex)
            {
                Log.Error("광고 타이머", ex);
            }
        }

        // ============================================================
        // 타이머: 광고 동기화 (5분)
        // ============================================================
        async void SyncTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                await AdManager.SyncAdFiles(config.BranchCode, adFolder);
            }
            catch (Exception ex)
            {
                Log.Error("광고 동기화 타이머", ex);
            }

            // 스크린2 웹 광고 URL 변경 감지
            try
            {
                await RefreshWebAd();
            }
            catch (Exception ex)
            {
                Log.Error("스크린2 갱신 타이머", ex);
            }
        }

        // ============================================================
        // 폴링 타이머: 인쇄 Job 체크
        // ============================================================
        async void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsNetworkAvailable())
            {
                if (!isPrinting)
                    SetScreenState("error", "인터넷 연결이 끊어졌습니다.\n관리자에게 요청해 주세요.");
                return;
            }

            try
            {
                // 프린터 상태 조회 + 서버 보고 (인쇄 중에도 계속)
                string status = PrinterStatus.GetStatus(config.PrinterType);
                string remain = PrinterStatus.GetRemain();

                if (status != lastStatus)
                {
                    Log.Write($"프린터 상태: {status} — {PrinterStatus.LastDetail}");
                    lastStatus = status;
                }

                await ApiClient.ReportPrinterStatus(config.BranchCode, status, remain);

                // 인쇄 중이면 상태 보고만 하고 Job 조회는 건너뜀
                if (isPrinting) return;

                if (status == PrinterStatus.ERROR)
                {
                    string errorMsg = PrinterStatus.LastDetail switch
                    {
                        "용지 없음" => "인쇄 용지가 소진되었습니다.\n관리자에게 요청해 주세요.",
                        "용지 걸림 (JAM)" => "용지가 걸렸습니다.\n관리자에게 요청해 주세요.",
                        "토너 없음" => "토너가 부족합니다.\n관리자에게 요청해 주세요.",
                        "용지 문제" => "인쇄 용지가 소진되었습니다.\n관리자에게 요청해 주세요.",
                        _ => "프린터 오류가 발생했습니다.\n관리자에게 요청해 주세요."
                    };
                    SetScreenState("error", errorMsg);
                }
                else if (status == PrinterStatus.OFFLINE)
                {
                    SetScreenState("error", "프린터가 꺼져 있습니다.\n관리자에게 요청해 주세요.");
                }
                else
                {
                    SetScreenState("normal");
                }

                if (status == PrinterStatus.READY)
                {
                    var job = await ApiClient.GetPrintJob(config.BranchCode);
                    if (job != null)
                    {
                        isPrinting = true;
                        SetScreenState("printing");
                        await ProcessJob(job);
                        SetScreenState("normal");
                        isPrinting = false;
                    }
                }
            }
            catch (HttpRequestException)
            {
                if (!isPrinting)
                    SetScreenState("error", "인터넷 연결이 끊어졌습니다.\n관리자에게 요청해 주세요.");
                Log.Write("서버 연결 실패");
            }
            catch (Exception ex)
            {
                Log.Error("폴링", ex);
                isPrinting = false;
            }
        }

        // ============================================================
        // Job 처리
        // ============================================================
        async System.Threading.Tasks.Task ProcessJob(PrintJob job)
        {
            string? filePath = null;
            for (int i = 1; i <= config.RetryCount; i++)
            {
                filePath = await ApiClient.DownloadPDF(job.PdfUrl, job.Id, tempFolder);
                if (filePath != null) break;
                Log.Write($"다운로드 재시도 {i}/{config.RetryCount}");
                await System.Threading.Tasks.Task.Delay(2000);
            }

            if (filePath == null)
            {
                Log.Error("다운로드 최종 실패");
                return;
            }

            bool success = false;
            for (int i = 1; i <= config.RetryCount; i++)
            {
                success = await PrintEngine.PrintPDF(filePath, job, config);
                if (success) break;
                Log.Write($"인쇄 재시도 {i}/{config.RetryCount}");
                await System.Threading.Tasks.Task.Delay(3000);
            }

            if (success)
            {
                for (int i = 1; i <= config.RetryCount; i++)
                {
                    bool reported = await ApiClient.ReportPrintResult(job.Id, job.PrintType);
                    if (reported) break;
                    await System.Threading.Tasks.Task.Delay(2000);
                }
                try { File.Delete(filePath); } catch { }
            }
            else
            {
                Log.Error($"인쇄 최종 실패: Job {job.Id}");
            }
        }

        // ============================================================
        // 유틸
        // ============================================================
        BitmapImage LoadImage(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
    }
}