using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;

Console.WriteLine("Consumer\n");

// default parameters
string hostName = "host.docker.internal";
int hostPort = 1883;
SocketConfiguration internalSocketConfig, externalSocketConfig;

// load internal config
var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Consumer.yml");

if (args.Length == 1) {
  // TODO: parse parameters
}
if (args.Length == 2) {
  // TODO: parse external socket config
}

var address = new HostAddress(hostName, hostPort);
var converter = new JsonPayloadConverter();
var cts = new CancellationTokenSource();
ISocket socket;

socket = new MqttSocket("consumer1", "Consumer", address, converter, connect: true);
socket.Subscribe<Document>("docs/all", ProcessDocument, cts.Token);

// v1
//while (!Console.KeyAvailable) {
//  Task.Delay(10).Wait();
//}
//socket.Disconnect();

// v2
try {
  while(!cts.Token.IsCancellationRequested) {
    Task.Delay(10).Wait();
  }
} catch (Exception ex) { }
finally {
  socket.Disconnect();
}

void ProcessDocument(IMessage msg, CancellationToken token) {
  var doc = (Document)msg.Content;
  Console.WriteLine("Consumer: received document.");
  Console.WriteLine(">>> " + doc);
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