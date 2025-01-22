using ai.hgb.application.demoapps.Common;
using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ai.hgb.application.demoapps.Prosumer {
  public class Program {

    static Parameters parameters = null;
    static RoutingTable routingTable = null;
    static ISocket socket;

    static int no = 0;
    static object locker = new object();
    static List<Document> documents = new List<Document>();

    static void Main(string[] args) {

      Console.WriteLine("Prosumer\n");

      // default parameters
      SocketConfiguration internalSocketConfig, externalSocketConfig;

      // load internal config
      var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Prosumer.yml");

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
      socket = new MqttSocket(parameters.Name, parameters.Name, address, converter, connect: true);

      // setup subscriptions and subsequent actions based on routing table
      Console.WriteLine("Setting up subscriptions and subsequent actions...");
      var routes = routingTable.Routes.Where(x => x.Sink.Id == parameters.Name && x.SinkPort.Id == "docparts");
      foreach(var route in routes) {
        Console.WriteLine("SourcePortAddress: " + route.SourcePort.Address);
        Console.WriteLine("SinkPortAddress: " + route.SinkPort.Address);
        socket.Subscribe<Document>(route.SinkPort.Address, ProcessDocument, cts.Token);
      }

      try {
        while (!cts.Token.IsCancellationRequested) {
          Task.Delay(10).Wait();
        }
        socket.Unsubscribe();
        socket.Disconnect().Dispose();
      }
      catch (Exception ex) { }
      finally {
        socket.Disconnect();
      }
    }

    static void ProcessDocument(IMessage msg, CancellationToken token) {
      var doc = (Document)msg.Content;
      Console.WriteLine("Received document " + doc.Id);
      Console.WriteLine(">>> " + doc);
      lock (locker) {
        documents.Add(doc);
        if (documents.Count == 3) {
          var routes = routingTable.Routes.Where(x => x.Source.Id == "pos" && x.SourcePort.Id == "docs");
          foreach(var route in routes) {
            socket.Publish(route.SourcePort.Address, new Document("id" + no, socket.Configuration.Name, string.Join(';', documents.Select(x => x.Text))));
          }
          Console.WriteLine("Published aggregated document " + doc.Id);
          documents.Clear();
        }
      }
    }
  }

  public class Parameters : IApplicationParametersBase, IApplicationParametersNetworking {
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("docCount")]
    public int DocCount { get; set; }
    [JsonPropertyName("applicationParametersBase")]
    public ApplicationParametersBase ApplicationParametersBase { get; set; }
    [JsonPropertyName("applicationParametersNetworking")]
    public ApplicationParametersNetworking ApplicationParametersNetworking { get; set; }

    public override string ToString() {
      return $"{Name}: DocCount={DocCount}";
    }
  }
}