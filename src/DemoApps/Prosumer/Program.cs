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
      string hostName = "host.docker.internal";
      int hostPort = 1883;
      SocketConfiguration internalSocketConfig, externalSocketConfig;

      // load internal config
      var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Prosumer.yml");



      try {
        if (args.Length > 0) parameters = JsonSerializer.Deserialize<Parameters>(args[0]);
        if (args.Length > 1) routingTable = JsonSerializer.Deserialize<RoutingTable>(args[1]);

        Console.WriteLine(parameters);
      }
      catch (Exception ex) { Console.WriteLine(ex.Message); }

      var address = new HostAddress(hostName, hostPort);
      var converter = new JsonPayloadConverter();
      var cts = new CancellationTokenSource();
      socket = new MqttSocket(parameters.Name, parameters.Name, address, converter, connect: true);


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
      Console.WriteLine("Prosumer: received document.");
      Console.WriteLine(">>> " + doc);
      lock (locker) {
        documents.Add(doc);
        if (documents.Count == 3) {
          var routes = routingTable.Routes.Where(x => x.Source.Id == "pos" && x.SourcePort.Id == "docs");
          foreach(var route in routes) {
            socket.Publish(route.SourcePort.Address, new Document("id" + no, socket.Configuration.Name, string.Join(';', documents.Select(x => x.Text))));
          }
          Console.WriteLine("Prosumer: published aggregated document.");
          documents.Clear();
        }
      }
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
}