using System;
using System.IO;

namespace selpic_new
{
    static class Log
    {
        static string logFolder = "";

        public static void Init(string baseDir)
        {
            logFolder = Path.Combine(baseDir, "log");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);
        }

        public static void Write(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{time}] {message}";
            Console.WriteLine(line);

            try
            {
                string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                string filePath = Path.Combine(logFolder, fileName);
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
            catch { }
        }

        public static void Error(string message, Exception? ex = null)
        {
            string detail = ex != null ? $"{message}: {ex.Message}" : message;
            Write($"[오류] {detail}");
        }
    }
}