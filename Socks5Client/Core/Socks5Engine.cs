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
    /// SOCKS5协议处理引擎
    /// 注意：当前实现是简化版本，实际部署时建议使用成熟的SOCKS5库
    /// </summary>
    public class Socks5Engine : IDisposable
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
        /// 远程SOCKS5服务器信息
        /// </summary>
        public ProxyConnectionInfo? RemoteServer { get; private set; }

        /// <summary>
        /// 启动本地SOCKS5代理服务
        /// </summary>
        public Task<bool> StartAsync(ProxyConnectionInfo remoteServer, int localPort)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Logger.Warning("代理服务已在运行");
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

                Logger.Info($"本地SOCKS5代理服务已启动，端口: {localPort}");

                // 开始接受连接
                _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error($"启动代理服务失败: {ex.Message}", ex);
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
                Logger.Info("本地SOCKS5代理服务已停止");
            }
            catch (Exception ex)
            {
                Logger.Error($"停止代理服务失败: {ex.Message}", ex);
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

                    // SOCKS5握手
                    if (!await PerformSocks5HandshakeAsync(stream))
                    {
                        Logger.Debug("SOCKS5握手失败");
                        return;
                    }

                    // 读取SOCKS5请求
                    var request = await ReadSocks5RequestAsync(stream);
                    if (request == null)
                    {
                        Logger.Debug("读取SOCKS5请求失败");
                        return;
                    }

                    // 建立到远程SOCKS5服务器的连接
                    remoteClient = new TcpClient();
                    await remoteClient.ConnectAsync(RemoteServer.Server, RemoteServer.Port);

                    var remoteStream = remoteClient.GetStream();
                    remoteStream.ReadTimeout = 30000;
                    remoteStream.WriteTimeout = 30000;

                    // 与远程SOCKS5服务器握手
                    if (!await PerformRemoteSocks5HandshakeAsync(remoteStream))
                    {
                        Logger.Debug("远程SOCKS5服务器握手失败");
                        SendSocks5Response(stream, 0x01); // 一般SOCKS服务器失败
                        return;
                    }

                    // 向远程SOCKS5服务器发送请求
                    if (!await ForwardSocks5RequestAsync(remoteStream, request))
                    {
                        Logger.Debug("转发SOCKS5请求失败");
                        SendSocks5Response(stream, 0x01);
                        return;
                    }

                    // 读取远程服务器响应
                    var remoteResponse = await ReadSocks5ResponseAsync(remoteStream);
                    if (remoteResponse == null)
                    {
                        Logger.Debug("读取远程SOCKS5响应失败");
                        SendSocks5Response(stream, 0x01);
                        return;
                    }

                    // 向客户端发送响应
                    SendSocks5Response(stream, 0x00); // 成功

                    // 转发数据
                    var tasks = new[]
                    {
                        ForwardDataAsync(stream, remoteStream, cancellationToken),
                        ForwardDataAsync(remoteStream, stream, cancellationToken)
                    };

                    await Task.WhenAny(tasks);
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
        /// 执行SOCKS5握手（客户端 -> 本地代理）
        /// </summary>
        private async Task<bool> PerformSocks5HandshakeAsync(NetworkStream stream)
        {
            try
            {
                var buffer = new byte[2];
                int read = await stream.ReadAsync(buffer, 0, 2);
                if (read < 2 || buffer[0] != 0x05)
                {
                    return false;
                }

                int methodCount = buffer[1];
                if (methodCount == 0)
                {
                    return false;
                }

                var methods = new byte[methodCount];
                read = await stream.ReadAsync(methods, 0, methodCount);
                if (read < methodCount)
                {
                    return false;
                }

                // 选择无需认证（0x00）
                var response = new byte[] { 0x05, 0x00 };
                await stream.WriteAsync(response, 0, 2);
                await stream.FlushAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 与远程SOCKS5服务器握手
        /// </summary>
        private async Task<bool> PerformRemoteSocks5HandshakeAsync(NetworkStream remoteStream)
        {
            try
            {
                // 发送问候（支持无需认证和用户名密码认证）
                byte[] greeting;
                if (!string.IsNullOrEmpty(RemoteServer!.Username) || !string.IsNullOrEmpty(RemoteServer.Password))
                {
                    // 需要认证
                    greeting = new byte[] { 0x05, 0x02, 0x00, 0x02 };
                }
                else
                {
                    // 无需认证
                    greeting = new byte[] { 0x05, 0x01, 0x00 };
                }

                await remoteStream.WriteAsync(greeting, 0, greeting.Length);
                await remoteStream.FlushAsync();

                // 读取响应
                var response = new byte[2];
                int read = await remoteStream.ReadAsync(response, 0, 2);
                if (read < 2 || response[0] != 0x05)
                {
                    return false;
                }

                // 如果选择了用户名密码认证
                if (response[1] == 0x02)
                {
                    return await AuthenticateRemoteServerAsync(remoteStream);
                }

                return response[1] == 0x00;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 认证远程服务器
        /// </summary>
        private async Task<bool> AuthenticateRemoteServerAsync(NetworkStream remoteStream)
        {
            try
            {
                if (RemoteServer == null) return false;

                var username = RemoteServer.Username ?? "";
                var password = RemoteServer.Password ?? "";

                var usernameBytes = Encoding.UTF8.GetBytes(username);
                var passwordBytes = Encoding.UTF8.GetBytes(password);

                var authRequest = new byte[3 + usernameBytes.Length + passwordBytes.Length];
                authRequest[0] = 0x01; // 版本
                authRequest[1] = (byte)usernameBytes.Length;
                Array.Copy(usernameBytes, 0, authRequest, 2, usernameBytes.Length);
                authRequest[2 + usernameBytes.Length] = (byte)passwordBytes.Length;
                Array.Copy(passwordBytes, 0, authRequest, 3 + usernameBytes.Length, passwordBytes.Length);

                await remoteStream.WriteAsync(authRequest, 0, authRequest.Length);
                await remoteStream.FlushAsync();

                var response = new byte[2];
                int read = await remoteStream.ReadAsync(response, 0, 2);
                return read >= 2 && response[0] == 0x01 && response[1] == 0x00;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 读取SOCKS5请求
        /// </summary>
        private async Task<byte[]?> ReadSocks5RequestAsync(NetworkStream stream)
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

                // 组合完整的请求
                var request = new byte[4 + addressLength + 2];
                Array.Copy(header, request, 4);
                Array.Copy(address, 0, request, 4, addressLength);
                Array.Copy(port, 0, request, 4 + addressLength, 2);

                return request;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 转发SOCKS5请求到远程服务器
        /// </summary>
        private async Task<bool> ForwardSocks5RequestAsync(NetworkStream remoteStream, byte[] request)
        {
            try
            {
                await remoteStream.WriteAsync(request, 0, request.Length);
                await remoteStream.FlushAsync();
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
        private async Task<byte[]?> ReadSocks5ResponseAsync(NetworkStream remoteStream)
        {
            try
            {
                var header = new byte[4];
                int read = await remoteStream.ReadAsync(header, 0, 4);
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
                    read = await remoteStream.ReadAsync(domainLength, 0, 1);
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
                read = await remoteStream.ReadAsync(address, 0, addressLength);
                if (read < addressLength)
                {
                    return null;
                }

                var port = new byte[2];
                read = await remoteStream.ReadAsync(port, 0, 2);
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
        /// 发送SOCKS5响应
        /// </summary>
        private async void SendSocks5Response(NetworkStream stream, byte reply)
        {
            try
            {
                // 简化响应：只发送成功或失败
                var response = new byte[] { 0x05, reply, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                await stream.WriteAsync(response, 0, response.Length);
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
    /// 代理连接信息
    /// </summary>
    public class ProxyConnectionInfo
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
