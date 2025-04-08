using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai.hgb.application.demoapps.Common;


Console.WriteLine("Producer\n");

SocketConfiguration internalSocketConfig, externalSocketConfig;

// load internal config
var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Producer.yml");

Parameters parameters = null;
RoutingTable routingTable = null;

// parse parameters and routing table
try {
  if (args.Length > 0) parameters = JsonSerializer.Deserialize<Parameters>(args[0]);
  if (args.Length > 1) routingTable = JsonSerializer.Deserialize<RoutingTable>(args[1]);

  Console.WriteLine("Parameters:");
  Console.WriteLine("-------");
  Console.WriteLine(parameters);
  Console.WriteLine("-------\n");
}
catch (Exception ex) { Console.WriteLine(ex.Message); }

// setup socket and converter
var address = new HostAddress(parameters.ApplicationParametersNetworking.HostName, parameters.ApplicationParametersNetworking.HostPort);
var converter = new JsonPayloadConverter();
var cts = new CancellationTokenSource();
ISocket socket = null;

try {
  socket = new MqttSocket(parameters.Name, parameters.Name, address, converter, connect: true);

  var rnd = new Random();
  int no = 0;

  // setup publishing routes
  var routes = routingTable.Routes.Where(x => x.Source.Id == parameters.Name && x.SourcePort.Id == "docs");

  // start publishing
  Console.WriteLine("Start publishing...");
  while (!cts.Token.IsCancellationRequested && no < parameters.DocCount) {
    Task.Delay(1500 + rnd.Next(1000)).Wait();
    no++;
    var newDoc = new Document("id" + no, socket.Configuration.Name, parameters.DocPrefix + "Lorem ipsum...");
    foreach (var route in routes) {
      socket.Publish(route.SourcePort.Address, newDoc);
      Console.WriteLine($"Published new document {newDoc.Id} to: " + route.SourcePort.Address);
    }
    Console.WriteLine();
  }

} catch(Exception ex) {
  Console.WriteLine(ex.Message);
} finally {
  socket.Disconnect();
}

// v2
//try {
//  while (!cts.Token.IsCancellationRequested && no < parameters.DocCount) {
//    Task.Delay(1500 + rnd.Next(1000)).Wait();
//    no++;
//    var route = routingTable.Routes.Find(x => x.Source.Id == "pro" && x.SourcePort.Id == "docs");
//    Console.WriteLine(route.SourcePort.Address);
//    socket.Publish(route.SourcePort.Address, new Document("id" + no, socket.Configuration.Name, parameters.DocPrefix + "Lorem ipsum..."));
//    Console.WriteLine("Producer: published new document.");
//  }
//}
//catch (Exception ex) { }
//finally {
//  socket.Disconnect();
//}

public class Parameters : IApplicationParametersBase, IApplicationParametersNetworking {
  [JsonPropertyName("name")]
  public string Name { get; set; }
  [JsonPropertyName("description")]
  public string Description { get; set; }
  [JsonPropertyName("docCount")]
  public int DocCount { get; set; }
  [JsonPropertyName("docPrefix")]
  public string DocPrefix { get; set; }
  [JsonPropertyName("applicationParametersBase")]
  public ApplicationParametersBase ApplicationParametersBase { get; set; }
  [JsonPropertyName("applicationParametersNetworking")]
  public ApplicationParametersNetworking ApplicationParametersNetworking { get; set; }

  public override string ToString() {
    return $"{Name}: DocCount={DocCount}, DocPrefix={DocPrefix}";
  }
}