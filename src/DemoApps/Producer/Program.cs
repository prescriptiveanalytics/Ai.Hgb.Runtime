using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai.hgb.application.demoapps.Common;


Console.WriteLine("Producer\n");

// default parameters
string hostName = "host.docker.internal";
int hostPort = 1883;
//hostName = "127.0.0.1";
SocketConfiguration internalSocketConfig, externalSocketConfig;

// load internal config
var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Producer.yml");

Parameters parameters = null;
RoutingTable routingTable = null;

// PERFORMANCE TEST:
Stopwatch swatch = new Stopwatch();
swatch.Start();
Fib(100000000000); // 100 Mill ~27.5sec
swatch.Stop();
Console.WriteLine($"Time elapsed: {swatch.ElapsedMilliseconds} ms\n\n");

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
ISocket socket = null;

try {
  socket = new MqttSocket(parameters.Name, parameters.Name, address, converter, connect: true);

  var rnd = new Random();
  int no = 0;

  Console.WriteLine("Producer: start publishing");
  while (!cts.Token.IsCancellationRequested && no < parameters.DocCount) {
    Task.Delay(1500 + rnd.Next(1000)).Wait();
    no++;

    var routes = routingTable.Routes.Where(x => x.Source.Id == parameters.Name && x.SourcePort.Id == "docs");
    foreach (var route in routes) {
      Console.WriteLine(route.SourcePort.Address);
      socket.Publish(route.SourcePort.Address, new Document("id" + no, socket.Configuration.Name, parameters.DocPrefix + "Lorem ipsum..."));
    }
    Console.WriteLine("Producer: published new document.");
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

static long Fib(long x) {
  if (x == 0) return 0;

  long prev = 0;
  long next = 1;
  for (long i = 1; i < x; i++) {
    long sum = prev + next;
    prev = next;
    next = sum;
  }
  return next;
}

public class Parameters {
  [JsonPropertyName("name")]
  public string Name { get; set; }
  [JsonPropertyName("description")]
  public string Description { get; set; }
  [JsonPropertyName("docCount")]
  public int DocCount { get; set; }
  [JsonPropertyName("docPrefix")]
  public string DocPrefix { get; set; }

  public override string ToString() {
    return $"{Name}: DocCount={DocCount}, DocPrefix={DocPrefix}";
  }
}