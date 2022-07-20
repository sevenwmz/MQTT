using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using System.Text;

namespace MQTT
{
    public class MqttClient
    {
        #region Field Area

        /// <summary>
        /// Mqtt factory
        /// </summary>
        private MqttFactory _factory;
        /// <summary>
        /// Mqtt client
        /// </summary>
        private IMqttClient _mqttClient;
        /// <summary>
        /// Mqtt 配置信息
        /// </summary>
        private MqttClientConfig _mqttClientConfig;
        /// <summary>
        /// Mqtt options
        /// </summary>
        private IMqttClientOptions _options;

        #endregion

        #region CTOR

        /// <summary>
        /// 默认启动IP为127.0.0.1 端口为1883
        /// </summary>
        public MqttClient()
        {
            _mqttClientConfig = new MqttClientConfig
            {
                ServerIp = "127.0.0.1",
                Port = 1883
            };
            Init();
        }
        /// <summary>
        /// 调用示例
        /// 
        /// //var client = new MqttClient(s =>
        ///    {
        ///        s.Port = 1883;
        ///        s.ServerIp = "127.0.0.1";
        ///        s.UserName = "mqtt-test";
        ///        s.Password = "mqtt-test";
        ///        s.ReciveMsgCallback = s =>
        ///        {
        ///            Console.WriteLine(s.Payload_UTF8);
        ///        };
        ///    }, true);
        ///    client.Subscribe("/TopicName/");
        /// </summary>
        /// <param name="config">客户端配置信息</param>
        /// <param name="autoStart">直接启动</param>
        public MqttClient(Action<MqttClientConfig> config, bool autoStart = false)
        {
            _mqttClientConfig = new MqttClientConfig();
            config(_mqttClientConfig);
            Init();
            if (autoStart)
            {
                Start();
            }
        }

        #endregion

        /// <summary>
        /// 获取MqttClient实例
        /// 
        /// 调用示例
        ///    //var client = MqttClient.Instance(s =>
        ///    {
        ///        s.Port = 1883;
        ///        s.ServerIp = "127.0.0.1";
        ///        s.UserName = "mqtt-test";
        ///        s.Password = "mqtt-test";
        ///        s.ReciveMsgCallback = s =>
        ///        {
        ///            Console.WriteLine(s.Payload_UTF8);
        ///        };
        ///    }, true);
        ///    client.Subscribe("/TopicName/");
        /// </summary>
        /// <param name="config">客户端配置信息</param>
        /// <param name="autoStart">直接启动</param>
        /// <returns></returns>
        public static MqttClient Instance(Action<MqttClientConfig> config, bool autoStart = false)
            => new MqttClient(config, autoStart);

        /// <summary>
        /// 初始化注册
        /// </summary>
        private void Init()
        {
            try
            {
                _factory = new MqttFactory();

                _mqttClient = _factory.CreateMqttClient();

                _options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_mqttClientConfig.ServerIp, _mqttClientConfig.Port)
                    .WithCredentials(_mqttClientConfig.UserName, _mqttClientConfig.Password)
                    .WithClientId(_mqttClientConfig.ClientId)
                    .Build();

                //消息回调
                _mqttClient.UseApplicationMessageReceivedHandler(ReciveMsg);
            }
            catch (Exception exp)
            {
                if (_mqttClientConfig.Exception is null)
                {
                    throw exp;
                }
                _mqttClientConfig.Exception(exp);
            }
        }

        #region 内部事件转换处理

        /// <summary>
        /// 消息接收回调
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private void ReciveMsg(MqttApplicationMessageReceivedEventArgs e)
        {
            if (_mqttClientConfig.ReciveMsgCallback != null)
            {
                _mqttClientConfig.ReciveMsgCallback(new MqttClientReciveMsg
                {
                    Topic = e.ApplicationMessage.Topic,
                    Payload_UTF8 = Encoding.UTF8.GetString(e.ApplicationMessage.Payload),
                    Payload = e.ApplicationMessage.Payload,
                    Qos = e.ApplicationMessage.QualityOfServiceLevel,
                    Retain = e.ApplicationMessage.Retain,
                });
            }
        }

        /// <summary>
        /// 订阅
        /// </summary>
        /// <param name="topicName"></param>
        public async void Subscribe(string topicName)
        {
            topicName = topicName.Trim();
            if (string.IsNullOrEmpty(topicName))
            {
                throw new Exception("订阅主题不能为空！");
            }

            if (!_mqttClient.IsConnected)
            {
                throw new Exception("MQTT客户端尚未连接！请先启动连接");
            }
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topicName).Build());
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="topicName"></param>
        public async void Unsubscribe(string topicName)
        {
            topicName = topicName.Trim();
            if (string.IsNullOrEmpty(topicName))
            {
                throw new Exception("订阅主题不能为空！");
            }

            if (!_mqttClient.IsConnected)
            {
                throw new Exception("MQTT客户端尚未连接！请先启动连接");
            }
            await _mqttClient.UnsubscribeAsync(topicName);
        }

        /// <summary>
        /// 重连机制
        /// </summary>
        private void ReConnected()
        {
            if (_mqttClient.IsConnected)
            {
                return;
            }
            for (int i = 0; i < 10; i++)
            {
                //重连机制
                _mqttClient.UseDisconnectedHandler(async e =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_mqttClientConfig.ReconneTime));
                        await _mqttClient.ConnectAsync(_options);
                        return;
                    }
                    catch (Exception exp)
                    {
                        if (_mqttClientConfig.Exception is null)
                        {
                            throw exp;
                        }
                        _mqttClientConfig.Exception(exp);
                    }
                });
            }
        }

        #endregion

        #region 消息发送

        /// <summary>
        /// 发送消息,含有重连机制，如掉线会自动重连
        /// </summary>
        /// <param name="message"></param>
        private async Task PublishAsync(string topicName, MqttApplicationMessageBuilder MessageBuilder, PublicQos qos = 0)
        {
            string topic = topicName.Trim();
            if (string.IsNullOrEmpty(topic))
            {
                throw new Exception("主题不能为空！");
            }
            ReConnected();

            MessageBuilder.WithTopic(topic).WithRetainFlag();
            if (qos == PublicQos.Qos_0)
            {
                MessageBuilder.WithAtLeastOnceQoS();
            }
            else if (qos == PublicQos.Qos_1)
            {
                MessageBuilder.WithAtMostOnceQoS();
            }
            else
            {
                MessageBuilder.WithExactlyOnceQoS();
            }
            var Message = MessageBuilder.Build();
            try
            {
                await _mqttClient.PublishAsync(Message);
            }
            catch (Exception e)
            {
                if (_mqttClientConfig.Exception is null)
                {
                    throw e;
                }
                _mqttClientConfig.Exception(e);
            }

        }

        /// <summary>
        /// 发送消息,含有重连机制，如掉线会自动重连
        /// </summary>
        /// <param name="message">文字消息</param>
        public async Task Publish(string topicName, string message, PublicQos qos = 0)
        {
            await PublishAsync(topicName, new MqttApplicationMessageBuilder()
                .WithPayload(message), qos);
        }
        /// <summary>
        /// 发送消息,含有重连机制，如掉线会自动重连
        /// </summary>
        /// <param name="message">消息流</param>
        public async void Publish(string topicName, Stream message, PublicQos qos = 0)
            => await PublishAsync(topicName, new MqttApplicationMessageBuilder()
                .WithPayload(message), qos);

        /// <summary>
        /// 发送消息,含有重连机制，如掉线会自动重连
        /// </summary>
        /// <param name="message">Byte消息</param>
        public async void Publish(string topicName, IEnumerable<byte> message, PublicQos qos = 0)
            => await PublishAsync(topicName, new MqttApplicationMessageBuilder()
                .WithPayload(message), qos);

        /// <summary>
        /// 发送消息,含有重连机制，如掉线会自动重连
        /// </summary>
        /// <param name="message">Byte消息</param>
        public async void Publish(string topicName, byte[] message, PublicQos qos = 0)
            => await PublishAsync(topicName, new MqttApplicationMessageBuilder()
                .WithPayload(message), qos);

        #endregion

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <returns></returns>
        public async Task Start()
            => await _mqttClient.ConnectAsync(_options);

        /// <summary>
        /// 停止服务
        /// </summary>
        /// <returns></returns>
        public async Task Stop()
            => await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions { ReasonCode = MqttClientDisconnectReason.NormalDisconnection}, CancellationToken.None);

    }
    public class MqttClientConfig
    {
        private string _serverIp;
        /// <summary>
        /// 服务器IP
        /// </summary>
        public string ServerIp
        {
            get => _serverIp;
            set
            {
                if (string.IsNullOrEmpty(value.Trim()))
                {
                    throw new ArgumentException("ServerIp can't be null or empty!");
                }
                _serverIp = value;
            }
        }
        private int _port;
        /// <summary>
        /// 服务器端口
        /// </summary>
        public int Port
        {
            get => _port;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Port can't below the zero!");
                }
                _port = value;
            }
        }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        private string _clientId;
        /// <summary>
        /// 唯一用户ID，默认使用Guid
        /// </summary>
        public string ClientId
        {
            get
            {
                _clientId = _clientId ?? Guid.NewGuid().ToString();
                return _clientId;
            }
            set => _clientId = value;
        }
        /// <summary>
        /// 客户端掉线重连时间，单位/s，默认5s
        /// </summary>
        public double ReconneTime { get; set; } = 5;
        /// <summary>
        /// 异常回调，默认为空，为空抛异常
        /// </summary>
        public Action<Exception> Exception = null;

        /// <summary>
        /// 接收消息回调，默认不接收
        /// </summary>
        public Action<MqttClientReciveMsg> ReciveMsgCallback = null;
    }
    public class MqttClientReciveMsg
    {
        /// <summary>
        /// 主题
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// UTF-8格式下的 负载/消息
        /// </summary>
        public string Payload_UTF8 { get; set; }
        /// <summary>
        /// 原始 负载/消息
        /// </summary>
        public byte[] Payload { get; set; }
        /// <summary>
        /// Qos
        /// </summary>
        public MqttQualityOfServiceLevel Qos { get; set; }
        /// <summary>
        /// 保留
        /// </summary>
        public bool Retain { get; set; }
    }

    public enum PublicQos
    {
        Qos_0,
        Qos_1,
        Qos_2,
    }
}
