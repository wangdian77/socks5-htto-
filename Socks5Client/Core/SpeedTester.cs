using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Socks5Client.Logs;
using Socks5Client.Model;

namespace Socks5Client.Core
{
    /// <summary>
    /// 节点测速工具
    /// </summary>
    public class SpeedTester
    {
        /// <summary>
        /// 测试节点延迟（TCP连接时间）
        /// </summary>
        public async Task<int> TestLatencyAsync(ProxyNode node, int timeoutMs = 5000)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var startTime = DateTime.Now;
                    var connectTask = client.ConnectAsync(node.Server, node.Port);
                    var timeoutTask = Task.Delay(timeoutMs);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        Logger.Debug($"节点 {node.Name} 连接超时");
                        return -1;
                    }

                    var endTime = DateTime.Now;
                    var latency = (int)(endTime - startTime).TotalMilliseconds;

                    node.Latency = latency;
                    node.LastTestTime = DateTime.Now;

                    Logger.Debug($"节点 {node.Name} 延迟测试完成: {latency}ms");
                    return latency;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"节点 {node.Name} 延迟测试失败: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 使用Ping测试节点延迟（ICMP）
        /// </summary>
        public async Task<int> PingLatencyAsync(string server, int timeoutMs = 5000)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(server, timeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        return (int)reply.RoundtripTime;
                    }
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Ping测试失败: {server}, {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 批量测试节点延迟
        /// </summary>
        public async Task TestMultipleNodesAsync(System.Collections.Generic.List<ProxyNode> nodes, 
            IProgress<TestProgress>? progress = null)
        {
            int total = nodes.Count;
            int completed = 0;

            foreach (var node in nodes)
            {
                var latency = await TestLatencyAsync(node);
                completed++;

                progress?.Report(new TestProgress
                {
                    Total = total,
                    Completed = completed,
                    CurrentNode = node
                });

                // 避免过于频繁的请求
                await Task.Delay(200);
            }
        }
    }

    /// <summary>
    /// 测试进度信息
    /// </summary>
    public class TestProgress
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public ProxyNode? CurrentNode { get; set; }
    }
}

