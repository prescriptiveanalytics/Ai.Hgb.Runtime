using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Configuration;
using Ai.Hgb.Runtime;
using Ai.Hgb.Seidl.Data;
using Docker.DotNet;
using Docker.DotNet.Models;
using Serilog;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using YamlDotNet.Serialization;

#region fields

OrchestratorConfiguration config = null;
int httpPort = 7101;
int httpsPort = 7102;

DockerClient dockerClient;
HttpClient repositoryClient;
Socket brokerClient;
HttpClient languageServiceClient;

Image brokerImage, repositoryImage, languageServiceImage;
Container brokerContainer, repositoryContainer, languageServiceContainer;
List<Image> activeImages;
Dictionary<string, List<CreateContainerResponse>> activeContainers = new Dictionary<string, List<CreateContainerResponse>>();

#endregion fields

#region configuration
string configUri = null;

if ((args == null || args.Length == 0) && File.Exists(@"configurations/orchestrator/config.yml")) {
  configUri = @"configurations/orchestrator/config.yml";
}
else if (File.Exists(args[0])) {
  configUri = args[0];
  args = args.Skip(1).ToArray();
}

if (configUri != null) {
  var dser = new DeserializerBuilder()
  .IgnoreFields()
  .IgnoreUnmatchedProperties()
  .Build();
  var doc = File.ReadAllText(configUri);
  config = dser.Deserialize<OrchestratorConfiguration>(doc);
}
else {
  Console.WriteLine("No configuration found. Bye bye.\n");
}

#endregion configuration

#region service setup

var builder = WebApplication.CreateBuilder(args);

// configure service address
builder.WebHost.ConfigureKestrel((context, serverOptions) => {
  serverOptions.ListenAnyIP(httpPort);
});

// setup logger
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
    .Build();
Log.Logger = new LoggerConfiguration()
  .ReadFrom.Configuration(configuration)
  .CreateLogger();
builder.Host.UseSerilog();


// add swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
  app.UseSwagger();
  app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

#endregion service setup

#region remote clients setup
//TBD
// setup repository client
//HttpClientHandler clientHandler = new HttpClientHandler();
//clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
//repositoryClient = new HttpClient(clientHandler);
//repositoryClient.BaseAddress = repositoryUri;

// setup docker client
dockerClient = new DockerClientConfiguration(new Uri(config.DockerUri)).CreateClient();

// setup repository client
HttpClientHandler clientHandler = new HttpClientHandler();
clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
repositoryClient = new HttpClient(clientHandler);
repositoryClient.BaseAddress = new Uri(config.RepositoryUri);

// setup language service client
languageServiceClient = new HttpClient(clientHandler);
languageServiceClient.BaseAddress = new Uri(config.LanguageServiceUri);

#endregion remote clients setup

#region business logic and run

MapRoutes();
app.Run();

#endregion business logic and run

#region routes

void MapRoutes() {
  
  app.MapGet("/hello", () =>
  {
    return "world!";
  });
  //.WithName("GetWeatherForecast")
  //.WithOpenApi();

  app.MapPost("/run", async (ProgramRecord pr, string name = null, string tag = null) => {
      try {
        string runId = Guid.NewGuid().ToString();
      string nameTag = $"{name}:{tag}";

        // routing table
        var routingResponse = await languageServiceClient.PostAsJsonAsync("translate/routing", pr);
        if (routingResponse.IsSuccessStatusCode) {
          var rt = await routingResponse.Content.ReadFromJsonAsync<RoutingTable>();
        }

        var postResponse = await languageServiceClient.PostAsJsonAsync("translate/initializations", pr);
        if (postResponse.IsSuccessStatusCode) {
          var inits = await postResponse.Content.ReadFromJsonAsync<List<InitializationRecord>>();

          var containerTasks = new List<Task<CreateContainerResponse>>();
          foreach (var init in inits) {
            // filter routing table
            //var rt = i.routing.ExtractForPoint(i.name);
            var rt = new RoutingTable();
            var point = init.routing.Points.Find(x => x.Id == init.name);
            rt.AddPoint(point);
            var routes = init.routing.Routes.Where(x => x.Source.Id == point.Id || x.Sink.Id == point.Id); // TODO: change to x.Sink.Equals(point)
            rt.Routes.AddRange(routes);

            // build addresses
            //Console.WriteLine("\nPoints:");
            foreach (var _point in rt.Points) {
              foreach (var _port in _point.Ports.Where(x => x.Type == PortType.Out || x.Type == PortType.Server)) {
                _port.Address = $"{runId}/{_point.Id}/{_port.Id}";
              }
            }

            //Console.WriteLine("\nRoutes:");
            foreach (var _route in rt.Routes) {
              _route.SourcePort.Address = $"{runId}/{_route.Source.Id}/{_route.SourcePort.Id}";
              _route.SinkPort.Address = $"{runId}/{_route.Source.Id}/{_route.SourcePort.Id}";
              //Console.WriteLine(_route.Source.Id + "." + _route.SourcePort.Id + " --> " + _route.Sink.Id + "." + _route.SinkPort.Id);
            }

            // add base parameters to list
            init.parameters["name"] = init.name;
            string desc = init.parameters.ContainsKey("description") ? (string)init.parameters["description"] : "";
            init.parameters.Add("applicationParametersBase", new ApplicationParametersBase(init.name, desc));
            var brokerUriParts = config.BrokerUri.Split(':');
            var brokerUri = new HostAddress(brokerUriParts[0], int.Parse(brokerUriParts[1]));
            if (brokerUri.Name == "127.0.0.1" || brokerUri.Name == "localhost") brokerUri.Name = "host.docker.internal"; // modify broker host name
            init.parameters.Add("applicationParametersNetworking", new ApplicationParametersNetworking(brokerUri.Name, brokerUri.Port));

            containerTasks.Add(dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
              Image = init.exe.imageName + ":" + init.exe.imageTag,
              Name = init.exe.imageName + "." + init.name,
              Cmd = new string[] { JsonSerializer.Serialize(init.parameters), JsonSerializer.Serialize(rt) }
            }));
          }

          // wait for setup
          await Task.WhenAll(containerTasks);

          // start containers
          var containerStarts = new List<Task<bool>>();
          foreach (var t in containerTasks) {
            containerStarts.Add(dockerClient.Containers.StartContainerAsync(t.Result.ID, new ContainerStartParameters()));
          }

          await Task.WhenAll(containerStarts);          
          if (!activeContainers.ContainsKey(nameTag)) activeContainers.Add(nameTag, new List<CreateContainerResponse>());
          foreach (var c in containerTasks) {
            activeContainers[nameTag].Add(c.Result);
          }
        }
        else {
          Console.WriteLine(postResponse.StatusCode);
        }

      return Results.Ok("ok");
    }
      catch (Exception exc) {
        Log.Fatal(exc.Message);
        return Results.Ok(exc.Message);
      }
    });    
}

#endregion routes

#region data structures

public class RuntimeComponent : Ai.Hgb.Runtime.Enumeration {
  public static RuntimeComponent Docker => new(1, "docker");
  public static RuntimeComponent Repository => new(2, "repository");
  public static RuntimeComponent LanguageService => new(3, "languageserver");
  public static RuntimeComponent Broker => new(4, "broker");
  public static RuntimeComponent PerformanceMonitor => new(5, "performancemonitor");

  public RuntimeComponent(int id, string name) : base(id, name) { }
}

public class OrchestratorConfiguration {
  public List<string> Startup { get; set; }
  public string DockerUri { get; set; }
  public string RepositoryUri { get; set; }
  public string LanguageServiceUri { get; set; }
  public string BrokerUri { get; set; }
  public string BrokerWebsocketUri { get; set; }
  public string RepositoryImageName { get; set; }
  public string RepositoryImageTag { get; set; }
  public string RepositoryContainerName { get; set; }
  public int RepositoryImageExposedPort { get; set; }
  public string LanguageServiceImageName { get; set; }
  public string LanguageServiceImageTag { get; set; }
  public string LanguageServiceContainerName { get; set; }
  public int LanguageServiceImageExposedPort { get; set; }
  public string BrokerImageName { get; set; }
  public string BrokerImageTag { get; set; }
  public string BrokerContainerName { get; set; }
  public int BrokerImageExposedMqttPort { get; set; }
  public int BrokerImageExposedWebsocketPort { get; set; }
}

#endregion data structures

