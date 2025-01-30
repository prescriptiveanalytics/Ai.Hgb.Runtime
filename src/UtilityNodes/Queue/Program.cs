using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace Ai.Hgb.Application.UtilityNodes.Queue {
  internal class Program {

    static object locker = new object();
    static ConcurrentQueue<IMessage> messages = new ConcurrentQueue<IMessage>();
    static ISocket socket;


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
      socket = new MqttSocket("q1", "Queue_1", address, converter, connect: true);

      // setup subscriptions
      // TODO: interpret routing table
      socket.Subscribe("topic1/addjob", Enqueue, cts.Token);
      socket.Subscribe("topic1/getjob", Dequeue, cts.Token);

    }

    static void Enqueue(IMessage msg, CancellationToken token) {
      messages.Enqueue(msg);
    }

    static void Dequeue(IMessage msg, CancellationToken token) {            

      if(msg.Content is not JobRequest) { // single dequeue
        IMessage message;
        if (messages.TryDequeue(out message)) {
          socket.Publish(msg.ResponseTopic, message.Content);
        }
      } else { // multiple/retain dequeue
        var request = (JobRequest)msg.Content;
        var count = request.RequestCount;        
        var response = new List<IMessage>();
        bool empty = false;
        for (var i = 0; i < count && !empty; i++) {
          IMessage message;
          if (messages.TryDequeue(out message)) {
            response.Add(message);
          } else {
            empty = true;
          }
        }
        if (request.BulkResponse) {
          socket.Publish(msg.ResponseTopic, response);
        } else {          
          foreach(var message in response) socket.Publish(msg.ResponseTopic, message.Content);
        }
      }
    }
  }

  public struct JobRequest { // TODO: move to common entities
    public int RequestCount { get; set; }

    public bool Retain { get; set; }
    
    public bool BulkResponse { get; set; }

    public JobRequest(int requestCount = 1, bool retain = false, bool bulkResponse = false) {
      RequestCount = requestCount;
      Retain = retain;
      BulkResponse = bulkResponse;
    }

    public override string ToString() {
      return $"requestCount: {RequestCount}, retain: {Retain}, bulkResponse: {BulkResponse}";
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
