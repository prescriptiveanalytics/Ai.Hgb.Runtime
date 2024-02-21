using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;

Console.WriteLine("Broker\n");

// default parameters
string hostName = "127.0.0.1";
int hostPort = 1883;
BrokerConfiguration internalConfig, externalConfig;

// load internal config
internalConfig = Parser.Parse<BrokerConfiguration>("./configurations/Broker.yml");

// parse arguments
if (args.Length == 1) {
  // TODO: load external config and override internal one
}




var address = new HostAddress(hostName, hostPort);
var cts = new CancellationTokenSource();
var broker = new MqttBroker(address, true);

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
