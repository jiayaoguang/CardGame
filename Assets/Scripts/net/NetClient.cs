#region 核心数据结构定义
using System.Collections.Concurrent;
using System.Threading;

using System;

/// <summary>
/// 网络事件数据封装
/// </summary>
public class NetEventData
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public int MsgId { get; set; }

    /// <summary>
    /// 消息字节数据
    /// </summary>
    public byte[] MsgBytes { get; set; }
}

/// <summary>
/// 消息处理器委托
/// </summary>
/// <param name="eventData">事件数据</param>
public delegate void MessageProcessor(NetEventData eventData);

/// <summary>
/// 日志输出接口（解耦Unity Debug）
/// </summary>
public interface ILogger
{
    void Log(string message);
    void LogWarning(string message);
    void LogError(string message);
}

/// <summary>
/// 默认日志实现（兼容Unity）
/// </summary>
public class DefaultLogger : ILogger
{
    public void Log(string message)
    {
#if UNITY_ENGINE
        UnityEngine.Debug.Log(message);
#else
        Console.WriteLine($"[LOG] {message}");
#endif
    }

    public void LogWarning(string message)
    {
#if UNITY_ENGINE
        UnityEngine.Debug.LogWarning(message);
#else
        Console.WriteLine($"[WARNING] {message}");
#endif
    }

    public void LogError(string message)
    {
#if UNITY_ENGINE
        UnityEngine.Debug.LogError(message);
#else
        Console.WriteLine($"[ERROR] {message}");
#endif
    }
}
#endregion




public class UnityLogger : ILogger
{
    public void Log(string message)
    {
        UnityEngine.Debug.Log(message);

    }

    public void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning(message);

    }

    public void LogError(string message)
    {
        UnityEngine.Debug.LogError(message);
    }
}


#region 抽象网络客户端基类（不继承MonoBehaviour）
/// <summary>
/// 网络客户端抽象基类（无MonoBehaviour依赖）
/// </summary>
public abstract class NetClient : IDisposable
{
    #region 配置常量
    /// <summary>
    /// 接收缓冲区大小（避免频繁创建大数组）
    /// </summary>
    protected const int RECEIVE_BUFFER_SIZE = 8192;

    /// <summary>
    /// 最大消息长度（防止恶意数据攻击）
    /// </summary>
    protected const int MAX_MESSAGE_LENGTH = 1024 * 1024; // 1MB

    /// <summary>
    /// 消息头长度（4字节长度 + 4字节ID）
    /// </summary>
    protected const int MESSAGE_HEADER_LENGTH = 8;
    #endregion

    #region 核心字段
    /// <summary>
    /// 消息处理器字典
    /// </summary>
    protected readonly ConcurrentDictionary<int, MessageProcessor> _processorDict = new ConcurrentDictionary<int, MessageProcessor>();

    /// <summary>
    /// 全局消息队列（网络线程生产，外部线程消费）
    /// </summary>
    protected readonly ConcurrentQueue<NetEventData> _globalQueue = new ConcurrentQueue<NetEventData>();

    /// <summary>
    /// 协议ID与类型映射
    /// </summary>
    protected readonly ConcurrentDictionary<int, Type> _protoClassDict = new ConcurrentDictionary<int, Type>();

    /// <summary>
    /// 协议类型与ID映射
    /// </summary>
    protected readonly ConcurrentDictionary<Type, int> _protoClass2IdDict = new ConcurrentDictionary<Type, int>();

    /// <summary>
    /// 不完整消息缓冲区
    /// </summary>
    private byte[] _uncompleteMsgBuffer = Array.Empty<byte>();

    /// <summary>
    /// 连接状态
    /// </summary>
    protected bool IsConnected { get; set; } = false;

    /// <summary>
    /// 接收线程
    /// </summary>
    private Thread _receiveThread;

    /// <summary>
    /// 取消令牌源（用于异步操作取消）
    /// </summary>
    protected CancellationTokenSource _cts;

    /// <summary>
    /// 日志器
    /// </summary>
    protected ILogger _logger;

    /// <summary>
    /// JSON序列化管理器
    /// </summary>
    protected JsonManager _jsonManager;
    #endregion

    #region 构造函数
    protected NetClient()
    {
        _logger = new UnityLogger();
        _jsonManager = JsonManager.Instance;
        _cts = new CancellationTokenSource();
    }
    #endregion

    #region 对外接口
    /// <summary>
    /// 启动客户端
    /// </summary>
    public virtual void StartClient(string addr, int port)
    {
        try
        {
            Connect(addr,port);
            StartReceive();
        }
        catch (Exception e)
        {
            _logger.LogError($"网络客户端启动失败：{e.Message}\n{e.StackTrace}");
        }
    }



    /// <summary>
    /// 连接服务器
    /// </summary>
    /// <param name="addr">服务器地址</param>
    /// <param name="port">服务器端口</param>
    protected abstract void Connect(string addr, int port);

    /// <summary>
    /// 发送序列化对象
    /// </summary>
    /// <param name="data">待发送对象</param>
    public void Send(object data)
    {
        if (data == null)
        {
            _logger.LogError("发送数据不能为空");
            return;
        }

        int msgId = GetMsgId(data.GetType());
        if (msgId <= 0)
        {
            _logger.LogError($"未找到类型 {data.GetType().Name} 对应的消息ID");
            return;
        }

        Send(msgId, data);
    }

    /// <summary>
    /// 发送指定ID的对象
    /// </summary>
    /// <param name="id">消息ID</param>
    /// <param name="data">待发送对象</param>
    public void Send(int id, object data)
    {
       

        try
        {
            string content = _jsonManager.Serialize(data);
            Send(id, content);
        }
        catch (Exception e)
        {
            _logger.LogError($"序列化消息失败（ID：{id}）：{e.Message}");
        }
    }

    /// <summary>
    /// 发送指定ID的字符串内容
    /// </summary>
    /// <param name="id">消息ID</param>
    /// <param name="content">消息内容</param>
    public abstract void Send(int id, string content);

    /// <summary>
    /// 注册消息处理器
    /// </summary>
    /// <param name="id">消息ID</param>
    /// <param name="processor">处理器</param>
    /// <returns>是否注册成功</returns>
    public bool RegisterProcessor(int id, MessageProcessor processor)
    {
        if (processor == null)
        {
            _logger.LogError("消息处理器不能为空");
            return false;
        }

        return _processorDict.TryAdd(id, processor);
    }

    /// <summary>
    /// 注销消息处理器
    /// </summary>
    /// <param name="id">消息ID</param>
    /// <returns>是否注销成功</returns>
    public bool UnregisterProcessor(int id)
    {
        return _processorDict.TryRemove(id, out _);
    }

    /// <summary>
    /// 注册协议类型映射
    /// </summary>
    /// <param name="msgId">消息ID</param>
    /// <param name="protoClazz">协议类型</param>
    public void RegisterProto(int msgId, Type protoClazz)
    {
        if (protoClazz == null)
        {
            _logger.LogError("协议类型不能为空");
            return;
        }

        _protoClassDict.TryAdd(msgId, protoClazz);
        _protoClass2IdDict.TryAdd(protoClazz, msgId);
    }

    /// <summary>
    /// 根据类型获取消息ID
    /// </summary>
    /// <param name="type">协议类型</param>
    /// <returns>消息ID</returns>
    public int GetMsgId(Type type)
    {
        if (type == null) return 0;
        _protoClass2IdDict.TryGetValue(type, out int msgId);
        return msgId;
    }

    /// <summary>
    /// 根据ID获取协议类型
    /// </summary>
    /// <param name="msgId">消息ID</param>
    /// <returns>协议类型</returns>
    public Type GetProto(int msgId)
    {
        _protoClassDict.TryGetValue(msgId, out Type protoClazz);
        return protoClazz;
    }

    /// <summary>
    /// 手动更新消息队列（替代MonoBehaviour的Update）
    /// </summary>
    public void Update()
    {
        while (_globalQueue.TryDequeue(out NetEventData eventData))
        {
            if (eventData == null || eventData.MsgBytes == null)
            {
                continue;
            }

            if (_processorDict.TryGetValue(eventData.MsgId, out MessageProcessor processor))
            {
                try
                {
                    processor.Invoke(eventData);
                }
                catch (Exception e)
                {
                    _logger.LogError($"执行消息处理器异常（MsgId={eventData.MsgId}）：{e.Message}\n{e.StackTrace}");
                }
            }
        }
    }
    #endregion

    #region 内部核心逻辑
    /// <summary>
    /// 启动接收线程
    /// </summary>
    protected virtual void StartReceive()
    {
        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _logger.LogWarning("接收线程已在运行");
            return;
        }

        IsConnected = true;
        _receiveThread = new Thread(ReceiveMsgLoop)
        {

            Name = $"{GetType().Name}_ReceiveThread"
        };
        _receiveThread.Start();

        _logger.Log("接收线程已启动");
    }

    /// <summary>
    /// 接收消息循环（包装层，统一异常处理）
    /// </summary>
    private void ReceiveMsgLoop()
    {
        try
        {
            for (int i= 0; ;i++) {

                ReceiveOnceMsg();

                if ((i & 0xffff)==0) {
                    Thread.Sleep(1);
                }

            }

        }
        catch (OperationCanceledException)
        {
            _logger.Log("接收操作已取消");
        }
        catch (Exception e)
        {
            _logger.LogError($"接收线程异常：{e.Message}\n{e.StackTrace}");
        }
        finally
        {
            IsConnected = false;
            Disconnect();
        }

        _logger.LogError("接收线程结束");
    }

    /// <summary>
    /// 接收一次消息（子类实现）
    /// </summary>
    protected abstract void ReceiveOnceMsg();

    /// <summary>
    /// 处理接收到的字节数据（核心：粘包/拆包处理）
    /// </summary>
    /// <param name="buffer">数据缓冲区</param>
    /// <param name="readLen">实际读取长度</param>
    protected void HandleReceiveBytes(byte[] buffer, int readLen)
    {
        if (readLen <= 0 || buffer == null) return;

        // 1. 合并到不完整消息缓冲区
        int newTotalLength = _uncompleteMsgBuffer.Length + readLen;
        if (newTotalLength > MAX_MESSAGE_LENGTH)
        {
            _logger.LogError($"消息缓冲区超出最大限制：{newTotalLength} > {MAX_MESSAGE_LENGTH}，清空缓冲区");
            _uncompleteMsgBuffer = Array.Empty<byte>();
            return;
        }

        byte[] newBuffer = new byte[newTotalLength];
        Array.Copy(_uncompleteMsgBuffer, 0, newBuffer, 0, _uncompleteMsgBuffer.Length);
        Array.Copy(buffer, 0, newBuffer, _uncompleteMsgBuffer.Length, readLen);
        _uncompleteMsgBuffer = newBuffer;

        // 2. 解析完整消息
        int currentIndex = 0;
        while (currentIndex + MESSAGE_HEADER_LENGTH <= _uncompleteMsgBuffer.Length)
        {
            // 解析消息长度（大端序）
            int msgLength = (_uncompleteMsgBuffer[currentIndex] << 24) |
                           (_uncompleteMsgBuffer[currentIndex + 1] << 16) |
                           (_uncompleteMsgBuffer[currentIndex + 2] << 8) |
                           _uncompleteMsgBuffer[currentIndex + 3];

            // 解析消息ID（大端序）
            int msgId = (_uncompleteMsgBuffer[currentIndex + 4] << 24) |
                       (_uncompleteMsgBuffer[currentIndex + 5] << 16) |
                       (_uncompleteMsgBuffer[currentIndex + 6] << 8) |
                       _uncompleteMsgBuffer[currentIndex + 7];

            // 合法性校验
            if (msgLength <= 0 || msgLength > MAX_MESSAGE_LENGTH)
            {
                _logger.LogError($"无效的消息长度：{msgLength}，清空缓冲区");
                _uncompleteMsgBuffer = Array.Empty<byte>();
                return;
            }

            // 检查是否有完整的消息（4字节长度 + 消息内容长度）
            int totalMsgLength = 4 + msgLength;
            if (currentIndex + totalMsgLength > _uncompleteMsgBuffer.Length)
            {
                break; // 消息不完整，等待下一次数据
            }

            // 提取消息内容（跳过8字节头）
            byte[] completeMsg = new byte[msgLength - 4];
            Array.Copy(_uncompleteMsgBuffer, currentIndex + MESSAGE_HEADER_LENGTH, completeMsg, 0, completeMsg.Length);

            // 发布事件
            PublishEvent(msgId, completeMsg);

            // 移动索引
            currentIndex += totalMsgLength;
        }

        // 3. 处理剩余的不完整消息
        if (currentIndex > 0)
        {
            int remainingLength = _uncompleteMsgBuffer.Length - currentIndex;
            if (remainingLength > 0)
            {
                byte[] remainingBuffer = new byte[remainingLength];
                Array.Copy(_uncompleteMsgBuffer, currentIndex, remainingBuffer, 0, remainingLength);
                _uncompleteMsgBuffer = remainingBuffer;
            }
            else
            {
                _uncompleteMsgBuffer = Array.Empty<byte>();
            }
        }
    }

    /// <summary>
    /// 发布消息事件到队列
    /// </summary>
    /// <param name="msgId">消息ID</param>
    /// <param name="msg">消息内容</param>
    protected void PublishEvent(int msgId, byte[] msg)
    {
        if (!_processorDict.ContainsKey(msgId))
        {
            _logger.LogWarning($"未注册的消息处理器：MsgId={msgId}");
            return;
        }

        NetEventData eventData = new NetEventData
        {
            MsgId = msgId,
            MsgBytes = msg
        };

        _globalQueue.Enqueue(eventData);
    }

    /// <summary>
    /// 断开连接（子类实现具体逻辑）
    /// </summary>
    protected abstract void Disconnect();

    /// <summary>
    /// 停止客户端，释放资源
    /// </summary>
    public virtual void StopClient()
    {
        IsConnected = false;

        // 取消异步操作
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        // 终止接收线程
        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Interrupt();
            _receiveThread.Join(1000);
        }

        // 清空缓冲区和队列
        _uncompleteMsgBuffer = Array.Empty<byte>();
        while (_globalQueue.TryDequeue(out _)) { }

        // 断开连接
        Disconnect();

        _logger.Log("网络客户端已停止");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public virtual void Dispose()
    {
        StopClient();

        _cts?.Dispose();
        _processorDict.Clear();
        _protoClassDict.Clear();
        _protoClass2IdDict.Clear();
    }
    #endregion
}
#endregion