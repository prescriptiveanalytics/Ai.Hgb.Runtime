using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;

Console.WriteLine("Producer\n");

// default parameters
string hostName = "host.docker.internal";
int hostPort = 1883;
SocketConfiguration internalSocketConfig, externalSocketConfig;

// load internal config
var internalConfig = Parser.Parse<SocketConfiguration>("./configurations/Producer.yml");

if (args.Length == 1) {
  // TODO: parse parameters
}
if(args.Length == 2) {
  // TODO: parse external socket config
}


var address = new HostAddress(hostName, hostPort);
var converter = new JsonPayloadConverter();
var cts = new CancellationTokenSource();
ISocket socket;

socket = new MqttSocket("producer1", "Producer", address, converter, connect: true);

var rnd = new Random();
int no = 0;
// v1
//while(!Console.KeyAvailable) {
//  Task.Delay(1500 + rnd.Next(1000)).Wait();
//  no++;
//  socket.Publish("docs/all", new Document("id" + no, socket.Configuration.Name, "Lorem ipsum..."));
//  Console.WriteLine("Producer: published new document.");
//}
//socket.Disconnect();

// v2
try {
  while (!cts.Token.IsCancellationRequested) {
    Task.Delay(1500 + rnd.Next(1000)).Wait();
    no++;
    socket.Publish("docs/all", new Document("id" + no, socket.Configuration.Name, "Lorem ipsum..."));
    Console.WriteLine("Producer: published new document.");
  }
}
catch (Exception ex) { }
finally {
  socket.Disconnect();
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