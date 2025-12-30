using System.Windows;
using Socks5Client.Core;
using Socks5Client.Model;
using Socks5Client.UI.ViewModels;

namespace Socks5Client.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private TrayIconManager? _trayIconManager;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // 订阅事件
            _viewModel.OnAddNodeRequested += ShowAddNodeDialog;
            _viewModel.OnEditNodeRequested += ShowEditNodeDialog;

            // 初始化系统托盘
            var configManager = ConfigManager.Instance;
            if (configManager.Settings.EnableTrayIcon)
            {
                _trayIconManager = new TrayIconManager();
                _trayIconManager.Initialize(this);
            }
        }

        private void ShowAddNodeDialog()
        {
            var dialog = new NodeEditWindow();
            if (dialog.ShowDialog() == true && dialog.SavedNode != null)
            {
                // 使用从OnSave事件中返回的节点，确保密码正确
                ConfigManager.Instance.AddProxyNode(dialog.SavedNode);
                _viewModel.LoadNodes();
            }
        }

        private void ShowEditNodeDialog(ProxyNode node)
        {
            var dialog = new NodeEditWindow(node);
            if (dialog.ShowDialog() == true && dialog.SavedNode != null)
            {
                // 使用从OnSave事件中返回的节点，确保密码正确
                ConfigManager.Instance.UpdateProxyNode(dialog.SavedNode);
                _viewModel.LoadNodes();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var configManager = ConfigManager.Instance;
            
            // 如果启用了最小化到托盘，则隐藏窗口而不是关闭
            if (configManager.Settings.MinimizeToTray && configManager.Settings.EnableTrayIcon)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                Hide();
                ShowInTaskbar = false;
                return;
            }

            // 断开代理连接（这会自动恢复系统代理）
            if (_viewModel.IsConnected)
            {
                var result = MessageBox.Show("代理正在运行，是否断开并退出？", "确认", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                
                // 用户确认退出，断开代理连接
                // Disconnect() 方法会自动恢复系统代理
                _viewModel.Disconnect();
            }
            else
            {
                // 即使没有连接，也确保系统代理被恢复（以防万一）
                // 这会在 App.OnExit 中再次确保恢复
            }

            _trayIconManager?.Dispose();
            base.OnClosing(e);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
        }

        private void ToggleLogsButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowLogs = !_viewModel.ShowLogs;
            if (_viewModel.ShowLogs)
            {
                ((System.Windows.Controls.Button)sender).Content = "隐藏日志";
            }
            else
            {
                ((System.Windows.Controls.Button)sender).Content = "显示日志";
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearLogs();
        }
    }
}

