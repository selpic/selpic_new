using System;
using System.Windows;

namespace selpic_new
{
    public partial class SetupWindow : Window
    {
        private Config config;
        public bool ShouldRestart { get; private set; } = false;

        public SetupWindow(Config config)
        {
            InitializeComponent();
            this.config = config;
            LoadConfig();
        }

        void LoadConfig()
        {
            BranchCodeInput.Text = config.BranchCode;
            if (!string.IsNullOrEmpty(config.BranchCode))
            {
                BranchInfoText.Text = $"{config.BranchCode}\r\n{config.BranchName}";
            }

            // 기기 타입
            for (int i = 0; i < DeviceTypeCombo.Items.Count; i++)
            {
                var item = DeviceTypeCombo.Items[i] as System.Windows.Controls.ComboBoxItem;
                if (item != null && item.Tag?.ToString() == config.DeviceType)
                {
                    DeviceTypeCombo.SelectedIndex = i;
                    break;
                }
            }

            // 프린터 선택
            for (int i = 0; i < PrinterCombo.Items.Count; i++)
            {
                var item = PrinterCombo.Items[i] as System.Windows.Controls.ComboBoxItem;
                if (item != null && item.Content.ToString() == config.PrinterType)
                {
                    PrinterCombo.SelectedIndex = i;
                    break;
                }
            }

            GhostScriptInput.Text = config.GhostScriptPath;
            PaymentCodeInput.Text = config.PaymentCode;
            TerminalCodeInput.Text = config.TerminalCode;
            PollIntervalInput.Text = config.PollInterval.ToString();
            RetryCountInput.Text = config.RetryCount.ToString();

            // 바로가기 / 자동실행 현재 상태
            ShortcutCheck.IsChecked = SystemSetup.HasDesktopShortcut();
            AutoStartCheck.IsChecked = SystemSetup.IsAutoStartEnabled();
        }

        async void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            string code = BranchCodeInput.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("지점코드를 입력하세요.", "SELPIC");
                return;
            }

            SearchBtn.IsEnabled = false;
            SearchBtn.Content = "검색중...";

            try
            {
                string name = await ApiClient.SearchBranch(code);
                if (!string.IsNullOrEmpty(name))
                {
                    BranchInfoText.Text = $"{code}\r\n{name}";
                    config.BranchCode = code;
                    config.BranchName = name;
                }
                else
                {
                    BranchInfoText.Text = "";
                    MessageBox.Show("등록된 지점을 찾을 수 없습니다.", "SELPIC");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"검색 오류: {ex.Message}", "SELPIC");
            }

            SearchBtn.IsEnabled = true;
            SearchBtn.Content = "검색";
        }

        bool SaveConfig()
        {
            if (string.IsNullOrEmpty(config.BranchCode))
            {
                MessageBox.Show("지점코드를 검색해주세요.", "SELPIC");
                return false;
            }

            // 기기 타입
            var deviceItem = DeviceTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (deviceItem != null)
                config.DeviceType = deviceItem.Tag?.ToString() ?? "C";

            // 프린터
            var selectedItem = PrinterCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem != null)
                config.PrinterType = selectedItem.Content.ToString() ?? "Brother HL-L9430CDN series";

            // GhostScript
            config.GhostScriptPath = GhostScriptInput.Text.Trim();

            // 결제
            config.PaymentCode = PaymentCodeInput.Text.Trim();
            config.TerminalCode = TerminalCodeInput.Text.Trim();

            // 시스템
            if (int.TryParse(PollIntervalInput.Text, out int interval) && interval > 0)
                config.PollInterval = interval;
            if (int.TryParse(RetryCountInput.Text, out int retry) && retry > 0)
                config.RetryCount = retry;

            config.Save();

            // 바로가기
            if (ShortcutCheck.IsChecked == true)
                SystemSetup.CreateDesktopShortcut();
            else
                SystemSetup.RemoveDesktopShortcut();

            // 자동실행
            if (AutoStartCheck.IsChecked == true)
                SystemSetup.EnableAutoStart();
            else
                SystemSetup.DisableAutoStart();

            return true;
        }

        void RestartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveConfig()) return;
            ShouldRestart = true;
            MessageBox.Show("설정이 저장되었습니다.\n프로그램을 재시작합니다.", "SELPIC");
            DialogResult = true;
            Close();
        }

        void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveConfig()) return;
            ShouldRestart = false;
            MessageBox.Show("설정이 저장되었습니다.\n프로그램을 종료합니다.", "SELPIC");
            DialogResult = true;
            Close();
        }

        void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}