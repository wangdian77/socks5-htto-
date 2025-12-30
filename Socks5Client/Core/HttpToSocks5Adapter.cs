using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Socks5Client.Logs;

namespace Socks5Client.Core
{
    /// <summary>
    /// HTTP到SOCKS5适配器
    /// 将HTTP代理请求转换为SOCKS5请求，使浏览器可以通过HTTP代理协议使用SOCKS5代理
    /// </summary>
    public class HttpToSocks5Adapter : IDisposable
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;
        private readonly object _lock = new object();
        private ProxyConnectionInfo? _socks5Server;

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
        /// 启动HTTP到SOCKS5适配器
        /// </summary>
        public Task<bool> StartAsync(ProxyConnectionInfo socks5Server, int localPort)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Logger.Warning("HTTP到SOCKS5适配器已在运行");
                    return Task.FromResult(false);
                }

                _socks5Server = socks5Server;
                LocalPort = localPort;
                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;
            }

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, localPort);
                _listener.Start();

                Logger.Info($"HTTP到SOCKS5适配器已启动，端口: {localPort}");

                // 开始接受连接
                _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error($"启动HTTP到SOCKS5适配器失败: {ex.Message}", ex);
                lock (_lock)
                {
                    _isRunning = false;
                }
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 停止适配器
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
                Logger.Info("HTTP到SOCKS5适配器已停止");
            }
            catch (Exception ex)
            {
                Logger.Error($"停止HTTP到SOCKS5适配器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 接受客户端连接
        /// </summary>
        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            if (_listener == null || _socks5Server == null)
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
            if (_socks5Server == null)
            {
                client.Close();
                return;
            }

            TcpClient? socks5Client = null;

            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    stream.ReadTimeout = 10000; // 减少到10秒
                    stream.WriteTimeout = 10000;

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

                    // 连接到远程SOCKS5服务器（带超时）
                    socks5Client = new TcpClient();
                    var connectTask = socks5Client.ConnectAsync(_socks5Server.Server, _socks5Server.Port);
                    var timeoutTask = Task.Delay(10000); // 10秒超时
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        Logger.Error($"连接SOCKS5服务器超时: {_socks5Server.Server}:{_socks5Server.Port}");
                        socks5Client?.Close();
                        SendHttpError(stream, "504 Gateway Timeout");
                        return;
                    }
                    
                    if (connectTask.IsFaulted)
                    {
                        Logger.Error($"连接SOCKS5服务器失败: {_socks5Server.Server}:{_socks5Server.Port}", connectTask.Exception);
                        socks5Client?.Close();
                        SendHttpError(stream, "502 Bad Gateway");
                        return;
                    }

                    var socks5Stream = socks5Client.GetStream();
                    socks5Stream.ReadTimeout = 10000; // 减少到10秒
                    socks5Stream.WriteTimeout = 10000;

                    // 执行SOCKS5握手
                    Logger.Info($"开始SOCKS5握手: {_socks5Server.Server}:{_socks5Server.Port}");
                    if (!await PerformSocks5HandshakeAsync(socks5Stream))
                    {
                        Logger.Error($"SOCKS5握手失败: {_socks5Server.Server}:{_socks5Server.Port}");
                        SendHttpError(stream, "502 Bad Gateway");
                        return;
                    }
                    Logger.Info($"SOCKS5握手成功: {_socks5Server.Server}:{_socks5Server.Port}");

                    // 如果是CONNECT方法（HTTPS隧道）
                    if (requestInfo.Method == "CONNECT")
                    {
                        await HandleConnectMethodAsync(stream, socks5Stream, requestInfo);
                    }
                    else
                    {
                        // 普通HTTP请求通过SOCKS5代理
                        await HandleHttpRequestAsync(stream, socks5Stream, requestInfo);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Debug("客户端连接处理被取消");
            }
            catch (TimeoutException ex)
            {
                Logger.Error($"处理客户端连接超时: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"处理客户端连接失败: {ex.Message}", ex);
            }
            finally
            {
                socks5Client?.Close();
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
        /// 执行SOCKS5握手
        /// </summary>
        private async Task<bool> PerformSocks5HandshakeAsync(NetworkStream stream)
        {
            try
            {
                // 发送问候（支持无需认证和用户名密码认证）
                // 注意：SOCKS5认证需要同时提供用户名和密码，但有些服务器可能只需要其中一个
                // 为了兼容性，如果用户名或密码任一不为空，就尝试认证
                byte[] greeting;
                bool needsAuth = !string.IsNullOrEmpty(_socks5Server!.Username) || !string.IsNullOrEmpty(_socks5Server.Password);
                
                Logger.Info($"SOCKS5握手 - 需要认证: {needsAuth}, 用户名: {(string.IsNullOrEmpty(_socks5Server.Username) ? "(空)" : $"{_socks5Server.Username.Length}字符")}, " +
                          $"密码: {(string.IsNullOrEmpty(_socks5Server.Password) ? "(空)" : $"{_socks5Server.Password.Length}字符")}");
                
                if (needsAuth)
                {
                    greeting = new byte[] { 0x05, 0x02, 0x00, 0x02 };
                }
                else
                {
                    greeting = new byte[] { 0x05, 0x01, 0x00 };
                }

                await stream.WriteAsync(greeting, 0, greeting.Length);
                await stream.FlushAsync();

                // 读取响应（带超时）
                var response = new byte[2];
                var readTask = stream.ReadAsync(response, 0, 2);
                var timeoutTask = Task.Delay(5000); // 5秒超时
                var completedTask = await Task.WhenAny(readTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    Logger.Error($"SOCKS5握手超时: 等待服务器响应超时");
                    return false;
                }

                int read = await readTask;
                if (read < 2)
                {
                    Logger.Error($"SOCKS5握手失败: 读取响应不完整，只读取了 {read} 字节");
                    return false;
                }
                
                if (response[0] != 0x05)
                {
                    Logger.Error($"SOCKS5握手失败: 无效的协议版本 {response[0]}，期望 0x05");
                    return false;
                }

                // 如果选择了用户名密码认证
                if (response[1] == 0x02)
                {
                    Logger.Info("SOCKS5服务器要求用户名密码认证");
                    var authResult = await AuthenticateSocks5Async(stream);
                    if (!authResult)
                    {
                        Logger.Error("SOCKS5认证失败: 用户名或密码错误");
                    }
                    else
                    {
                        Logger.Info("SOCKS5认证成功");
                    }
                    return authResult;
                }
                
                if (response[1] != 0x00)
                {
                    Logger.Error($"SOCKS5握手失败: 服务器返回错误码 0x{response[1]:X2}");
                    return false;
                }

                Logger.Info("SOCKS5握手成功（无需认证）");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"SOCKS5握手异常: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// SOCKS5认证
        /// </summary>
        private async Task<bool> AuthenticateSocks5Async(NetworkStream stream)
        {
            try
            {
                if (_socks5Server == null) return false;

                var username = _socks5Server.Username ?? "";
                var password = _socks5Server.Password ?? "";

                // 记录认证信息（仅记录长度，不记录实际内容）
                Logger.Info($"开始SOCKS5认证 - 用户名长度: {username.Length}, 密码长度: {password.Length}");
                if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
                {
                    Logger.Warning("SOCKS5认证: 用户名和密码都为空");
                }

                var usernameBytes = Encoding.UTF8.GetBytes(username);
                var passwordBytes = Encoding.UTF8.GetBytes(password);

                var authRequest = new byte[3 + usernameBytes.Length + passwordBytes.Length];
                authRequest[0] = 0x01; // 版本
                authRequest[1] = (byte)usernameBytes.Length;
                Array.Copy(usernameBytes, 0, authRequest, 2, usernameBytes.Length);
                authRequest[2 + usernameBytes.Length] = (byte)passwordBytes.Length;
                Array.Copy(passwordBytes, 0, authRequest, 3 + usernameBytes.Length, passwordBytes.Length);

                await stream.WriteAsync(authRequest, 0, authRequest.Length);
                await stream.FlushAsync();

                var response = new byte[2];
                int read = await stream.ReadAsync(response, 0, 2);
                if (read >= 2)
                {
                    if (response[0] == 0x01 && response[1] == 0x00)
                    {
                        Logger.Info("SOCKS5认证成功");
                        return true;
                    }
                    else
                    {
                        Logger.Error($"SOCKS5认证失败: 服务器返回状态码 0x{response[1]:X2}");
                        return false;
                    }
                }
                else
                {
                    Logger.Error($"SOCKS5认证失败: 读取响应不完整，只读取了 {read} 字节");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"SOCKS5认证异常: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理CONNECT方法（HTTPS隧道）
        /// </summary>
        private async Task HandleConnectMethodAsync(NetworkStream clientStream, NetworkStream socks5Stream, HttpRequestInfo requestInfo)
        {
            try
            {
                Logger.Info($"处理CONNECT请求: {requestInfo.Host}:{requestInfo.Port}");
                
                // 发送SOCKS5连接请求（带超时）
                var connectRequestTask = SendSocks5ConnectRequestAsync(socks5Stream, requestInfo.Host, requestInfo.Port);
                var timeoutTask = Task.Delay(8000); // 8秒超时
                var completedTask = await Task.WhenAny(connectRequestTask, timeoutTask);
                
                if (completedTask == timeoutTask || !await connectRequestTask)
                {
                    Logger.Error($"SOCKS5 CONNECT请求失败或超时: {requestInfo.Host}:{requestInfo.Port}");
                    SendHttpError(clientStream, "504 Gateway Timeout");
                    return;
                }

                // 读取SOCKS5响应（带超时）
                var readResponseTask = ReadSocks5ResponseAsync(socks5Stream);
                timeoutTask = Task.Delay(8000);
                completedTask = await Task.WhenAny(readResponseTask, timeoutTask);
                
                byte[]? response = null;
                if (completedTask == readResponseTask)
                {
                    response = await readResponseTask;
                }
                
                if (response == null || response[1] != 0x00)
                {
                    Logger.Error($"SOCKS5 CONNECT响应失败: {requestInfo.Host}:{requestInfo.Port}, 响应码: {(response != null ? response[1] : -1)}");
                    SendHttpError(clientStream, "502 Bad Gateway");
                    return;
                }

                Logger.Info($"SOCKS5 CONNECT成功: {requestInfo.Host}:{requestInfo.Port}");

                // 向客户端发送200 Connection Established
                var httpResponse = "HTTP/1.1 200 Connection Established\r\n\r\n";
                var responseBytes = Encoding.UTF8.GetBytes(httpResponse);
                await clientStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await clientStream.FlushAsync();

                // 转发数据
                var cts = new CancellationTokenSource();
                var tasks = new[]
                {
                    ForwardDataAsync(clientStream, socks5Stream, cts.Token),
                    ForwardDataAsync(socks5Stream, clientStream, cts.Token)
                };

                await Task.WhenAny(tasks);
                cts.Cancel(); // 取消另一个任务
            }
            catch (TaskCanceledException)
            {
                Logger.Debug("CONNECT请求处理被取消");
            }
            catch (TimeoutException ex)
            {
                Logger.Error($"处理CONNECT请求超时: {ex.Message}");
                SendHttpError(clientStream, "504 Gateway Timeout");
            }
            catch (Exception ex)
            {
                Logger.Error($"处理CONNECT请求失败: {ex.Message}", ex);
                SendHttpError(clientStream, "502 Bad Gateway");
            }
        }

        /// <summary>
        /// 处理普通HTTP请求
        /// </summary>
        private async Task HandleHttpRequestAsync(NetworkStream clientStream, NetworkStream socks5Stream, HttpRequestInfo requestInfo)
        {
            try
            {
                Logger.Debug($"处理HTTP请求: {requestInfo.Method} {requestInfo.Host}:{requestInfo.Port}");
                
                // 发送SOCKS5连接请求
                if (!await SendSocks5ConnectRequestAsync(socks5Stream, requestInfo.Host, requestInfo.Port))
                {
                    Logger.Error($"SOCKS5连接请求失败: {requestInfo.Host}:{requestInfo.Port}");
                    SendHttpError(clientStream, "502 Bad Gateway");
                    return;
                }

                // 读取SOCKS5响应
                var response = await ReadSocks5ResponseAsync(socks5Stream);
                if (response == null || response[1] != 0x00)
                {
                    Logger.Error($"SOCKS5连接响应失败: {requestInfo.Host}:{requestInfo.Port}, 响应码: {(response != null ? response[1] : -1)}");
                    SendHttpError(clientStream, "502 Bad Gateway");
                    return;
                }

                Logger.Debug($"SOCKS5连接成功: {requestInfo.Host}:{requestInfo.Port}");

                // 清理HTTP请求头，移除代理相关的头部
                var cleanedHeaders = CleanHttpHeaders(requestInfo.Headers, requestInfo.Host, requestInfo.Port);
                var requestBytes = Encoding.UTF8.GetBytes(cleanedHeaders);
                
                // 转发HTTP请求到目标服务器
                await socks5Stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await socks5Stream.FlushAsync();

                Logger.Debug($"已发送HTTP请求到 {requestInfo.Host}:{requestInfo.Port}");

                // 转发响应
                await ForwardDataAsync(socks5Stream, clientStream, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理HTTP请求失败: {ex.Message}", ex);
                SendHttpError(clientStream, "502 Bad Gateway");
            }
        }

        /// <summary>
        /// 清理HTTP请求头，移除代理相关的头部并修复URL
        /// </summary>
        private string CleanHttpHeaders(string headers, string host, int port)
        {
            try
            {
                var lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var cleanedLines = new List<string>();
                
                // 处理第一行（请求行）
                if (lines.Length > 0)
                {
                    var firstLine = lines[0].Split(' ');
                    if (firstLine.Length >= 3)
                    {
                        var method = firstLine[0];
                        var url = firstLine[1];
                        var version = firstLine[2];
                        
                        // 如果URL是完整URL，提取路径部分
                        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                        {
                            var uri = new Uri(url);
                            url = uri.PathAndQuery;
                        }
                        
                        cleanedLines.Add($"{method} {url} {version}");
                    }
                }
                
                // 处理其他头部，移除代理相关和连接相关头部
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        cleanedLines.Add(line);
                        continue;
                    }
                    
                    var lowerLine = line.ToLowerInvariant();
                    // 移除代理相关头部
                    if (lowerLine.StartsWith("proxy-") ||
                        lowerLine.StartsWith("proxyconnection:") ||
                        lowerLine.StartsWith("connection: keep-alive"))
                    {
                        continue;
                    }
                    
                    // 修复Host头部
                    if (lowerLine.StartsWith("host:"))
                    {
                        if (port == 80)
                        {
                            cleanedLines.Add($"Host: {host}");
                        }
                        else
                        {
                            cleanedLines.Add($"Host: {host}:{port}");
                        }
                        continue;
                    }
                    
                    cleanedLines.Add(line);
                }
                
                // 添加Connection头部
                cleanedLines.Add("Connection: close");
                
                return string.Join("\r\n", cleanedLines);
            }
            catch (Exception ex)
            {
                Logger.Debug($"清理HTTP头部失败: {ex.Message}，使用原始头部");
                return headers;
            }
        }

        /// <summary>
        /// 发送SOCKS5连接请求
        /// </summary>
        private async Task<bool> SendSocks5ConnectRequestAsync(NetworkStream stream, string host, int port)
        {
            try
            {
                // 判断是IP地址还是域名
                byte[] addressBytes;
                byte addressType;

                if (IPAddress.TryParse(host, out var ipAddress))
                {
                    if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        // IPv4
                        addressBytes = ipAddress.GetAddressBytes();
                        addressType = 0x01;
                    }
                    else
                    {
                        // IPv6
                        addressBytes = ipAddress.GetAddressBytes();
                        addressType = 0x04;
                    }
                }
                else
                {
                    // 域名
                    var hostBytes = Encoding.UTF8.GetBytes(host);
                    addressBytes = new byte[1 + hostBytes.Length];
                    addressBytes[0] = (byte)hostBytes.Length;
                    Array.Copy(hostBytes, 0, addressBytes, 1, hostBytes.Length);
                    addressType = 0x03;
                }

                var portBytes = new byte[] { (byte)(port >> 8), (byte)(port & 0xFF) };

                var request = new byte[4 + addressBytes.Length + 2];
                request[0] = 0x05; // SOCKS版本
                request[1] = 0x01; // CONNECT命令
                request[2] = 0x00; // 保留
                request[3] = addressType;
                Array.Copy(addressBytes, 0, request, 4, addressBytes.Length);
                request[4 + addressBytes.Length] = portBytes[0];
                request[4 + addressBytes.Length + 1] = portBytes[1];

                await stream.WriteAsync(request, 0, request.Length);
                await stream.FlushAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 读取SOCKS5响应
        /// </summary>
        private async Task<byte[]?> ReadSocks5ResponseAsync(NetworkStream stream)
        {
            try
            {
                var header = new byte[4];
                int read = await stream.ReadAsync(header, 0, 4);
                if (read < 4 || header[0] != 0x05)
                {
                    return null;
                }

                int addressLength = 0;
                if (header[3] == 0x01) // IPv4
                {
                    addressLength = 4;
                }
                else if (header[3] == 0x03) // 域名
                {
                    var domainLength = new byte[1];
                    read = await stream.ReadAsync(domainLength, 0, 1);
                    if (read < 1)
                    {
                        return null;
                    }
                    addressLength = domainLength[0];
                }
                else if (header[3] == 0x04) // IPv6
                {
                    addressLength = 16;
                }
                else
                {
                    return null;
                }

                var address = new byte[addressLength];
                read = await stream.ReadAsync(address, 0, addressLength);
                if (read < addressLength)
                {
                    return null;
                }

                var port = new byte[2];
                read = await stream.ReadAsync(port, 0, 2);
                if (read < 2)
                {
                    return null;
                }

                var response = new byte[4 + addressLength + 2];
                Array.Copy(header, response, 4);
                Array.Copy(address, 0, response, 4, addressLength);
                Array.Copy(port, 0, response, 4 + addressLength, 2);

                return response;
            }
            catch
            {
                return null;
            }
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
}

