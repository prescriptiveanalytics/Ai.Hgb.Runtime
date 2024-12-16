using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine("Consumer\n");

// default parameters
string hostName = "host.docker.internal";
int hostPort = 1883;
SocketConfiguration internalSocketConfig, externalSocketConfig;

// load internal config
var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Consumer.yml");

Parameters parameters = null;
RoutingTable routingTable = null;

try {
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
var route = routingTable.Routes.Find(x => x.Sink.Id == "con" && x.SinkPort.Id == "docs");

socket = new MqttSocket("consumer1", "Consumer", address, converter, connect: true);
Console.WriteLine(route.SourcePort.Address);
socket.Subscribe<Document>(route.SinkPort.Address, ProcessDocument, cts.Token);

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



public struct Document {
  public string Id { get; set; }
  public string Author { get; set; }
  public string Text { get; set; }

  public Document(string id, string author, string text) {
    Id = id;
    Author = author;
    Text = text;
  }

  public override string ToString() {
    return $"Id: {Id}, author: {Author}";
  }
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