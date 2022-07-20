using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTT
{
    public class MqttServer
    {
        #region Field Area

        /// <summary>
        /// Mqtt服务端
        /// </summary>
        private IMqttServer _mqttServer;
        /// <summary>
        /// MqttOptions
        /// </summary>
        private MqttServerOptions _options;
        /// <summary>
        /// Mqtt配置信息
        /// </summary>
        private MqttServerConfig _mqttServerConfig;

        #endregion

        #region CTOR

        /// <summary>
        /// 调用示例
        /// 
        /// var server = new MqttServer(s =>
        /// {
        ///     s.ConnectionValidator = new MqttServerConnValidator { Port = 1884,};
        ///     //消息回调
        ///     s.MessageReceived = s =>
        ///     {
        ///         Console.WriteLine(s.Msg);
        ///     };
        ///     //用户连接回调
        ///     s.ClientConnected = s =>
        ///     {
        ///         Console.WriteLine(s.Msg);
        ///     };
        ///     //用户订阅回调
        ///     s.SubscribedTopic = s =>
        ///     {
        ///         Console.WriteLine(s.Msg);
        ///     };
        /// }, true);//直接启动
        /// </summary>
        public MqttServer()
        {
            Init();
        }
        /// <summary>
        /// MqttServer
        /// 
        /// 调用示例
        /// 
        /// var server = new MqttServer(s =>
        /// {
        ///     s.ConnectionValidator = new MqttServerConnValidator { Port = 1884,};
        ///     //消息回调
        ///     s.MessageReceived = s =>
        ///     {
        ///         Console.WriteLine(s.Msg);
        ///     };
        ///     //用户连接回调
        ///     s.ClientConnected = s =>
        ///     {
        ///         Console.WriteLine(s.Msg);
        ///     };
        ///     //用户订阅回调
        ///     s.SubscribedTopic = s =>
        ///     {
        ///         Console.WriteLine(s.Msg);
        ///     };
        /// }, true);//直接启动
        /// </summary>
        /// <param name="mqConfig">配置回调和参数</param>
        /// <param name="autoStartMqtt">是否直接启动mqtt服务</param>
        public MqttServer(Action<MqttServerConfig> mqConfig, bool autoStartMqtt = false)
        {
            _mqttServerConfig = new MqttServerConfig();
            mqConfig(_mqttServerConfig);
            Init();
            if (autoStartMqtt)
            {
                Start().Wait();
            }
        }

        #endregion

        /// <summary>
        /// 获取实例
        /// </summary>
        /// <param name="mqConfig">MQTT配置</param>
        /// <param name="autoStartMqtt">是否直接启动</param>
        /// <returns></returns>
        public static MqttServer Instance(Action<MqttServerConfig> mqConfig, bool autoStartMqtt = false)
        {
            return new MqttServer(mqConfig, autoStartMqtt);
        }

        /// <summary>
        /// 初始化MQTT
        /// </summary>
        private void Init()
        {
            try
            {
                #region Validator

                if (_mqttServerConfig.ConnectionValidator != null)
                {
                    //验证客户端信息比如客户Id、用户名、密码
                    _options = new MqttServerOptions
                    {
                        //连接验证
                        ConnectionValidator = new MqttServerConnectionValidatorDelegate(p =>
                        {
                            if (_mqttServerConfig.ConnectionValidator.ClientIds != null && 
                                _mqttServerConfig.ConnectionValidator.ClientIds.Count > 0)
                            {
                                if (_mqttServerConfig.ConnectionValidator.ClientIds.First(s => s.Equals(p.ClientId)) is null)
                                {
                                    p.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
                                }
                            }

                            if (_mqttServerConfig.ConnectionValidator.Usernames != null &&
                                _mqttServerConfig.ConnectionValidator.Passwords != null &&
                                _mqttServerConfig.ConnectionValidator.Usernames.Count > 0 &&
                                _mqttServerConfig.ConnectionValidator.Passwords.Count > 0)
                            {
                                if (_mqttServerConfig.ConnectionValidator.Usernames.First(s => s.Equals(p.Username)) is null ||
                                    _mqttServerConfig.ConnectionValidator.Usernames.First(s => s.Equals(p.Username)) is null)
                                {
                                    p.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                                }
                            }
                            
                        }),
                    };
                    if (_mqttServerConfig.ConnectionValidator.Port > 0)
                    {
                        //验证客户端端口和服务器端口是否一致（不一致则不能连接到服务器成功）
                        _options.DefaultEndpointOptions.Port = _mqttServerConfig.ConnectionValidator.Port;
                    }
                }
                else
                {
                    _options = new MqttServerOptions();
                }

                #endregion

                #region Event Binding

                //创建Mqtt服务器
                _mqttServer = new MqttFactory().CreateMqttServer();

                //开启订阅事件
                _mqttServer.ClientSubscribedTopicHandler = new MqttServerClientSubscribedTopicHandlerDelegate(MqttNetServer_SubscribedTopic);

                //取消订阅事件
                _mqttServer.ClientUnsubscribedTopicHandler = new MqttServerClientUnsubscribedTopicHandlerDelegate(MqttNetServer_UnSubscribedTopic);

                //客户端消息事件
                _mqttServer.UseApplicationMessageReceivedHandler(MqttServe_ApplicationMessageReceived);

                //客户端连接事件
                _mqttServer.UseClientConnectedHandler(MqttNetServer_ClientConnected);

                //客户端断开事件
                _mqttServer.UseClientDisconnectedHandler(MqttNetServer_ClientDisConnected);

                #endregion

            }
            catch (Exception e)
            {
                if (_mqttServerConfig.ExceptionCallBack is null)
                {
                    throw e;
                }
                _mqttServerConfig.ExceptionCallBack(e);
            }

        }

        /// <summary>
        /// //启动服务器
        /// </summary>
        /// <returns></returns>
        public async Task Start()
            => await _mqttServer.StartAsync(_options);

        /// <summary>
        /// 停止服务
        /// </summary>
        /// <returns></returns>
        public async Task Stop()
            => await _mqttServer.StopAsync();

        #region 内部事件转换处理

        /// <summary>
        /// //客户订阅
        /// </summary>
        /// <param name="e"></param>
        private void MqttNetServer_SubscribedTopic(MqttServerClientSubscribedTopicEventArgs e)
        {
            if (_mqttServerConfig.SubscribedTopic != null)
            {
                _mqttServerConfig.SubscribedTopic(new MqttServerRecive
                {
                    ClientId = e.ClientId,//客户端Id
                    Topic = e.TopicFilter.Topic,
                    Msg = $"客户端[{ e.ClientId}]已订阅主题：{e.TopicFilter.Topic}"
                });
            }
        }

        /// <summary>
        /// //客户取消订阅
        /// </summary>
        /// <param name="e"></param>
        private void MqttNetServer_UnSubscribedTopic(MqttServerClientUnsubscribedTopicEventArgs e)
        {
            if (_mqttServerConfig.UnSubscribedTopic != null)
            {
                _mqttServerConfig.UnSubscribedTopic(new MqttServerRecive
                {
                    ClientId = e.ClientId,//客户端Id
                    Topic = e.TopicFilter,
                    Msg = $"客户端[{ e.ClientId}]已取消订阅主题：{e.TopicFilter}"
                });
            }
        }

        /// <summary>
        /// //接收消息
        /// </summary>
        /// <param name="e"></param>
        private void MqttServe_ApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            var Payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            var Retain = e.ApplicationMessage.Retain;
            if (_mqttServerConfig.MessageReceived != null)
            {
                _mqttServerConfig.MessageReceived(new MqttServerRecive
                {
                    ClientId = e.ClientId,//客户端Id
                    Topic = e.ApplicationMessage.Topic,
                    Payload = Payload,
                    Qos = e.ApplicationMessage.QualityOfServiceLevel,
                    Retain = e.ApplicationMessage.Retain,
                    Msg = $"客户端[{e.ClientId}]>> 主题：[{e.ApplicationMessage.Topic}] " +
                            $"负载：[{Payload}] Qos：[{e.ApplicationMessage.QualityOfServiceLevel}] " +
                            $"保留：[{e.ApplicationMessage.Retain}]"
                });
            }
        }

        /// <summary>
        /// //客户连接
        /// </summary>
        /// <param name="e"></param>
        private void MqttNetServer_ClientConnected(MqttServerClientConnectedEventArgs e)
        {
            if (_mqttServerConfig.ClientConnected != null)
            {
                _mqttServerConfig.ClientConnected(new MqttServerRecive
                {
                    ClientId = e.ClientId,//客户端Id
                    Msg = $"客户端[{ e.ClientId}]已连接"
                });
            }
        }

        /// <summary>
        /// //客户连接断开
        /// </summary>
        /// <param name="e"></param>
        private void MqttNetServer_ClientDisConnected(MqttServerClientDisconnectedEventArgs e)
        {
            if (_mqttServerConfig.ClientDisConnected != null)
            {
                _mqttServerConfig.ClientDisConnected(new MqttServerRecive
                {
                    ClientId = e.ClientId,//客户端Id
                    Msg = $"客户端[{ e.ClientId}]已断开连接"
                });
            }
        }

        #endregion
    }

    public class MqttServerConfig
    {
        /// <summary>
        /// Mqtt验证
        /// </summary>
        public MqttServerConnValidator ConnectionValidator = null;
        /// <summary>
        /// 客户订阅信息提醒
        /// </summary>
        public Action<MqttServerRecive> SubscribedTopic = null;
        /// <summary>
        /// 客户取消订阅提醒
        /// </summary>
        public Action<MqttServerRecive> UnSubscribedTopic = null;
        /// <summary>
        /// 接收消息
        /// </summary>
        public Action<MqttServerRecive> MessageReceived = null;
        /// <summary>
        /// 客户连接提醒
        /// </summary>
        public Action<MqttServerRecive> ClientConnected = null;
        /// <summary>
        /// 客户连接断开提醒
        /// </summary>
        public Action<MqttServerRecive> ClientDisConnected = null;
        /// <summary>
        /// 异常信息回调（建议设置）
        /// </summary>
        public Action<Exception> ExceptionCallBack = null;
    }

    public class MqttServerConnValidator
    {
        /// <summary>
        /// Connecte Client Id
        /// </summary>
        public IList<string> ClientIds { get; set; }

        /// <summary>
        /// Verify Username
        /// </summary>
        public IList<string> Usernames { get; set; }

        /// <summary>
        /// Verify Password
        /// </summary>
        public IList<string> Passwords { get; set; }

        /// <summary>
        /// Verify Port
        /// </summary>
        public int Port { get; set; }
    }

    public class MqttServerRecive
    {
        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ClientId { get; set; }
        /// <summary>
        /// 主题
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// 负载/消息
        /// </summary>
        public string Payload { get; set; }
        /// <summary>
        /// Qos
        /// </summary>
        public MqttQualityOfServiceLevel Qos { get; set; }
        /// <summary>
        /// 保留
        /// </summary>
        public bool Retain { get; set; }
        /// <summary>
        /// 消息内容
        /// </summary>
        public string Msg { get; set; }
    }
}
