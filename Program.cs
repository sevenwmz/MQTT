// See https://aka.ms/new-console-template for more information


using MQTT;


Console.WriteLine("Hello, World!");

//var server = new MqttServer(s =>
//{
//    s.ConnectionValidator = new MqttServerConnValidator { Port = 1884,};
//    //消息回调
//    s.MessageReceived = s =>
//    {
//        Console.WriteLine(s.Msg);
//    };
//    //用户连接回调
//    s.ClientConnected = s =>
//    {
//        Console.WriteLine(s.Msg);
//    };
//    //用户订阅回调
//    s.SubscribedTopic = s =>
//    {
//        Console.WriteLine(s.Msg);
//    };
//}, true);//直接启动


//var client = new MqttClient(s =>
//{
//    s.Port = 1883;
//    s.ServerIp = "127.0.0.1";
//    s.UserName = "mqtt-test";
//    s.Password = "mqtt-test";
//    s.ReciveMsgCallback = s =>
//    {
//        Console.WriteLine(s.Payload_UTF8);
//    };
//}, true);
//client.Subscribe("/iot/");
while (true)
{
    //await client.Publish("/iot/", "test11111111");
    Thread.Sleep(1000);
}
