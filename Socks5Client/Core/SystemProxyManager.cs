using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Socks5Client.Logs;

namespace Socks5Client.Core
{
    /// <summary>
    /// 系统代理管理器，负责设置和恢复Windows系统代理
    /// </summary>
    public class SystemProxyManager
    {
        private string? _originalProxyServer;
        private bool _originalProxyEnabled = false;
        private bool _proxyModified = false;

        /// <summary>
        /// 设置系统代理
        /// </summary>
        public bool SetSystemProxy(string proxyServer, int port)
        {
            try
            {
                // 保存原始设置
                if (!_proxyModified)
                {
                    SaveOriginalProxySettings();
                }

                var proxyAddress = $"{proxyServer}:{port}";

                // 设置代理服务器
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        key.SetValue("ProxyServer", proxyAddress);
                        key.SetValue("ProxyEnable", 1);
                        key.SetValue("ProxyOverride", "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*");
                    }
                }

                // 通知系统设置已更改
                // INTERNET_OPTION_SETTINGS_CHANGED = 39
                // INTERNET_OPTION_REFRESH = 37
                InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
                
                // 发送 WM_SETTINGCHANGE 消息通知所有应用程序
                SendMessageTimeout(
                    new IntPtr(0xFFFF), // HWND_BROADCAST
                    0x001A, // WM_SETTINGCHANGE
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0x0002, // SMTO_ABORTIFHUNG
                    5000,
                    out _);

                _proxyModified = true;
                Logger.Info($"系统代理已设置: {proxyAddress} (这是本地代理服务器，将转发到远程代理)");
                Logger.Info($"注意: 系统代理显示的是本地地址 {proxyAddress}，这是正常的。实际代理服务器由应用程序管理。");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"设置系统代理失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 恢复系统代理设置
        /// </summary>
        public bool RestoreSystemProxy()
        {
            if (!_proxyModified)
            {
                return true;
            }

            return RestoreSystemProxyInternal();
        }

        /// <summary>
        /// 强制恢复系统代理设置（即使不知道之前是否修改过）
        /// 用于程序退出时的保险措施
        /// </summary>
        public static bool ForceRestoreSystemProxy()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        // 检查当前代理设置是否是本地代理（127.0.0.1）
                        var currentProxy = key.GetValue("ProxyServer")?.ToString();
                        var proxyEnabled = key.GetValue("ProxyEnable");
                        bool isEnabled = proxyEnabled != null && Convert.ToInt32(proxyEnabled) == 1;

                        // 如果代理指向本地地址（可能是我们的程序设置的），则禁用它
                        if (isEnabled && !string.IsNullOrEmpty(currentProxy) && 
                            (currentProxy.Contains("127.0.0.1") || currentProxy.Contains("localhost")))
                        {
                            // 禁用代理（设置为原始状态：未启用）
                            key.SetValue("ProxyEnable", 0);
                            
                            // 通知系统设置已更改
                            InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
                            InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
                            
                            // 发送 WM_SETTINGCHANGE 消息通知所有应用程序
                            SendMessageTimeout(
                                new IntPtr(0xFFFF), // HWND_BROADCAST
                                0x001A, // WM_SETTINGCHANGE
                                IntPtr.Zero,
                                IntPtr.Zero,
                                0x0002, // SMTO_ABORTIFHUNG
                                5000,
                                out _);

                            Logger.Info("系统代理已强制恢复（程序退出时的保险措施）");
                            return true;
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"强制恢复系统代理设置失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 内部恢复系统代理的方法
        /// </summary>
        private bool RestoreSystemProxyInternal()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        if (_originalProxyServer != null)
                        {
                            key.SetValue("ProxyServer", _originalProxyServer);
                        }
                        key.SetValue("ProxyEnable", _originalProxyEnabled ? 1 : 0);
                    }
                }

                // 通知系统设置已更改
                InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
                
                // 发送 WM_SETTINGCHANGE 消息通知所有应用程序
                SendMessageTimeout(
                    new IntPtr(0xFFFF), // HWND_BROADCAST
                    0x001A, // WM_SETTINGCHANGE
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0x0002, // SMTO_ABORTIFHUNG
                    5000,
                    out _);

                _proxyModified = false;
                Logger.Info("系统代理设置已恢复");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"恢复系统代理设置失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 保存原始代理设置
        /// </summary>
        private void SaveOriginalProxySettings()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (key != null)
                    {
                        _originalProxyServer = key.GetValue("ProxyServer")?.ToString();
                        var proxyEnable = key.GetValue("ProxyEnable");
                        _originalProxyEnabled = proxyEnable != null && Convert.ToInt32(proxyEnable) == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"保存原始代理设置失败: {ex.Message}", ex);
            }
        }

        [DllImport("wininet.dll")]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);
    }
}

