using System;
using System.Windows;
using Socks5Client.Core;
using Socks5Client.Logs;

namespace Socks5Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Logger.Info("应用程序启动");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info("应用程序退出，正在恢复系统代理设置...");
            
            // 确保在程序退出时恢复系统代理
            // 使用静态方法强制恢复，作为保险措施
            // 即使 ProxyController 已经恢复了，这里也会再次确保恢复
            try
            {
                SystemProxyManager.ForceRestoreSystemProxy();
            }
            catch (Exception ex)
            {
                Logger.Error($"恢复系统代理失败: {ex.Message}", ex);
            }
            
            base.OnExit(e);
        }
    }
}

