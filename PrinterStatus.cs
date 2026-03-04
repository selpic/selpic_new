using System;
using System.Runtime.InteropServices;

namespace selpic_new
{
    static class PrinterStatus
    {
        // 서버 전송용 상태값
        public const string READY = "normal";
        public const string ERROR = "error";
        public const string OFFLINE = "offline";
        public const string BUSY = "busy";

        // 상세 상태 메시지 (로그/UI용)
        public static string LastDetail { get; private set; } = "";

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool OpenPrinter(string pPrinterName, out IntPtr hPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool GetPrinter(IntPtr hPrinter, int level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool ClosePrinter(IntPtr hPrinter);

        // Windows 프린터 상태 플래그
        const int PRINTER_STATUS_PAUSED           = 0x00000001;
        const int PRINTER_STATUS_ERROR            = 0x00000002;
        const int PRINTER_STATUS_PENDING_DELETION = 0x00000004;
        const int PRINTER_STATUS_PAPER_JAM        = 0x00000008;
        const int PRINTER_STATUS_PAPER_OUT        = 0x00000010;
        const int PRINTER_STATUS_MANUAL_FEED      = 0x00000020;
        const int PRINTER_STATUS_PAPER_PROBLEM    = 0x00000040;
        const int PRINTER_STATUS_OFFLINE          = 0x00000080;
        const int PRINTER_STATUS_IO_ACTIVE        = 0x00000100;
        const int PRINTER_STATUS_BUSY             = 0x00000200;
        const int PRINTER_STATUS_PRINTING         = 0x00000400;
        const int PRINTER_STATUS_OUTPUT_BIN_FULL  = 0x00000800;
        const int PRINTER_STATUS_NOT_AVAILABLE    = 0x00001000;
        const int PRINTER_STATUS_WAITING          = 0x00002000;
        const int PRINTER_STATUS_PROCESSING       = 0x00004000;
        const int PRINTER_STATUS_INITIALIZING     = 0x00008000;
        const int PRINTER_STATUS_WARMING_UP       = 0x00010000;
        const int PRINTER_STATUS_TONER_LOW        = 0x00020000;
        const int PRINTER_STATUS_NO_TONER         = 0x00040000;
        const int PRINTER_STATUS_PAGE_PUNT        = 0x00080000;
        const int PRINTER_STATUS_USER_INTERVENTION= 0x00100000;
        const int PRINTER_STATUS_OUT_OF_MEMORY    = 0x00200000;
        const int PRINTER_STATUS_DOOR_OPEN        = 0x00400000;
        const int PRINTER_STATUS_SERVER_UNKNOWN   = 0x00800000;
        const int PRINTER_STATUS_POWER_SAVE       = 0x01000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct PRINTER_INFO_2
        {
            public IntPtr pServerName;
            public IntPtr pPrinterName;
            public IntPtr pShareName;
            public IntPtr pPortName;
            public IntPtr pDriverName;
            public IntPtr pComment;
            public IntPtr pLocation;
            public IntPtr pDevMode;
            public IntPtr pSepFile;
            public IntPtr pPrintProcessor;
            public IntPtr pDatatype;
            public IntPtr pParameters;
            public IntPtr pSecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint cJobs;
            public uint AveragePPM;
        }

        public static string GetStatus(string printerName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LastDetail = "시뮬레이션 (Mac)";
                return READY;
            }

            // Brother DLL 도착 전까지 기본 상태만 반환
            // USB 연결 Spooler API로는 실시간 하드웨어 상태 감지 불가
            // Phase 2: Brother DLL로 상세 상태 조회 업그레이드

            try
            {
                if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                {
                    LastDetail = "프린터를 찾을 수 없음";
                    return OFFLINE;
                }

                ClosePrinter(hPrinter);
                LastDetail = "정상";
                return READY;
            }
            catch
            {
                LastDetail = "프린터 연결 확인 불가";
                return OFFLINE;
            }
        }

        static string AnalyzeStatus(int status, int jobs)
        {
            // 정상
            if (status == 0)
            {
                LastDetail = jobs > 0 ? $"정상 (대기 {jobs}건)" : "정상";
                return READY;
            }

            // 상세 분석 — 심각한 순서대로 체크

            // 오프라인
            if ((status & PRINTER_STATUS_OFFLINE) != 0)
            {
                LastDetail = "오프라인 (연결 끊김)";
                return OFFLINE;
            }

            // 용지 문제
            if ((status & PRINTER_STATUS_PAPER_JAM) != 0)
            {
                LastDetail = "용지 걸림 (JAM)";
                return ERROR;
            }
            if ((status & PRINTER_STATUS_PAPER_OUT) != 0)
            {
                LastDetail = "용지 없음";
                return ERROR;
            }
            if ((status & PRINTER_STATUS_PAPER_PROBLEM) != 0)
            {
                LastDetail = "용지 문제";
                return ERROR;
            }

            // 토너 문제
            if ((status & PRINTER_STATUS_NO_TONER) != 0)
            {
                LastDetail = "토너 없음";
                return ERROR;
            }
            if ((status & PRINTER_STATUS_TONER_LOW) != 0)
            {
                LastDetail = "토너 부족 (교체 필요)";
                return READY;  // 인쇄는 가능
            }

            // 물리적 문제
            if ((status & PRINTER_STATUS_DOOR_OPEN) != 0)
            {
                LastDetail = "커버 열림";
                return ERROR;
            }
            if ((status & PRINTER_STATUS_OUTPUT_BIN_FULL) != 0)
            {
                LastDetail = "출력함 가득 참";
                return ERROR;
            }
            if ((status & PRINTER_STATUS_OUT_OF_MEMORY) != 0)
            {
                LastDetail = "메모리 부족";
                return ERROR;
            }

            // 사용자 조치 필요
            if ((status & PRINTER_STATUS_USER_INTERVENTION) != 0)
            {
                LastDetail = "사용자 조치 필요";
                return ERROR;
            }

            // 일반 에러
            if ((status & PRINTER_STATUS_ERROR) != 0)
            {
                LastDetail = $"프린터 오류 (0x{status:X})";
                return ERROR;
            }

            // 바쁨 관련 (인쇄 가능 상태)
            if ((status & PRINTER_STATUS_PRINTING) != 0)
            {
                LastDetail = $"인쇄 중 (대기 {jobs}건)";
                return BUSY;
            }
            if ((status & PRINTER_STATUS_BUSY) != 0)
            {
                LastDetail = "처리 중";
                return BUSY;
            }
            if ((status & PRINTER_STATUS_PROCESSING) != 0)
            {
                LastDetail = "데이터 처리 중";
                return BUSY;
            }
            if ((status & PRINTER_STATUS_IO_ACTIVE) != 0)
            {
                LastDetail = "통신 중";
                return BUSY;
            }

            // 대기 상태 (인쇄 가능)
            if ((status & PRINTER_STATUS_WARMING_UP) != 0)
            {
                LastDetail = "워밍업 중";
                return BUSY;
            }
            if ((status & PRINTER_STATUS_INITIALIZING) != 0)
            {
                LastDetail = "초기화 중";
                return BUSY;
            }
            if ((status & PRINTER_STATUS_POWER_SAVE) != 0)
            {
                LastDetail = "절전 모드";
                return READY;
            }
            if ((status & PRINTER_STATUS_WAITING) != 0)
            {
                LastDetail = "대기 중";
                return READY;
            }
            if ((status & PRINTER_STATUS_PAUSED) != 0)
            {
                LastDetail = "일시정지";
                return ERROR;
            }
            if ((status & PRINTER_STATUS_MANUAL_FEED) != 0)
            {
                LastDetail = "수동 급지 대기";
                return ERROR;
            }

            // 알 수 없는 상태
            LastDetail = $"알 수 없는 상태 (0x{status:X})";
            return READY;
        }

        public static string GetRemain()
        {
            // Brother DLL 도착 후 토너 잔량 조회로 업그레이드 예정
            return "100";
        }
    }
}