using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Socks5Client.Logs;

namespace Socks5Client.Model
{
    /// <summary>
    /// 订阅管理器，负责订阅的更新和解析
    /// </summary>
    public class SubscriptionManager
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private readonly ConfigManager _configManager;

        public SubscriptionManager()
        {
            _configManager = ConfigManager.Instance;
            HttpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// 更新订阅
        /// </summary>
        public async Task<SubscriptionUpdateResult> UpdateSubscriptionAsync(string subscriptionId)
        {
            var subscription = _configManager.GetSubscriptions()
                .FirstOrDefault(s => s.Id == subscriptionId);

            if (subscription == null)
            {
                return new SubscriptionUpdateResult
                {
                    Success = false,
                    ErrorMessage = "订阅不存在"
                };
            }

            try
            {
                Logger.Info($"开始更新订阅: {subscription.Name}");

                var response = await HttpClient.GetStringAsync(subscription.Url);
                var nodes = ParseSubscriptionContent(response, subscription.Id);

                // 移除该订阅的旧节点
                var oldNodes = _configManager.GetProxyNodes()
                    .Where(n => n.SubscriptionId == subscription.Id)
                    .ToList();

                foreach (var oldNode in oldNodes)
                {
                    _configManager.RemoveProxyNode(oldNode.Id);
                }

                // 添加新节点
                int addedCount = 0;
                foreach (var node in nodes)
                {
                    if (node.IsValid())
                    {
                        _configManager.AddProxyNode(node);
                        addedCount++;
                    }
                }

                // 更新订阅信息
                subscription.LastUpdateTime = DateTime.Now;
                subscription.NodeCount = addedCount;
                _configManager.UpdateSubscription(subscription);

                Logger.Info($"订阅更新成功: {subscription.Name}, 新增节点: {addedCount}");

                return new SubscriptionUpdateResult
                {
                    Success = true,
                    AddedNodeCount = addedCount,
                    TotalNodeCount = addedCount
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"更新订阅失败: {subscription.Name}, 错误: {ex.Message}");
                return new SubscriptionUpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 更新所有订阅
        /// </summary>
        public async Task<List<SubscriptionUpdateResult>> UpdateAllSubscriptionsAsync()
        {
            var subscriptions = _configManager.GetSubscriptions()
                .Where(s => s.IsEnabled)
                .ToList();

            var results = new List<SubscriptionUpdateResult>();

            foreach (var subscription in subscriptions)
            {
                var result = await UpdateSubscriptionAsync(subscription.Id);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// 解析订阅内容（支持多种格式）
        /// </summary>
        private List<ProxyNode> ParseSubscriptionContent(string content, string subscriptionId)
        {
            var nodes = new List<ProxyNode>();

            try
            {
                // 尝试解析为 JSON 格式（简单节点列表）
                if (content.TrimStart().StartsWith("["))
                {
                    var jsonNodes = JsonConvert.DeserializeObject<List<SubscriptionNodeData>>(content);
                    if (jsonNodes != null)
                    {
                        foreach (var jsonNode in jsonNodes)
                        {
                            var node = new ProxyNode
                            {
                                Name = jsonNode.Name ?? $"{jsonNode.Server}:{jsonNode.Port}",
                                Server = jsonNode.Server ?? "",
                                Port = jsonNode.Port,
                                Username = jsonNode.Username ?? "",
                                Password = jsonNode.Password ?? "",
                                SubscriptionId = subscriptionId
                            };
                            nodes.Add(node);
                        }
                    }
                }
                else
                {
                    // 尝试解析为行格式：server:port:username:password 或 server:port
                    var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Trim().Split(':');
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int port))
                        {
                            var node = new ProxyNode
                            {
                                Name = $"{parts[0]}:{port}",
                                Server = parts[0],
                                Port = port,
                                Username = parts.Length > 2 ? parts[2] : "",
                                Password = parts.Length > 3 ? parts[3] : "",
                                SubscriptionId = subscriptionId
                            };
                            nodes.Add(node);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"解析订阅内容失败: {ex.Message}");
            }

            return nodes;
        }
    }

    /// <summary>
    /// 订阅节点数据（用于JSON解析）
    /// </summary>
    internal class SubscriptionNodeData
    {
        public string? Name { get; set; }
        public string? Server { get; set; }
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    /// <summary>
    /// 订阅更新结果
    /// </summary>
    public class SubscriptionUpdateResult
    {
        public bool Success { get; set; }
        public int AddedNodeCount { get; set; }
        public int TotalNodeCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

