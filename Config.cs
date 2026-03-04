using System;
using System.IO;

namespace selpic_new
{
    public class Config
    {
        public string BranchCode { get; set; } = "";
        public string BranchName { get; set; } = "";
        public string DeviceType { get; set; } = "C";
        public string PrinterType { get; set; } = "Brother HL-L9430CDN series";
        public int PollInterval { get; set; } = 5;
        public int RetryCount { get; set; } = 3;
        public string PaymentCode { get; set; } = "";
        public string TerminalCode { get; set; } = "";
        public string GhostScriptPath { get; set; } = "";

        static string ConfigPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        public void Save()
        {
            var lines = new[]
            {
                "[Branch]",
                $"Code={BranchCode}",
                $"Name={BranchName}",
                $"DeviceType={DeviceType}",
                "",
                "[Printer]",
                $"Type={PrinterType}",
                $"GhostScript={GhostScriptPath}",
                "",
                "[System]",
                $"PollInterval={PollInterval}",
                $"RetryCount={RetryCount}",
                "",
                "[Payment]",
                $"PaymentCode={PaymentCode}",
                $"TerminalCode={TerminalCode}"
            };
            File.WriteAllLines(ConfigPath, lines);
            Log.Write($"설정 저장: {ConfigPath}");
        }

        public static Config Load()
        {
            var config = new Config();
            if (!File.Exists(ConfigPath)) return config;

            string[] lines = File.ReadAllLines(ConfigPath);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("[")) continue;

                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0) continue;

                string key = trimmed.Substring(0, eqIndex).Trim();
                string val = trimmed.Substring(eqIndex + 1).Trim();

                switch (key)
                {
                    case "Code": config.BranchCode = val; break;
                    case "Name": config.BranchName = val; break;
                    case "DeviceType": config.DeviceType = val; break;
                    case "Type": config.PrinterType = val; break;
                    case "GhostScript": config.GhostScriptPath = val; break;
                    case "PollInterval": config.PollInterval = int.TryParse(val, out int i) ? i : 5; break;
                    case "RetryCount": config.RetryCount = int.TryParse(val, out int r) ? r : 3; break;
                    case "PaymentCode": config.PaymentCode = val; break;
                    case "TerminalCode": config.TerminalCode = val; break;
                }
            }

            // GhostScript 기본 경로
            if (string.IsNullOrEmpty(config.GhostScriptPath))
                config.GhostScriptPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Tools", "GhostScript", "bin", "gswin64c.exe");

            return config;
        }
    }
}