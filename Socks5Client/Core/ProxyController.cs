using System;
using System.Threading.Tasks;
using Socks5Client.Logs;
using Socks5Client.Model;

namespace Socks5Client.Core
{
    /// <summary>
    /// 代理控制器，管理代理的启停和切换
    /// </summary>
    public class ProxyController
    {
        private Socks5Engine? _socks5Engine;
        private HttpProxyEngine? _httpEngine;
        private HttpToSocks5Adapter? _httpToSocks5Adapter;
        private SystemProxyManager? _systemProxyManager;
        private ProxyNode? _currentNode;
        private readonly ConfigManager _configManager;
        private bool _isEnabled = false;

        /// <summary>
        /// 当前连接的节点
        /// </summary>
        public ProxyNode? CurrentNode => _currentNode;

        /// <summary>
        /// 是否已启用代理
        /// </summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// 代理状态变化事件
        /// </summary>
        public event EventHandler<ProxyStatusChangedEventArgs>? StatusChanged;

        public ProxyController()
        {
            _configManager = ConfigManager.Instance;
        }

        /// <summary>
        /// 连接到指定的代理节点
        /// </summary>
        public async Task<bool> ConnectAsync(ProxyNode node, bool enableSystemProxy = true)
        {
            try
            {
                // 如果已连接，先断开
                if (_isEnabled)
                {
                    Disconnect();
                }

                Logger.Info($"正在连接到节点: {node.Name} ({node.Server}:{node.Port}) - {node.Type}");

                _currentNode = node;
                _systemProxyManager = new SystemProxyManager();

                var connectionInfo = new ProxyConnectionInfo
                {
                    Server = node.Server,
                    Port = node.Port,
                    Username = node.Username,
                    Password = node.Password
                };

                // 记录连接信息（用于调试）
                Logger.Info($"连接信息 - 服务器: {connectionInfo.Server}:{connectionInfo.Port}, " +
                           $"用户名: {(string.IsNullOrEmpty(connectionInfo.Username) ? "(空)" : $"{connectionInfo.Username.Length}字符")}, " +
                           $"密码: {(string.IsNullOrEmpty(connectionInfo.Password) ? "(空)" : $"{connectionInfo.Password.Length}字符")}");

                var localPort = _configManager.Settings.LocalPort;
                bool success = false;

                // 根据代理类型启动相应的服务
                if (node.Type == Model.ProxyType.SOCKS5)
                {
                    // 对于SOCKS5代理，使用HTTP到SOCKS5适配器
                    // 这样浏览器可以通过HTTP代理协议使用SOCKS5代理
                    _httpToSocks5Adapter = new HttpToSocks5Adapter();
                    success = await _httpToSocks5Adapter.StartAsync(connectionInfo, localPort);
                }
                else if (node.Type == Model.ProxyType.HTTP)
                {
                    _httpEngine = new HttpProxyEngine();
                    success = await _httpEngine.StartAsync(connectionInfo, localPort);
                }

                if (!success)
                {
                    Logger.Error($"启动代理服务失败: {node.Name}");
                    _currentNode = null;
                    OnStatusChanged(false, node, "启动代理服务失败");
                    return false;
                }

                // 设置系统代理（始终使用HTTP代理端口，因为Windows系统代理只支持HTTP/HTTPS）
                // 系统代理设置为本地地址，本地代理服务器会将请求转发到远程代理
                if (enableSystemProxy && _configManager.Settings.EnableSystemProxy)
                {
                    Logger.Info($"设置系统代理为本地地址: 127.0.0.1:{localPort} (将转发到远程代理 {node.Server}:{node.Port})");
                    _systemProxyManager.SetSystemProxy("127.0.0.1", localPort);
                }
                else
                {
                    Logger.Info("系统代理未启用，仅本地代理服务运行");
                }

                _isEnabled = true;
                node.IsConnected = true;

                Logger.Info($"代理连接成功: {node.Name}");
                OnStatusChanged(true, node, "连接成功");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"连接代理节点失败: {node.Name}", ex);
                _currentNode = null;
                _isEnabled = false;
                OnStatusChanged(false, node, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 断开代理连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_socks5Engine != null)
                {
                    _socks5Engine.Stop();
                    _socks5Engine.Dispose();
                    _socks5Engine = null;
                }

                if (_httpEngine != null)
                {
                    _httpEngine.Stop();
                    _httpEngine.Dispose();
                    _httpEngine = null;
                }

                if (_httpToSocks5Adapter != null)
                {
                    _httpToSocks5Adapter.Stop();
                    _httpToSocks5Adapter.Dispose();
                    _httpToSocks5Adapter = null;
                }

                if (_systemProxyManager != null)
                {
                    _systemProxyManager.RestoreSystemProxy();
                    _systemProxyManager = null;
                }

                if (_currentNode != null)
                {
                    _currentNode.IsConnected = false;
                    var node = _currentNode;
                    _currentNode = null;
                    Logger.Info($"代理已断开: {node.Name}");
                    OnStatusChanged(false, node, "已断开");
                }

                _isEnabled = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"断开代理连接失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 切换代理节点
        /// </summary>
        public async Task<bool> SwitchNodeAsync(ProxyNode newNode)
        {
            var oldNode = _currentNode;
            Disconnect();
            await Task.Delay(500); // 等待清理完成
            return await ConnectAsync(newNode);
        }

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        private void OnStatusChanged(bool isConnected, ProxyNode? node, string message)
        {
            StatusChanged?.Invoke(this, new ProxyStatusChangedEventArgs
            {
                IsConnected = isConnected,
                Node = node,
                Message = message
            });
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 代理状态变化事件参数
    /// </summary>
    public class ProxyStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public ProxyNode? Node { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

