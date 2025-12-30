using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Socks5Client.Core;
using Socks5Client.Logs;
using Socks5Client.Model;

namespace Socks5Client.UI.ViewModels
{
    /// <summary>
    /// 主窗口ViewModel
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ProxyController _proxyController;
        private readonly ConfigManager _configManager;
        private readonly SpeedTester _speedTester;
        private ProxyNode? _selectedNode;
        private string _statusMessage = "未连接";
        private bool _isConnected = false;
        private long _uploadBytes = 0;
        private long _downloadBytes = 0;
        private readonly Dispatcher _dispatcher;

        public MainViewModel()
        {
            _proxyController = new ProxyController();
            _configManager = ConfigManager.Instance;
            _speedTester = new SpeedTester();
            _dispatcher = Dispatcher.CurrentDispatcher;

            // 加载节点列表
            LoadNodes();

            // 订阅代理状态变化
            _proxyController.StatusChanged += OnProxyStatusChanged;

            // 订阅日志事件
            Logger.LogAdded += OnLogAdded;

            // 加载最近的日志
            LoadRecentLogs();

            // 初始化命令
            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => SelectedNode != null && !IsConnected);
            DisconnectCommand = new RelayCommand(() => Disconnect(), () => IsConnected);
            AddNodeCommand = new RelayCommand(() => OnAddNodeRequested?.Invoke());
            EditNodeCommand = new RelayCommand(() => OnEditNodeRequested?.Invoke(SelectedNode!), () => SelectedNode != null);
            DeleteNodeCommand = new RelayCommand(() => DeleteNode(), () => SelectedNode != null);
            TestLatencyCommand = new RelayCommand(async () => await TestLatencyAsync(), () => SelectedNode != null);
            TestAllNodesCommand = new RelayCommand(async () => await TestAllNodesAsync());
        }

        /// <summary>
        /// 代理节点列表
        /// </summary>
        public ObservableCollection<ProxyNode> ProxyNodes { get; } = new ObservableCollection<ProxyNode>();

        /// <summary>
        /// 日志条目列表
        /// </summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        /// <summary>
        /// 是否显示日志面板
        /// </summary>
        private bool _showLogs = false;
        public bool ShowLogs
        {
            get => _showLogs;
            set
            {
                _showLogs = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 选中的节点
        /// </summary>
        public ProxyNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                OnPropertyChanged();
                ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
                ((RelayCommand)EditNodeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteNodeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)TestLatencyCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
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
                ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 上传字节数
        /// </summary>
        public long UploadBytes
        {
            get => _uploadBytes;
            set
            {
                _uploadBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UploadSpeedDisplay));
            }
        }

        /// <summary>
        /// 下载字节数
        /// </summary>
        public long DownloadBytes
        {
            get => _downloadBytes;
            set
            {
                _downloadBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DownloadSpeedDisplay));
            }
        }

        /// <summary>
        /// 上传速度显示
        /// </summary>
        public string UploadSpeedDisplay => FormatBytes(UploadBytes);

        /// <summary>
        /// 下载速度显示
        /// </summary>
        public string DownloadSpeedDisplay => FormatBytes(DownloadBytes);

        /// <summary>
        /// 连接命令
        /// </summary>
        public ICommand ConnectCommand { get; }

        /// <summary>
        /// 断开命令
        /// </summary>
        public ICommand DisconnectCommand { get; }

        /// <summary>
        /// 添加节点命令
        /// </summary>
        public ICommand AddNodeCommand { get; }

        /// <summary>
        /// 编辑节点命令
        /// </summary>
        public ICommand EditNodeCommand { get; }

        /// <summary>
        /// 删除节点命令
        /// </summary>
        public ICommand DeleteNodeCommand { get; }

        /// <summary>
        /// 测试延迟命令
        /// </summary>
        public ICommand TestLatencyCommand { get; }

        /// <summary>
        /// 测试所有节点命令
        /// </summary>
        public ICommand TestAllNodesCommand { get; }

        /// <summary>
        /// 添加节点请求事件
        /// </summary>
        public event Action? OnAddNodeRequested;

        /// <summary>
        /// 编辑节点请求事件
        /// </summary>
        public event Action<ProxyNode>? OnEditNodeRequested;

        /// <summary>
        /// 加载节点列表
        /// </summary>
        public void LoadNodes()
        {
            ProxyNodes.Clear();
            foreach (var node in _configManager.GetProxyNodes())
            {
                ProxyNodes.Add(node);
            }
        }

        /// <summary>
        /// 连接代理
        /// </summary>
        private async Task ConnectAsync()
        {
            if (SelectedNode == null) return;

            StatusMessage = "正在连接...";
            var success = await _proxyController.ConnectAsync(SelectedNode);
            if (!success)
            {
                StatusMessage = "连接失败";
            }
        }

        /// <summary>
        /// 断开代理
        /// </summary>
        public void Disconnect()
        {
            _proxyController.Disconnect();
            StatusMessage = "已断开";
        }

        /// <summary>
        /// 删除节点
        /// </summary>
        private void DeleteNode()
        {
            if (SelectedNode == null) return;

            if (SelectedNode.IsConnected)
            {
                Disconnect();
            }

            _configManager.RemoveProxyNode(SelectedNode.Id);
            ProxyNodes.Remove(SelectedNode);
            SelectedNode = null;
        }

        /// <summary>
        /// 测试延迟
        /// </summary>
        private async Task TestLatencyAsync()
        {
            if (SelectedNode == null) return;

            StatusMessage = $"正在测试 {SelectedNode.Name}...";
            await _speedTester.TestLatencyAsync(SelectedNode);
            StatusMessage = $"测试完成: {SelectedNode.LatencyDisplay}";
        }

        /// <summary>
        /// 测试所有节点延迟
        /// </summary>
        private async Task TestAllNodesAsync()
        {
            StatusMessage = "正在测试所有节点...";
            var nodes = ProxyNodes.ToList();
            await _speedTester.TestMultipleNodesAsync(nodes, new Progress<TestProgress>(progress =>
            {
                StatusMessage = $"正在测试 ({progress.Completed}/{progress.Total})...";
            }));
            StatusMessage = "所有节点测试完成";
        }

        /// <summary>
        /// 代理状态变化处理
        /// </summary>
        private void OnProxyStatusChanged(object? sender, ProxyStatusChangedEventArgs e)
        {
            IsConnected = e.IsConnected;
            StatusMessage = e.Message;

            // 更新节点连接状态
            if (e.Node != null)
            {
                foreach (var node in ProxyNodes)
                {
                    node.IsConnected = node.Id == e.Node.Id && e.IsConnected;
                }
            }
        }

        /// <summary>
        /// 加载最近的日志
        /// </summary>
        private void LoadRecentLogs()
        {
            var recentLogs = Logger.GetRecentLogs(100);
            foreach (var log in recentLogs)
            {
                LogEntries.Add(log);
            }
        }

        /// <summary>
        /// 日志添加事件处理
        /// </summary>
        private void OnLogAdded(object? sender, LogEntry entry)
        {
            // 在UI线程上添加日志
            _dispatcher.Invoke(() =>
            {
                LogEntries.Add(entry);
                
                // 限制日志数量（最多保留500条）
                while (LogEntries.Count > 500)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// 清除日志
        /// </summary>
        public void ClearLogs()
        {
            LogEntries.Clear();
            Logger.Clear();
        }

        /// <summary>
        /// 格式化字节数
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 简单的RelayCommand实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

