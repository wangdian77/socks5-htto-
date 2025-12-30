using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Socks5Client.Model
{
    /// <summary>
    /// 代理类型
    /// </summary>
    public enum ProxyType
    {
        SOCKS5,
        HTTP
    }

    /// <summary>
    /// 代理节点数据模型
    /// </summary>
    public class ProxyNode : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _server = string.Empty;
        private int _port = 1080;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private ProxyType _proxyType = ProxyType.SOCKS5;
        private int _latency = -1;
        private bool _isConnected = false;
        private DateTime _lastTestTime = DateTime.MinValue;

        /// <summary>
        /// 节点名称/别名
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 服务器地址
        /// </summary>
        public string Server
        {
            get => _server;
            set
            {
                _server = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 用户名（可选）
        /// </summary>
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 密码（可选）
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 代理类型
        /// </summary>
        public ProxyType Type
        {
            get => _proxyType;
            set
            {
                _proxyType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 延迟（毫秒），-1表示未测试
        /// </summary>
        public int Latency
        {
            get => _latency;
            set
            {
                _latency = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LatencyDisplay));
            }
        }

        /// <summary>
        /// 延迟显示文本
        /// </summary>
        public string LatencyDisplay
        {
            get
            {
                if (_latency < 0) return "未测试";
                return $"{_latency}ms";
            }
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 最后测试时间
        /// </summary>
        public DateTime LastTestTime
        {
            get => _lastTestTime;
            set
            {
                _lastTestTime = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 唯一标识符
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 订阅源ID（如果来自订阅）
        /// </summary>
        public string? SubscriptionId { get; set; }

        /// <summary>
        /// 验证节点配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Server) &&
                   Port > 0 && Port <= 65535 &&
                   (!string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password));
        }

        /// <summary>
        /// 创建节点副本
        /// </summary>
        public ProxyNode Clone()
        {
            return new ProxyNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name,
                Server = Server,
                Port = Port,
                Username = Username,
                Password = Password,
                Type = Type,
                SubscriptionId = SubscriptionId
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

