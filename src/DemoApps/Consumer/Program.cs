using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai.hgb.application.demoapps.Common;

Console.WriteLine("Consumer\n");

// default parameters
string hostName = "host.docker.internal";
int hostPort = 1883;
//hostName = "127.0.0.1";
SocketConfiguration internalSocketConfig, externalSocketConfig;

// load internal config
var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Consumer.yml");

Parameters parameters = null;
RoutingTable routingTable = null;

try {
  Console.WriteLine(string.Join('\n', args));
  Console.WriteLine("\n\n");

  if (args.Length > 0) parameters = JsonSerializer.Deserialize<Parameters>(args[0]);
  if (args.Length > 1) routingTable = JsonSerializer.Deserialize<RoutingTable>(args[1]);

  Console.WriteLine(parameters);
}
catch (Exception ex) { Console.WriteLine(ex.Message); }

var address = new HostAddress(hostName, hostPort);
var converter = new JsonPayloadConverter();
var cts = new CancellationTokenSource();
ISocket socket;

int no = 0;
socket = new MqttSocket(parameters.Name, parameters.Name, address, converter, connect: true);


var routes = routingTable.Routes.Where(x => x.Sink.Id == parameters.Name && x.SinkPort.Id == "docs");
foreach(var route in routes) {
  Console.WriteLine(route.SourcePort.Address);
  socket.Subscribe<Document>(route.SinkPort.Address, ProcessDocument, cts.Token);
}
Console.WriteLine($"Consumer: subscribed to {routes.Count()} topics");

// v1
//while (!Console.KeyAvailable) {
//  Task.Delay(10).Wait();
//}
//socket.Disconnect();

// v2
try {
  while(!cts.Token.IsCancellationRequested && no < parameters.DocCount) {
    Task.Delay(10).Wait();
  }
  socket.Unsubscribe();
  socket.Disconnect().Dispose();
} catch (Exception ex) { }
finally {
  socket.Disconnect();
}

void ProcessDocument(IMessage msg, CancellationToken token) {
  var doc = (Document)msg.Content;
  Console.WriteLine("Consumer: received document.");
  Console.WriteLine(">>> " + doc);
  Interlocked.Increment(ref no);
}

public class Parameters {
  [JsonPropertyName("name")]
  public string Name { get; set; }
  [JsonPropertyName("description")]
  public string Description { get; set; }
  [JsonPropertyName("docCount")]
  public int DocCount { get; set; }

  public override string ToString() {
    return $"{Name}: DocCount={DocCount}";
  }
}
