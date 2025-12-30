# SOCKS5 代理客户端

一个简洁易用的 Windows 平台 SOCKS5 代理客户端，使用 C# 和 WPF 开发。

## 功能特性

- ✅ 添加/编辑/删除 SOCKS5/HTTP 代理节点
- ✅ 支持 SOCKS5 和 HTTP 代理协议
- ✅ 支持代理节点用户名和密码认证
- ✅ 一键连接/断开代理
- ✅ 节点延迟测试（支持单个和批量测试）
- ✅ 实时显示连接状态和流量统计
- ✅ 系统代理自动配置（可选）
- ✅ 最小化到系统托盘
- ✅ 节点配置持久化存储
- ✅ 订阅节点列表更新（可选功能）
- ✅ 日志记录和查看

## 系统要求

- Windows 10/11 或更高版本
- .NET 8.0 Runtime（如果使用框架依赖发布）或直接运行自包含版本

## 技术栈

- **语言**: C# 12
- **框架**: .NET 8.0
- **UI框架**: WPF (Windows Presentation Foundation)
- **架构**: MVVM 模式
- **配置存储**: JSON 格式

## 项目结构

```
Socks5Client/
├── Core/                    # 核心业务逻辑层
│   ├── Socks5Engine.cs     # SOCKS5协议处理引擎
│   ├── ProxyController.cs  # 代理控制器
│   ├── SystemProxyManager.cs # 系统代理管理
│   └── SpeedTester.cs      # 节点测速工具
├── Model/                   # 数据模型和配置管理
│   ├── ProxyNode.cs        # 代理节点数据模型
│   ├── AppSettings.cs      # 应用程序设置
│   ├── ConfigManager.cs    # 配置管理器
│   └── SubscriptionManager.cs # 订阅管理器
├── UI/                      # 用户界面层
│   ├── MainWindow.xaml     # 主窗口
│   ├── NodeEditWindow.xaml # 节点编辑窗口
│   ├── ViewModels/         # ViewModel层
│   └── Converters/         # 值转换器
├── Logs/                    # 日志模块
│   └── Logger.cs           # 日志记录器
└── Resources/               # 资源文件
```

## 构建说明

### 前提条件

1. 安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. 安装 Visual Studio 2022（推荐）或 Visual Studio Code

### 构建步骤

1. 克隆或下载项目代码
2. 打开命令行，进入项目根目录
3. 运行以下命令：

```bash
# 还原NuGet包
dotnet restore

# 构建项目（Debug模式）
dotnet build

# 或构建Release版本
dotnet build -c Release
```

### 发布

```bash
# 发布为自包含单文件（推荐，用户无需安装.NET Runtime）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 发布文件位于: Socks5Client/bin/Release/net8.0-windows/win-x64/publish/
```

## 使用说明

### 添加代理节点

1. 点击主界面上的"添加节点"按钮
2. 填写节点信息：
   - **节点名称**: 自定义名称（如：香港节点1）
   - **服务器地址**: SOCKS5服务器IP或域名
   - **端口**: SOCKS5服务器端口（通常为1080）
   - **用户名/密码**: 如果服务器需要认证，填写对应信息（可选）
3. 点击"保存"

### 连接代理

1. 在节点列表中选择要连接的节点
2. 点击右侧的"连接"按钮
3. 连接成功后，状态显示为"已连接"（绿色）
4. 如果启用了系统代理，浏览器等应用会自动使用该代理

### 测试节点延迟

- **测试单个节点**: 选择节点后点击"测试延迟"按钮
- **测试所有节点**: 点击"测试全部"按钮批量测试所有节点

测试结果会显示在节点列表的"延迟"列中。

### 断开代理

点击"断开"按钮即可断开当前连接，系统代理设置会自动恢复。

## 配置文件位置

配置文件存储在用户本地应用数据目录：
```
%LocalAppData%\Socks5Client\config.json
```

日志文件存储在：
```
%LocalAppData%\Socks5Client\Logs\log_YYYYMMDD.txt
```

## 开发说明

### 架构设计

项目采用分层架构，将UI层、业务逻辑层和数据层分离：

- **UI层**: WPF界面，使用MVVM模式，通过数据绑定与ViewModel交互
- **核心层**: 包含SOCKS5协议处理、代理控制、系统代理管理等核心功能
- **模型层**: 数据模型定义和配置管理

### 扩展开发

如需添加新功能：

1. **添加新协议支持**: 在 `Core` 目录下创建新的协议处理类，并在 `ProxyController` 中集成
2. **添加新UI页面**: 在 `UI` 目录下创建新的XAML窗口和对应的ViewModel
3. **修改配置项**: 在 `Model/AppSettings.cs` 中添加新属性，`ConfigManager` 会自动处理序列化

## 注意事项

1. **管理员权限**: 修改系统代理设置可能需要管理员权限，首次运行时建议以管理员身份运行
2. **防火墙**: 本地SOCKS5代理服务需要在防火墙中允许，程序首次启动时会提示
3. **端口占用**: 默认本地监听端口为7890，如果被占用可在设置中修改
4. **节点认证**: 某些SOCKS5服务器需要用户名密码认证，添加节点时请填写正确的认证信息

## 已知限制

- 当前版本仅支持SOCKS5协议
- 订阅功能需要订阅源支持标准格式（JSON或文本行格式）
- 系统代理自动配置功能在Windows 7/8上可能表现不同

## 许可证

本项目采用 MIT 许可证，详见 LICENSE 文件。

## 联系方式

如有问题或建议，欢迎通过以下方式联系：

- **QQ邮箱**: 2022572450@qq.com

## 贡献

欢迎提交 Issue 和 Pull Request 来帮助改进项目。

## 致谢

本项目参考了以下开源项目的设计思路：
- Clash Verge
- Netch

---

**免责声明**: 本软件仅供学习和研究使用，使用者需遵守当地法律法规，对使用本软件产生的任何后果自行承担责任。

