using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;

Console.WriteLine("Broker\n");

// default parameters
string hostName = "127.0.0.1";
int hostPort = 1883;
int websocketPort = 5000;
string websocketPattern = "mqtt";
BrokerConfiguration internalConfig, externalConfig;

// load internal config
internalConfig = Parser.Parse<BrokerConfiguration>("./configurations/Broker.yml");

// parse arguments
if (args.Length > 0) hostName = args[0];
if (args.Length > 1) hostPort = int.Parse(args[1]);
if (args.Length > 2) websocketPort = int.Parse(args[2]);
if (args.Length > 3) websocketPattern = args[3];

var address = new HostAddress(hostName, hostPort);
var cts = new CancellationTokenSource();
var broker = new MqttBroker(address, true, true, websocketPort, websocketPattern);

try {
  broker.StartUp();
  while (!cts.Token.IsCancellationRequested) {
    Task.Delay(10).Wait();
  }
}
catch (Exception ex) {
  Console.WriteLine(ex.Message);
}
finally {
  broker.TearDown();
}
