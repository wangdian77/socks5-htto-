using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Socks5Client.Logs;

namespace Socks5Client.Core
{
    /// <summary>
    /// HTTP代理协议处理引擎
    /// </summary>
    public class HttpProxyEngine : IDisposable
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;
        private readonly object _lock = new object();

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
        }

        /// <summary>
        /// 本地监听端口
        /// </summary>
        public int LocalPort { get; private set; }

        /// <summary>
        /// 远程HTTP代理服务器信息
        /// </summary>
        public ProxyConnectionInfo? RemoteServer { get; private set; }

        /// <summary>
        /// 启动本地HTTP代理服务
        /// </summary>
        public Task<bool> StartAsync(ProxyConnectionInfo remoteServer, int localPort)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Logger.Warning("HTTP代理服务已在运行");
                    return Task.FromResult(false);
                }

                RemoteServer = remoteServer;
                LocalPort = localPort;
                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;
            }

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, localPort);
                _listener.Start();

                Logger.Info($"本地HTTP代理服务已启动，端口: {localPort}");

                // 开始接受连接
                _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error($"启动HTTP代理服务失败: {ex.Message}", ex);
                lock (_lock)
                {
                    _isRunning = false;
                }
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 停止代理服务
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return;
                }

                _isRunning = false;
                _cancellationTokenSource?.Cancel();
            }

            try
            {
                _listener?.Stop();
                Logger.Info("本地HTTP代理服务已停止");
            }
            catch (Exception ex)
            {
                Logger.Error($"停止HTTP代理服务失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 接受客户端连接
        /// </summary>
        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            if (_listener == null || RemoteServer == null)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.Error($"接受客户端连接失败: {ex.Message}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            if (RemoteServer == null)
            {
                client.Close();
                return;
            }

            TcpClient? remoteClient = null;

            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    stream.ReadTimeout = 30000;
                    stream.WriteTimeout = 30000;

                    // 读取HTTP请求
                    var request = await ReadHttpRequestAsync(stream);
                    if (request == null)
                    {
                        Logger.Debug("读取HTTP请求失败");
                        return;
                    }

                    // 解析请求
                    var requestInfo = ParseHttpRequest(request);
                    if (requestInfo == null)
                    {
                        Logger.Debug("解析HTTP请求失败");
                        SendHttpError(stream, "400 Bad Request");
                        return;
                    }

                    // 建立到远程HTTP代理服务器的连接
                    remoteClient = new TcpClient();
                    await remoteClient.ConnectAsync(RemoteServer.Server, RemoteServer.Port);

                    var remoteStream = remoteClient.GetStream();
                    remoteStream.ReadTimeout = 30000;
                    remoteStream.WriteTimeout = 30000;

                    // 如果是CONNECT方法（HTTPS隧道）
                    if (requestInfo.Method == "CONNECT")
                    {
                        await HandleConnectMethodAsync(stream, remoteStream, requestInfo);
                    }
                    else
                    {
                        // 普通HTTP请求
                        await HandleHttpRequestAsync(stream, remoteStream, requestInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"处理客户端连接失败: {ex.Message}");
            }
            finally
            {
                remoteClient?.Close();
            }
        }

        /// <summary>
        /// 读取HTTP请求
        /// </summary>
        private async Task<string?> ReadHttpRequestAsync(NetworkStream stream)
        {
            try
            {
                var buffer = new byte[8192];
                int totalRead = 0;
                int read;

                // 读取请求头
                while (totalRead < buffer.Length)
                {
                    read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead);
                    if (read == 0)
                    {
                        return null;
                    }
                    totalRead += read;

                    var text = Encoding.UTF8.GetString(buffer, 0, totalRead);
                    if (text.Contains("\r\n\r\n"))
                    {
                        return text;
                    }
                }

                return Encoding.UTF8.GetString(buffer, 0, totalRead);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析HTTP请求
        /// </summary>
        private HttpRequestInfo? ParseHttpRequest(string request)
        {
            try
            {
                var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                {
                    return null;
                }

                var firstLine = lines[0].Split(' ');
                if (firstLine.Length < 3)
                {
                    return null;
                }

                var method = firstLine[0];
                var url = firstLine[1];
                var version = firstLine[2];

                // 解析Host头
                string? host = null;
                int port = 80;
                foreach (var line in lines)
                {
                    if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        var hostValue = line.Substring(5).Trim();
                        var parts = hostValue.Split(':');
                        host = parts[0];
                        if (parts.Length > 1)
                        {
                            int.TryParse(parts[1], out port);
                        }
                        break;
                    }
                }

                // 如果是CONNECT方法，从URL解析
                if (method == "CONNECT")
                {
                    var connectParts = url.Split(':');
                    if (connectParts.Length == 2)
                    {
                        host = connectParts[0];
                        int.TryParse(connectParts[1], out port);
                    }
                }

                if (host == null)
                {
                    return null;
                }

                return new HttpRequestInfo
                {
                    Method = method,
                    Url = url,
                    Host = host,
                    Port = port,
                    Version = version,
                    Headers = request
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 处理CONNECT方法（HTTPS隧道）
        /// </summary>
        private async Task HandleConnectMethodAsync(NetworkStream clientStream, NetworkStream remoteStream, HttpRequestInfo requestInfo)
        {
            try
            {
                // 通过远程HTTP代理发送CONNECT请求
                var connectRequest = $"CONNECT {requestInfo.Host}:{requestInfo.Port} HTTP/1.1\r\n";
                connectRequest += $"Host: {requestInfo.Host}:{requestInfo.Port}\r\n";
                
                // 如果需要认证
                if (!string.IsNullOrEmpty(RemoteServer!.Username) || !string.IsNullOrEmpty(RemoteServer.Password))
                {
                    var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{RemoteServer.Username}:{RemoteServer.Password}"));
                    connectRequest += $"Proxy-Authorization: Basic {auth}\r\n";
                }
                
                connectRequest += "\r\n";
                
                var requestBytes = Encoding.UTF8.GetBytes(connectRequest);
                await remoteStream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await remoteStream.FlushAsync();

                // 读取远程代理的响应
                var responseBuffer = new byte[4096];
                int totalRead = 0;
                int read;
                
                // 读取响应头
                while (totalRead < responseBuffer.Length)
                {
                    read = await remoteStream.ReadAsync(responseBuffer, totalRead, responseBuffer.Length - totalRead);
                    if (read == 0)
                    {
                        throw new Exception("远程代理连接已关闭");
                    }
                    totalRead += read;
                    
                    var responseText = Encoding.UTF8.GetString(responseBuffer, 0, totalRead);
                    if (responseText.Contains("\r\n\r\n"))
                    {
                        break;
                    }
                }

                var responseText2 = Encoding.UTF8.GetString(responseBuffer, 0, totalRead);
                if (!responseText2.StartsWith("HTTP/1.1 200", StringComparison.OrdinalIgnoreCase) && 
                    !responseText2.StartsWith("HTTP/1.0 200", StringComparison.OrdinalIgnoreCase))
                {
                    // 代理连接失败，返回错误给客户端
                    SendHttpError(clientStream, "502 Bad Gateway");
                    return;
                }

                // 向客户端发送200 Connection Established
                var response = "HTTP/1.1 200 Connection Established\r\n\r\n";
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await clientStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await clientStream.FlushAsync();

                // 转发数据（客户端 <-> 远程代理）
                var tasks = new[]
                {
                    ForwardDataAsync(clientStream, remoteStream, CancellationToken.None),
                    ForwardDataAsync(remoteStream, clientStream, CancellationToken.None)
                };

                await Task.WhenAny(tasks);
            }
            catch (Exception ex)
            {
                Logger.Debug($"处理CONNECT请求失败: {ex.Message}");
                SendHttpError(clientStream, "502 Bad Gateway");
            }
        }

        /// <summary>
        /// 处理普通HTTP请求
        /// </summary>
        private async Task HandleHttpRequestAsync(NetworkStream clientStream, NetworkStream remoteStream, HttpRequestInfo requestInfo)
        {
            try
            {
                // 构建代理请求
                var proxyRequest = BuildProxyRequest(requestInfo);
                var requestBytes = Encoding.UTF8.GetBytes(proxyRequest);

                // 如果需要认证
                if (!string.IsNullOrEmpty(RemoteServer!.Username) || !string.IsNullOrEmpty(RemoteServer.Password))
                {
                    var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{RemoteServer.Username}:{RemoteServer.Password}"));
                    var authHeader = $"Proxy-Authorization: Basic {auth}\r\n";
                    var insertPos = proxyRequest.IndexOf("\r\n\r\n");
                    if (insertPos > 0)
                    {
                        var newRequest = proxyRequest.Insert(insertPos, authHeader);
                        requestBytes = Encoding.UTF8.GetBytes(newRequest);
                    }
                }

                // 发送请求到远程代理
                await remoteStream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await remoteStream.FlushAsync();

                // 转发响应
                await ForwardDataAsync(remoteStream, clientStream, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Debug($"处理HTTP请求失败: {ex.Message}");
                SendHttpError(clientStream, "502 Bad Gateway");
            }
        }

        /// <summary>
        /// 构建代理请求
        /// </summary>
        private string BuildProxyRequest(HttpRequestInfo requestInfo)
        {
            // 构建完整的URL
            var fullUrl = $"http://{requestInfo.Host}";
            if (requestInfo.Port != 80)
            {
                fullUrl += $":{requestInfo.Port}";
            }
            fullUrl += requestInfo.Url;

            // 替换请求行
            var request = requestInfo.Headers;
            var firstLineEnd = request.IndexOf("\r\n");
            if (firstLineEnd > 0)
            {
                var newFirstLine = $"{requestInfo.Method} {fullUrl} {requestInfo.Version}\r\n";
                request = newFirstLine + request.Substring(firstLineEnd + 2);
            }

            return request;
        }

        /// <summary>
        /// 发送HTTP错误响应
        /// </summary>
        private async void SendHttpError(NetworkStream stream, string error)
        {
            try
            {
                var response = $"HTTP/1.1 {error}\r\nContent-Length: 0\r\n\r\n";
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await stream.FlushAsync();
            }
            catch
            {
                // 忽略发送错误
            }
        }

        /// <summary>
        /// 转发数据
        /// </summary>
        private async Task ForwardDataAsync(NetworkStream source, NetworkStream destination, CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[4096];
                int bytesRead;

                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    await destination.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                Logger.Debug($"数据转发失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// HTTP请求信息
    /// </summary>
    public class HttpRequestInfo
    {
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 80;
        public string Version { get; set; } = string.Empty;
        public string Headers { get; set; } = string.Empty;
    }
}

