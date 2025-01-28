using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System.Text.Json.Serialization;

namespace Ai.Hgb.Application.UtilityNodes.Queue {
  internal class Program {
    static void Main(string[] args) {
      Console.WriteLine("Ai.Hgb.Application.UtilityNodes.Queue");

      // IDEAS:
      // - passive (=standard) and active job queue
      // - bulk requests
      // - persistent queue

      // load internal config
      // TODO

      // parse parameters and routing table
      // TODO

      // setup socket and converter      
      //var address = new HostAddress(parameters.ApplicationParametersNetworking.HostName, parameters.ApplicationParametersNetworking.HostPort);
      var address = new HostAddress("127.0.0.1", 1883);
      var converter = new JsonPayloadConverter();
      var cts = new CancellationTokenSource();
      //socket = new MqttSocket(parameters.Name, parameters.Name, address, converter, connect: true);
      var socket = new MqttSocket("q1", "Queue_1", address, converter, connect: true);

      // setup subscriptions
      // TODO
      socket.Subscribe("topic1/addjob", Enqueue, cts.Token);

      // passive queue: enqueue all incoming messages and dequeue after request
      socket.Subscribe("topic1/getjob", Dequeue, cts.Token);

      // active queue: do not enqueue; instead route to workers
      // TODO: new version of Enqueue
    }

    static void Enqueue(IMessage msg, CancellationToken token) {

    }

    static void Dequeue(IMessage msg, CancellationToken token) {

    }
  }

  public struct JobRequest { // TODO: move to common entities
    public int Count { get; set; }

    public bool Retain { get; set; }

    public JobRequest(int count, bool retain) {
      Count = count;
      Retain = retain;
    }

    public override string ToString() {
      return $"count: {Count}, retain: {Retain}";
    }
  }

  public class Parameters : IApplicationParametersBase, IApplicationParametersNetworking {
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("applicationParametersBase")]
    public ApplicationParametersBase ApplicationParametersBase { get; set; }
    [JsonPropertyName("applicationParametersNetworking")]
    public ApplicationParametersNetworking ApplicationParametersNetworking { get; set; }

    public override string ToString() {
      return $"{Name}";
    }
  }
}
