using System.Windows;

namespace WpfClipboardCapture
{
    public partial class SettingsWindow : Window
    {
        public int HistoryLimit { get; private set; }

        public SettingsWindow(int currentLimit)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            historyLimitTextBox.Text = currentLimit.ToString();
            HistoryLimit = currentLimit;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(historyLimitTextBox.Text, out int newLimit))
            {
                if (newLimit >= 3)
                {
                    HistoryLimit = newLimit;
                    DialogResult = true;
                }
                else
                {
                    MessageBox.Show("최소 3개 이상이어야 합니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("유효한 숫자를 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}