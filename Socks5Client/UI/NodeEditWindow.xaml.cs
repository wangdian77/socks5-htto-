using System.Windows;
using Socks5Client.Model;
using Socks5Client.UI.ViewModels;

namespace Socks5Client.UI
{
    /// <summary>
    /// Interaction logic for NodeEditWindow.xaml
    /// </summary>
    public partial class NodeEditWindow : Window
    {
        public NodeEditViewModel ViewModel { get; }
        
        /// <summary>
        /// 保存的节点（在保存后可用）
        /// </summary>
        public ProxyNode? SavedNode { get; private set; }

        public NodeEditWindow(ProxyNode? node = null)
        {
            InitializeComponent();
            ViewModel = new NodeEditViewModel(node);
            DataContext = ViewModel;

            // 如果是编辑模式，设置密码和代理类型
            if (node != null)
            {
                if (!string.IsNullOrEmpty(node.Password))
                {
                    PasswordBox.Password = node.Password;
                }
            }

            // 订阅保存事件
            ViewModel.OnSave += OnSave;
            ViewModel.OnCancel += () => DialogResult = false;
        }

        private void OnSave(ProxyNode node)
        {
            // 从 PasswordBox 读取密码并更新节点
            string passwordFromBox = PasswordBox.Password;
            node.Password = passwordFromBox;
            
            // 同时更新 ViewModel 的 Password 属性，确保一致性
            ViewModel.Password = passwordFromBox;
            
            // 保存节点以便外部访问
            SavedNode = node;
            
            // 记录日志以便调试（不记录实际密码内容）
            System.Diagnostics.Debug.WriteLine($"NodeEditWindow.OnSave: 密码长度 = {passwordFromBox?.Length ?? 0}");
            
            DialogResult = true;
            Close();
        }
    }
}

