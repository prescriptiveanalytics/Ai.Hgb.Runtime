using Ai.Hgb.Common.Entities;
using Ai.Hgb.Runtime;
using Docker.DotNet;
using Docker.DotNet.Models;
using Serilog;
using System.Diagnostics;
using System.Net.Sockets;
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
Dictionary<string, List<CreateContainerResponse>> activeContainers;

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

}

#endregion routes

#region data structures

public class RuntimeComponent : Enumeration {
  public static RuntimeComponent Docker => new(1, "docker");
  public static RuntimeComponent Repository => new(2, "repository");
  public static RuntimeComponent LanguageService => new(3, "languageserver");
  public static RuntimeComponent Broker => new(4, "broker");

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

