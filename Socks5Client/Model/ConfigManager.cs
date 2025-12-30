using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Socks5Client.Logs;

namespace Socks5Client.Model
{
    /// <summary>
    /// 配置管理器，负责配置的读取、保存和管理
    /// </summary>
    public class ConfigManager
    {
        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Socks5Client");

        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

        private AppSettings _settings;
        private static ConfigManager? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 单例实例
        /// </summary>
        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ConfigManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 当前设置
        /// </summary>
        public AppSettings Settings => _settings;

        private ConfigManager()
        {
            _settings = LoadSettings();
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        Logger.Info("配置加载成功");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"加载配置失败: {ex.Message}");
            }

            Logger.Info("使用默认配置");
            return new AppSettings();
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public bool SaveSettings()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
                Logger.Info("配置保存成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"保存配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 添加代理节点
        /// </summary>
        public void AddProxyNode(ProxyNode node)
        {
            _settings.ProxyNodes.Add(node);
            SaveSettings();
        }

        /// <summary>
        /// 删除代理节点
        /// </summary>
        public bool RemoveProxyNode(string nodeId)
        {
            var node = _settings.ProxyNodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                _settings.ProxyNodes.Remove(node);
                SaveSettings();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 更新代理节点
        /// </summary>
        public bool UpdateProxyNode(ProxyNode node)
        {
            var index = _settings.ProxyNodes.FindIndex(n => n.Id == node.Id);
            if (index >= 0)
            {
                _settings.ProxyNodes[index] = node;
                SaveSettings();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有代理节点
        /// </summary>
        public List<ProxyNode> GetProxyNodes()
        {
            return _settings.ProxyNodes.ToList();
        }

        /// <summary>
        /// 根据ID获取代理节点
        /// </summary>
        public ProxyNode? GetProxyNode(string nodeId)
        {
            return _settings.ProxyNodes.FirstOrDefault(n => n.Id == nodeId);
        }

        /// <summary>
        /// 添加订阅
        /// </summary>
        public void AddSubscription(SubscriptionInfo subscription)
        {
            _settings.Subscriptions.Add(subscription);
            SaveSettings();
        }

        /// <summary>
        /// 删除订阅
        /// </summary>
        public bool RemoveSubscription(string subscriptionId)
        {
            var subscription = _settings.Subscriptions.FirstOrDefault(s => s.Id == subscriptionId);
            if (subscription != null)
            {
                _settings.Subscriptions.Remove(subscription);
                SaveSettings();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 更新订阅
        /// </summary>
        public bool UpdateSubscription(SubscriptionInfo subscription)
        {
            var index = _settings.Subscriptions.FindIndex(s => s.Id == subscription.Id);
            if (index >= 0)
            {
                _settings.Subscriptions[index] = subscription;
                SaveSettings();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有订阅
        /// </summary>
        public List<SubscriptionInfo> GetSubscriptions()
        {
            return _settings.Subscriptions.ToList();
        }
    }
}

