# 项目结构说明

本项目是一个 Windows 平台的 SOCKS5 代理客户端，采用 C# + WPF 开发。

## 目录结构

```
socks5/
├── Socks5Client.sln              # Visual Studio 解决方案文件
├── .gitignore                    # Git 忽略文件配置
├── README.md                     # 项目说明文档
├── PROJECT_STRUCTURE.md          # 本文件
│
└── Socks5Client/                 # 主项目目录
    ├── Socks5Client.csproj       # 项目文件
    │
    ├── App.xaml                  # WPF 应用程序入口 XAML
    ├── App.xaml.cs               # 应用程序入口代码
    │
    ├── Core/                     # 核心业务逻辑层
    │   ├── Socks5Engine.cs       # SOCKS5 协议处理引擎
    │   ├── ProxyController.cs    # 代理控制器（管理连接/断开）
    │   ├── SystemProxyManager.cs # 系统代理设置管理
    │   └── SpeedTester.cs        # 节点延迟测速工具
    │
    ├── Model/                    # 数据模型和配置管理
    │   ├── ProxyNode.cs          # 代理节点数据模型
    │   ├── AppSettings.cs        # 应用程序设置模型
    │   ├── ConfigManager.cs      # 配置管理器（读写配置）
    │   └── SubscriptionManager.cs # 订阅管理（可选功能）
    │
    ├── UI/                       # 用户界面层
    │   ├── MainWindow.xaml       # 主窗口界面定义
    │   ├── MainWindow.xaml.cs    # 主窗口代码后台
    │   ├── NodeEditWindow.xaml   # 节点编辑窗口
    │   ├── NodeEditWindow.xaml.cs
    │   │
    │   ├── ViewModels/           # MVVM 模式 ViewModel
    │   │   ├── MainViewModel.cs  # 主窗口 ViewModel
    │   │   └── NodeEditViewModel.cs # 节点编辑 ViewModel
    │   │
    │   └── Converters/           # 数据绑定转换器
    │       ├── BoolToConnectedConverter.cs
    │       ├── BoolToColorConverter.cs
    │       ├── InverseBoolConverter.cs
    │       └── EditModeToTitleConverter.cs
    │
    ├── Logs/                     # 日志模块
    │   └── Logger.cs             # 日志记录器
    │
    └── Resources/                # 资源文件
        ├── Themes/               # 主题资源
        │   └── LightTheme.xaml
        └── app.ico               # 应用程序图标（占位文件）
```

## 模块说明

### Core 层（核心逻辑）

- **Socks5Engine**: 实现 SOCKS5 协议的核心引擎，处理客户端连接、握手、请求转发等
- **ProxyController**: 代理控制器，管理代理的启动、停止、切换等操作
- **SystemProxyManager**: 管理 Windows 系统代理设置（通过注册表）
- **SpeedTester**: 测试代理节点的延迟和连接速度

### Model 层（数据模型）

- **ProxyNode**: 代理节点的数据模型，包含服务器地址、端口、认证信息等
- **AppSettings**: 应用程序的全局设置
- **ConfigManager**: 配置文件的读写管理（JSON 格式）
- **SubscriptionManager**: 订阅链接的管理和更新

### UI 层（用户界面）

- **MainWindow**: 主窗口，显示节点列表、连接状态等
- **NodeEditWindow**: 节点编辑对话框
- **ViewModels**: MVVM 模式的视图模型，处理业务逻辑
- **Converters**: 数据绑定转换器，用于格式化显示

### Logs 层（日志）

- **Logger**: 统一的日志记录器，支持多级别日志，写入文件

## 数据流向

1. **用户操作** → UI 层（XAML/Code-behind）
2. **UI 层** → ViewModel 层（通过 DataBinding 和 Command）
3. **ViewModel** → Core 层（调用业务逻辑）
4. **Core 层** → Model 层（读写配置）
5. **状态变化** → 通过事件机制反馈到 UI

## 配置文件位置

- **配置文件**: `%LocalAppData%\Socks5Client\config.json`
- **日志文件**: `%LocalAppData%\Socks5Client\Logs\log_YYYYMMDD.txt`

## 技术要点

1. **MVVM 架构**: UI 与业务逻辑分离，便于测试和维护
2. **异步编程**: 使用 async/await 处理网络操作，避免 UI 阻塞
3. **事件驱动**: 使用事件机制实现模块间解耦通信
4. **单例模式**: ConfigManager 使用单例确保配置一致性
5. **资源管理**: 使用 using 和 IDisposable 确保资源正确释放

## 扩展建议

- 添加系统托盘图标支持
- 实现订阅功能的完整 UI
- 添加流量统计图表
- 支持更多代理协议（如 Shadowsocks）
- 添加规则路由功能
- 实现自动故障切换

