using System;
using System.Collections.Generic;

namespace Socks5Client.Model
{
    /// <summary>
    /// 应用程序设置
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 是否开机自启
        /// </summary>
        public bool StartWithWindows { get; set; } = false;

        /// <summary>
        /// 是否启用系统代理
        /// </summary>
        public bool EnableSystemProxy { get; set; } = true;

        /// <summary>
        /// 本地监听端口
        /// </summary>
        public int LocalPort { get; set; } = 7890;

        /// <summary>
        /// 语言设置 (zh-CN, en-US)
        /// </summary>
        public string Language { get; set; } = "zh-CN";

        /// <summary>
        /// 主题 (Light, Dark)
        /// </summary>
        public string Theme { get; set; } = "Light";

        /// <summary>
        /// 是否启用系统托盘
        /// </summary>
        public bool EnableTrayIcon { get; set; } = true;

        /// <summary>
        /// 是否最小化到托盘
        /// </summary>
        public bool MinimizeToTray { get; set; } = false;

        /// <summary>
        /// 日志级别 (Debug, Info, Warning, Error)
        /// </summary>
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// 是否自动测试节点延迟
        /// </summary>
        public bool AutoTestLatency { get; set; } = false;

        /// <summary>
        /// 订阅更新间隔（分钟）
        /// </summary>
        public int SubscriptionUpdateInterval { get; set; } = 60;

        /// <summary>
        /// 代理节点列表
        /// </summary>
        public List<ProxyNode> ProxyNodes { get; set; } = new List<ProxyNode>();

        /// <summary>
        /// 订阅列表
        /// </summary>
        public List<SubscriptionInfo> Subscriptions { get; set; } = new List<SubscriptionInfo>();

        /// <summary>
        /// 当前选中的节点ID
        /// </summary>
        public string? SelectedNodeId { get; set; }
    }

    /// <summary>
    /// 订阅信息
    /// </summary>
    public class SubscriptionInfo
    {
        /// <summary>
        /// 订阅ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 订阅名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 订阅URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 节点数量
        /// </summary>
        public int NodeCount { get; set; } = 0;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}

