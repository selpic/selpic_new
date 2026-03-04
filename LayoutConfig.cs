using System;
using System.IO;

namespace selpic_new
{
    public class LayoutConfig
    {
        // Grid row heights
        public int Screen1Height { get; set; } = 810;
        public int GuideHeight { get; set; } = 540;
        public int Screen2Height { get; set; } = 570;

        // QR code (guide panel 내 좌표)
        public int QrLeft { get; set; } = 822;
        public int QrTop { get; set; } = 145;
        public int QrSize { get; set; } = 200;

        // Branch code
        public int CodeLeft { get; set; } = 840;
        public int CodeTop { get; set; } = 325;
        public int CodeFontSize { get; set; } = 72;
        public string CodeColor { get; set; } = "#333333";

        // Branch name
        public int NameLeft { get; set; } = 795;
        public int NameTop { get; set; } = 457;
        public int NameFontSize { get; set; } = 30;
        public string NameColor { get; set; } = "#FFFFFF";

        // Error
        public int ErrorFontSize { get; set; } = 28;

        public static LayoutConfig Load(string deviceType)
        {
            var layout = new LayoutConfig();
            string sysPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "system", "default.sys");

            if (!File.Exists(sysPath))
            {
                Log.Write($"default.sys 없음, 기본값 사용 (Type_{deviceType})");
                return GetDefault(deviceType);
            }

            string section = $"Type_{deviceType}";
            string[] lines = File.ReadAllLines(sysPath);
            bool inSection = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("["))
                {
                    inSection = trimmed.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inSection || string.IsNullOrEmpty(trimmed)) continue;

                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0) continue;

                string key = trimmed.Substring(0, eqIndex).Trim();
                string val = trimmed.Substring(eqIndex + 1).Trim();

                switch (key)
                {
                    case "Screen1_height": if (int.TryParse(val, out int s1)) layout.Screen1Height = s1; break;
                    case "Guide_height": if (int.TryParse(val, out int gh)) layout.GuideHeight = gh; break;
                    case "Screen2_height": if (int.TryParse(val, out int s2)) layout.Screen2Height = s2; break;
                    case "QR_left": if (int.TryParse(val, out int ql)) layout.QrLeft = ql; break;
                    case "QR_top": if (int.TryParse(val, out int qt)) layout.QrTop = qt; break;
                    case "QR_size": if (int.TryParse(val, out int qs)) layout.QrSize = qs; break;
                    case "Code_left": if (int.TryParse(val, out int cl)) layout.CodeLeft = cl; break;
                    case "Code_top": if (int.TryParse(val, out int ct)) layout.CodeTop = ct; break;
                    case "Code_fontSize": if (int.TryParse(val, out int cf)) layout.CodeFontSize = cf; break;
                    case "Code_color": layout.CodeColor = val; break;
                    case "Name_left": if (int.TryParse(val, out int nl)) layout.NameLeft = nl; break;
                    case "Name_top": if (int.TryParse(val, out int nt)) layout.NameTop = nt; break;
                    case "Name_fontSize": if (int.TryParse(val, out int nf)) layout.NameFontSize = nf; break;
                    case "Name_color": layout.NameColor = val; break;
                    case "Error_fontSize": if (int.TryParse(val, out int ef)) layout.ErrorFontSize = ef; break;
                }
            }

            Log.Write($"레이아웃 로딩: {section}");
            return layout;
        }

        static LayoutConfig GetDefault(string deviceType)
        {
            return deviceType switch
            {
                "A" => new LayoutConfig
                {
                    Screen1Height = 1080, GuideHeight = 0, Screen2Height = 0,
                    QrLeft = 156, QrTop = 575, QrSize = 167,
                    CodeLeft = 170, CodeTop = 755, CodeFontSize = 60, CodeColor = "#333333",
                    NameLeft = 156, NameTop = 820, NameFontSize = 24, NameColor = "#FFFFFF",
                    ErrorFontSize = 25
                },
                "B" => new LayoutConfig
                {
                    Screen1Height = 768, GuideHeight = 512, Screen2Height = 0,
                    QrLeft = 792, QrTop = 30, QrSize = 167,
                    CodeLeft = 810, CodeTop = 210, CodeFontSize = 60, CodeColor = "#333333",
                    NameLeft = 780, NameTop = 280, NameFontSize = 24, NameColor = "#FFFFFF",
                    ErrorFontSize = 25
                },
                _ => new LayoutConfig() // Type_C 기본값
            };
        }
    }
}
