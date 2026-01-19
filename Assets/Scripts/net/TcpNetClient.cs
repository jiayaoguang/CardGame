#region TCP客户端实现
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;

using System;
using System.Diagnostics;

/// <summary>
/// TCP网络客户端实现
/// </summary>
public class TcpNetClient : NetClient
{
    #region 私有字段
    /// <summary>
    /// TCP Socket
    /// </summary>
    private Socket _clientSocket;

    /// <summary>
    /// 接收缓冲区（复用，减少内存分配）
    /// </summary>
    private byte[] _receiveBuffer;

    /// <summary>
    /// 默认连接地址
    /// </summary>
    private string _defaultIp = "127.0.0.1";

    /// <summary>
    /// 默认端口
    /// </summary>
    private int _defaultPort = 8088;
    #endregion

    #region 重写方法


    /// <summary>
    /// 连接TCP服务器
    /// </summary>
    /// <param name="addr">服务器地址</param>
    /// <param name="port">服务器端口</param>
    protected override void Connect(string addr, int port)
    {
        try
        {
            // 关闭已有连接
            Disconnect();

            // 初始化Socket
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
            _clientSocket.ReceiveTimeout = 120 * 1000;
            _clientSocket.SendTimeout = 5000;

            // 连接服务器
            IPAddress ip = IPAddress.Parse(addr);
            IPEndPoint endPoint = new IPEndPoint(ip, port);
            _clientSocket.Connect(endPoint);

            // 初始化接收缓冲区
            _receiveBuffer = new byte[RECEIVE_BUFFER_SIZE];

            _logger.Log($"成功连接到TCP服务器：{addr}:{port}");

            // 更新默认地址和端口
            _defaultIp = addr;
            _defaultPort = port;
        }
        catch (Exception e)
        {
            _logger.LogError($"连接TCP服务器失败 {addr}:{port}：{e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 发送消息（TCP）
    /// </summary>
    /// <param name="id">消息ID</param>
    /// <param name="content">消息内容</param>
    public override void Send(int id, string content)
    {
        if ( _clientSocket == null || !_clientSocket.Connected)
        {
            _logger.LogError("Socket未连接，无法发送消息");
            return;
        }

        try
        {
            // 转换内容为字节数组
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            int contentLength = contentBytes.Length;

            // 计算消息长度（4字节长度 + 内容长度）
            int msgLength = 4 + contentLength;

            // 构建发送缓冲区
            byte[] sendBytes = new byte[MESSAGE_HEADER_LENGTH + contentLength];

            // 写入消息长度（大端序）
            sendBytes[0] = (byte)(msgLength >> 24);
            sendBytes[1] = (byte)(msgLength >> 16);
            sendBytes[2] = (byte)(msgLength >> 8);
            sendBytes[3] = (byte)msgLength;

            // 写入消息ID（大端序）
            sendBytes[4] = (byte)(id >> 24);
            sendBytes[5] = (byte)(id >> 16);
            sendBytes[6] = (byte)(id >> 8);
            sendBytes[7] = (byte)id;

            // 写入消息内容
            Array.Copy(contentBytes, 0, sendBytes, MESSAGE_HEADER_LENGTH, contentLength);

            // 发送数据
            _clientSocket.Send(sendBytes, SocketFlags.None);
        }
        catch (Exception e)
        {
            IsConnected = false;
            _logger.LogError($"发送TCP消息失败（ID：{id}）：{e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 接收一次消息
    /// </summary>
    protected override void ReceiveOnceMsg()
    {
        // (IsConnected && !_cts.Token.IsCancellationRequested)
        
        try
        {
            if (_clientSocket == null || !_clientSocket.Connected)
            {
                Disconnect();
                Connect(_defaultIp, _defaultPort);
                UnityEngine.Debug.Log("reconnect ......");
                Thread.Sleep(500);
                return;

            }

            // 接收数据（复用缓冲区）
            int readLen = _clientSocket.Receive(_receiveBuffer);
            if (readLen <= 0)
            {
                //IsConnected = false;
                //_logger.LogWarning("TCP服务器断开连接");
                //break;

                return;
            }

            // 处理接收到的字节数据
            HandleReceiveBytes(_receiveBuffer, readLen);

            // 短暂休眠，降低CPU占用
            Thread.Sleep(1);
        }
        catch (ThreadInterruptedException)
        {
            _logger.Log("TCP接收线程被中断");
        }
        catch (SocketException ex)
        {
            _logger.LogError($"TCP Socket接收异常：{ex.SocketErrorCode} - {ex.Message}");

        }
        catch (Exception ex)
        {
            _logger.LogError($"TCP接收消息异常：{ex.Message}\n{ex.StackTrace}");
            Thread.Sleep(100); // 异常时延迟，避免频繁报错
        }
        
    }

    /// <summary>
    /// 断开连接，释放Socket资源
    /// </summary>
    protected override void Disconnect()
    {
        try
        {
            if (_clientSocket != null)
            {
                if (_clientSocket.Connected)
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                }
                _clientSocket.Close();
                _clientSocket.Dispose();
                _clientSocket = null;
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"TCP断开连接异常：{e.Message}");
        }

        IsConnected = false;
        _logger.Log("已断开与TCP服务器的连接");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        _clientSocket?.Dispose();
    }
    #endregion
}
#endregion