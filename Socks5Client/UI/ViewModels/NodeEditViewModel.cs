using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Socks5Client.Model;

namespace Socks5Client.UI.ViewModels
{
    /// <summary>
    /// 节点编辑ViewModel
    /// </summary>
    public class NodeEditViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _server = string.Empty;
        private int _port = 1080;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private ProxyType _proxyType = ProxyType.SOCKS5;
        private ProxyNode? _originalNode;

        public NodeEditViewModel(ProxyNode? node = null)
        {
            // 先初始化命令，避免在设置属性时出现 NullReferenceException
            // 注意：Save() 方法现在需要从外部传入密码，所以这里调用 Save() 时密码可能为空
            // 但 OnSave 事件处理程序会从 PasswordBox 读取密码并更新节点
            SaveCommand = new RelayCommand(() => Save(), () => IsValid());
            CancelCommand = new RelayCommand(() => OnCancel?.Invoke());

            // 然后设置属性值
            if (node != null)
            {
                _originalNode = node;
                // 直接设置私有字段，避免触发属性 setter（因为命令已经初始化，现在可以安全触发）
                _name = node.Name;
                _server = node.Server;
                _port = node.Port;
                _username = node.Username;
                _password = node.Password;
                _proxyType = node.Type;
                
                // 触发属性变更通知，但不触发命令更新（因为值已经设置好了）
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Server));
                OnPropertyChanged(nameof(Port));
                OnPropertyChanged(nameof(Username));
                OnPropertyChanged(nameof(Password));
                OnPropertyChanged(nameof(ProxyType));
                OnPropertyChanged(nameof(SelectedProxyType));
            }
        }

        /// <summary>
        /// 节点名称
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
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
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 端口
        /// </summary>
        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged();
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 用户名
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
        /// 密码
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
        public ProxyType ProxyType
        {
            get => _proxyType;
            set
            {
                _proxyType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedProxyType));
            }
        }

        /// <summary>
        /// 选中的代理类型（用于ComboBox绑定）
        /// </summary>
        public string SelectedProxyType
        {
            get => _proxyType.ToString();
            set
            {
                if (Enum.TryParse<ProxyType>(value, out var type))
                {
                    ProxyType = type;
                }
            }
        }

        /// <summary>
        /// 是否为编辑模式
        /// </summary>
        public bool IsEditMode => _originalNode != null;

        /// <summary>
        /// 保存命令
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// 保存完成事件
        /// </summary>
        public event Action<ProxyNode>? OnSave;

        /// <summary>
        /// 取消事件
        /// </summary>
        public event Action? OnCancel;

        /// <summary>
        /// 验证输入是否有效
        /// </summary>
        private bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(Server) &&
                   Port > 0 && Port <= 65535;
        }

        /// <summary>
        /// 保存节点
        /// </summary>
        private void Save()
        {
            if (!IsValid()) return;

            ProxyNode node;
            if (_originalNode != null)
            {
                // 更新现有节点
                node = _originalNode;
                node.Name = Name;
                node.Server = Server;
                node.Port = Port;
                node.Username = Username;
                // 密码将在 NodeEditWindow.OnSave 中从 PasswordBox 读取并更新
                // 这里先设置为空，避免使用可能过时的 ViewModel.Password
                node.Password = string.Empty;
                node.Type = ProxyType;
            }
            else
            {
                // 创建新节点
                node = new ProxyNode
                {
                    Name = Name,
                    Server = Server,
                    Port = Port,
                    Username = Username,
                    // 密码将在 NodeEditWindow.OnSave 中从 PasswordBox 读取并更新
                    // 这里先设置为空，避免使用可能过时的 ViewModel.Password
                    Password = string.Empty,
                    Type = ProxyType
                };
            }

            // 触发 OnSave 事件，让 NodeEditWindow 从 PasswordBox 读取密码并更新节点
            OnSave?.Invoke(node);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

