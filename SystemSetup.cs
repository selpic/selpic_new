using System;
using System.IO;
using Microsoft.Win32;

namespace selpic_new
{
    public static class SystemSetup
    {
        static string ExePath => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
        static string ExeDir => AppDomain.CurrentDomain.BaseDirectory;

        // ============================================================
        // 바탕화면 바로가기
        // ============================================================
        public static void CreateDesktopShortcut()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string shortcutPath = Path.Combine(desktopPath, "SELPIC.lnk");

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    Log.Write("WScript.Shell 사용 불가");
                    return;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = ExePath;
                shortcut.WorkingDirectory = ExeDir;
                shortcut.Description = "SELPIC PDF 인쇄 키오스크";
                shortcut.Save();

                Log.Write($"바탕화면 바로가기 생성: {shortcutPath}");
            }
            catch (Exception ex)
            {
                Log.Error("바로가기 생성 실패", ex);
            }
        }

        public static void RemoveDesktopShortcut()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string shortcutPath = Path.Combine(desktopPath, "SELPIC.lnk");
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                    Log.Write("바탕화면 바로가기 삭제");
                }
            }
            catch (Exception ex)
            {
                Log.Error("바로가기 삭제 실패", ex);
            }
        }

        public static bool HasDesktopShortcut()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            return File.Exists(Path.Combine(desktopPath, "SELPIC.lnk"));
        }

        // ============================================================
        // 부팅 시 자동실행 (레지스트리)
        // ============================================================
        public static void EnableAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue("SELPIC", $"\"{ExePath}\"");
                Log.Write("자동실행 등록 완료");
            }
            catch (Exception ex)
            {
                Log.Error("자동실행 등록 실패", ex);
            }
        }

        public static void DisableAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue("SELPIC", false);
                Log.Write("자동실행 해제 완료");
            }
            catch (Exception ex)
            {
                Log.Error("자동실행 해제 실패", ex);
            }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("SELPIC") != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
