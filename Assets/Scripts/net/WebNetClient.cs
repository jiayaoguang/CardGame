
using System;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;





#region WebSocket客户端实现
/// <summary>
/// WebSocket网络客户端实现
/// </summary>
public class WebNetClient : NetClient
{
    #region 私有字段
    /// <summary>
    /// WebSocket客户端
    /// </summary>
    private ClientWebSocket _clientWebSocket;

    /// <summary>
    /// 默认WebSocket地址
    /// </summary>
    private string _defaultWsUrl = "ws://127.0.0.1:8089";

    /// <summary>
    /// 接收块大小
    /// </summary>
    private const int ReceiveChunkSize = 1024;
    #endregion

    #region 构造函数
    public WebNetClient() : base()
    {
        _clientWebSocket = new ClientWebSocket();
    }
    #endregion

    #region 重写方法

    /// <summary>
    /// 连接WebSocket服务器（兼容原有接口）
    /// </summary>
    /// <param name="addr">服务器地址</param>
    /// <param name="port">服务器端口</param>
    protected override void Connect(string addr, int port)
    {
        Connect($"ws://{addr}:{port}");
    }

    /// <summary>
    /// 连接WebSocket服务器
    /// </summary>
    /// <param name="wsUrl">WebSocket完整地址</param>
    private void Connect(string wsUrl)
    {
        try
        {
            // 关闭已有连接
            Disconnect();

            _defaultWsUrl = wsUrl;
            _clientWebSocket = new ClientWebSocket();

            // 同步等待连接（异步改同步，保持接口一致性）
            Task connectTask = _clientWebSocket.ConnectAsync(new Uri(wsUrl), _cts.Token);
            connectTask.Wait(5000); // 5秒超时

            if (connectTask.IsCompleted && _clientWebSocket.State == WebSocketState.Open)
            {
                _logger.Log($"成功连接到WebSocket服务器：{wsUrl}");
            }
            else
            {
                _logger.LogError($"连接WebSocket服务器超时：{wsUrl}");
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"连接WebSocket服务器失败 {wsUrl}：{e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 发送消息（WebSocket）
    /// </summary>
    /// <param name="id">消息ID</param>
    /// <param name="content">消息内容</param>
    public override void Send(int id, string content)
    {
        if (!IsConnected || _clientWebSocket.State != WebSocketState.Open)
        {
            _logger.LogError("WebSocket未连接，无法发送消息");
            return;
        }

        try
        {
            // 转换内容为字节数组
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            int contentLength = contentBytes.Length;

            // 计算消息长度（4字节长度 + 内容长度）
            int msgLength = 4 + contentLength;

            // 构建发送缓冲区（遵循统一的消息格式）
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

            // 异步发送
            _ = SendBytesAsync(sendBytes);
        }
        catch (Exception e)
        {
            IsConnected = false;
            _logger.LogError($"发送WebSocket消息失败（ID：{id}）：{e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 异步发送字节数据
    /// </summary>
    /// <param name="data">要发送的字节数据</param>
    private async Task SendBytesAsync(byte[] data)
    {
        try
        {
            if (_clientWebSocket.State == WebSocketState.Open)
            {
                await _clientWebSocket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Binary,
                    true,
                    _cts.Token);
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"WebSocket发送数据异常：{e.Message}");
            IsConnected = false;
        }
    }

    /// <summary>
    /// 接收消息
    /// </summary>
    protected override void ReceiveOnceMsg()
    {
        // 异步接收消息
        var receiveTask = ReceiveMessagesAsync();
        // 等待任务完成（在后台线程中）
        receiveTask.Wait(_cts.Token);
    }

    /// <summary>
    /// 异步接收消息
    /// </summary>
    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[ReceiveChunkSize];

        while (IsConnected && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_clientWebSocket.State != WebSocketState.Open)
                {
                    IsConnected = false;
                    _logger.LogError("WebSocket连接已关闭，停止接收");
                    break;
                }

                var result = await _clientWebSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    IsConnected = false;
                    await _clientWebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "正常关闭",
                        CancellationToken.None);
                    _logger.Log("WebSocket服务器主动关闭连接");
                    break;
                }

                if (result.Count > 0)
                {
                    // 处理接收到的字节数据
                    HandleReceiveBytes(buffer, result.Count);
                }

                // 短暂休眠，降低CPU占用
                await Task.Delay(1, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Log("WebSocket接收操作已取消");
                break;
            }
            catch (Exception e)
            {
                _logger.LogError($"WebSocket接收消息异常：{e.Message}\n{e.StackTrace}");
                await Task.Delay(100, _cts.Token); // 异常时延迟，避免频繁报错
            }
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    protected override void Disconnect()
    {
        try
        {
            if (_clientWebSocket != null)
            {
                if (_clientWebSocket.State == WebSocketState.Open)
                {
                    _ = _clientWebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "客户端主动关闭",
                        CancellationToken.None);
                }
                _clientWebSocket.Dispose();
                _clientWebSocket = null;
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"WebSocket断开连接异常：{e.Message}");
        }

        IsConnected = false;
        _logger.Log("已断开与WebSocket服务器的连接");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        _clientWebSocket?.Dispose();
    }
    #endregion
}
#endregion








