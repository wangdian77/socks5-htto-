using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using Application = System.Windows.Application;

namespace Socks5Client.Core
{
    /// <summary>
    /// 系统托盘图标管理器
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private Window? _mainWindow;
        private bool _isDisposed = false;

        /// <summary>
        /// 初始化系统托盘
        /// </summary>
        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // 使用默认图标，实际应该使用应用图标
                Text = "SOCKS5/HTTP代理客户端",
                Visible = true
            };

            // 创建上下文菜单
            var contextMenu = new ContextMenuStrip();
            
            var showMenuItem = new ToolStripMenuItem("显示窗口");
            showMenuItem.Click += (s, e) => ShowWindow();
            contextMenu.Items.Add(showMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitMenuItem = new ToolStripMenuItem("退出");
            exitMenuItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            // 处理窗口状态变化
            if (_mainWindow != null)
            {
                _mainWindow.StateChanged += MainWindow_StateChanged;
                _mainWindow.Closing += MainWindow_Closing;
            }
        }

        /// <summary>
        /// 窗口状态变化处理
        /// </summary>
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_mainWindow == null) return;

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                // 最小化到托盘
                _mainWindow.Hide();
                _mainWindow.ShowInTaskbar = false;
            }
        }

        /// <summary>
        /// 窗口关闭处理
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_mainWindow == null) return;

            // 如果启用了最小化到托盘，则隐藏窗口而不是关闭
            var configManager = Model.ConfigManager.Instance;
            if (configManager.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                _mainWindow.WindowState = WindowState.Minimized;
                _mainWindow.Hide();
                _mainWindow.ShowInTaskbar = false;
            }
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        public void ShowWindow()
        {
            if (_mainWindow == null) return;

            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Activate();
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        private void ExitApplication()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Closing -= MainWindow_Closing;
                _mainWindow.Close();
            }
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 更新托盘图标提示文本
        /// </summary>
        public void UpdateToolTip(string text)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = text;
            }
        }

        /// <summary>
        /// 显示托盘通知
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.BalloonTipIcon = icon;
                _notifyIcon.ShowBalloonTip(3000);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            if (_mainWindow != null)
            {
                _mainWindow.StateChanged -= MainWindow_StateChanged;
                _mainWindow.Closing -= MainWindow_Closing;
            }

            _isDisposed = true;
        }
    }
}

