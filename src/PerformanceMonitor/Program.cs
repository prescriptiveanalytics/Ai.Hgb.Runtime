
using Ai.Hgb.Dat.Communication;
using Docker.DotNet;
using Serilog;
using YamlDotNet.Serialization;

namespace Ai.Hgb.Runtime.PerformanceMonitor {
  public class Program {

    #region fields
    static PerformanceMonitorConfiguration config;
    static int httpPort = 8005;
    static int httpsPort = 8006;

    static WebApplication app;
    static DockerClient dockerClient;
    static HttpClient repositoryClient;
    static Socket brokerClient;
    static HttpClient languageServiceClient;
    #endregion fields

    public static void Main(string[] args) {
      #region configuration
      string configUri = null;

      if ((args == null || args.Length == 0) && File.Exists(@"configurations/performancemonitor.config.yml")) {
        configUri = @"configurations/performancemonitor.config.yml";
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
        config = dser.Deserialize<PerformanceMonitorConfiguration>(doc);
      }
      else {
        Console.WriteLine("No configuration found. Bye bye.\n");
      }

      #endregion configuration

      #region service setup
      var builder = WebApplication.CreateBuilder(args);

      // Add services to the container.
      //builder.Services.AddAuthorization();

      // configure service address
      builder.WebHost.ConfigureKestrel((context, serverOptions) =>
      {
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

      app = builder.Build();

      // Configure the HTTP request pipeline.
      if (app.Environment.IsDevelopment()) {
        app.UseSwagger();
        app.UseSwaggerUI();
      }

      //app.UseHttpsRedirection();
      //app.UseAuthorization();
      #endregion service setup

      #region remote clients setup
      // setup docker client
      dockerClient = new DockerClientConfiguration(new Uri(config.DockerUri)).CreateClient();

      // setup repository client
      HttpClientHandler clientHandler = new HttpClientHandler();
      clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
      repositoryClient = new HttpClient(clientHandler);
      repositoryClient.BaseAddress = new Uri(config.RepositoryUri);
      #endregion #region remote clients setup

      #region business logic
      MapRoutes();
      app.Run();
      #endregion business logic
    }

    #region routes
    private static void MapRoutes() {
      app.MapPost("/monitor", (string name, string tag) =>
      {
        try {
          string runId = Guid.NewGuid().ToString();
          string nameTag = $"{name}:{tag}";

          return Results.Ok(nameTag);
        }
        catch (Exception exc) {
          Log.Fatal(exc.Message);
          return Results.Ok(exc.Message);
        }
      });

      app.MapGet("/state/{name}/{tag}", (string name, string tag) =>
      {
        string nameTag = $"{name}:{tag}";
        return Results.Ok(nameTag);
      });

    }
    #endregion routes
  }

  #region data structures

  public class PerformanceMonitorConfiguration {
    public string PerformanceMonitorUri { get; set; }
    public string DockerUri { get; set; }
    public string RepositoryUri { get; set; }
    public string LanguageServiceUri { get; set; }
    public string BrokerUri { get; set; }
    public string BrokerWebsocketUri { get; set; }
  }

  #endregion data structures
}
