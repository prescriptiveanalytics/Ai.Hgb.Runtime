
using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Docker.DotNet;
using Docker.DotNet.Models;
using Serilog;
using System.Numerics;
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

    static Dictionary<string, CancellationTokenSource> monitoringTaskControl;
    static Dictionary<string, LimitedQueue<Heartbeat>> stateBuffers;
    static int DEFAULT_BUFFERSIZE = 100;
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
      monitoringTaskControl = new Dictionary<string, CancellationTokenSource>();
      stateBuffers = new Dictionary<string, LimitedQueue<Heartbeat>>();


      MapRoutes();
      app.Run();
      #endregion business logic
    }

    #region routes
    private static void MapRoutes() {
      app.MapPost("/monitor-start", (string id, string name, string tag, bool stream) =>
      {
        try {
          string runId = Guid.NewGuid().ToString();
          string nameTag = $"{name}:{tag}";

          var buffer = new LimitedQueue<Heartbeat>(DEFAULT_BUFFERSIZE);
          stateBuffers.Add(nameTag, buffer);
          var taskControl = new CancellationTokenSource();
          monitoringTaskControl.Add(nameTag, taskControl);
          var stats = dockerClient.Containers.GetContainerStatsAsync(id, new ContainerStatsParameters { }, new StatsProgress(id, name, tag, buffer), taskControl.Token);

          return Results.Ok(nameTag);
        }
        catch (Exception exc) {
          Log.Fatal(exc.Message);
          return Results.Ok(exc.Message);
        }
      });

      app.MapPost("/monitor-stop", () =>
      {
        foreach (var kvp in monitoringTaskControl) kvp.Value.Cancel();
      });

      app.MapGet("/state/{name}/{tag}", (string name, string tag) =>
      {
        string nameTag = $"{name}:{tag}";
        LimitedQueue<Heartbeat> buffer;

        if(stateBuffers.TryGetValue(nameTag, out buffer)) {
          if(buffer.Count > 0) {
            var current = buffer.Last();
            return Results.Ok(current);
          } else {
            return Results.NoContent();
          }
        } else {
          return Results.NotFound($"The object {nameTag} could not be found.");
        }        
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

  public class LimitedQueue<T> : Queue<T> {
    private readonly int _maxSize;
    public LimitedQueue(int maxSize) {
      _maxSize = maxSize;
    }

    public void Enqueue(T item) {
      this.Enqueue(item);

      if (this.Count > _maxSize)
        this.Dequeue();        
    }

    public T Dequeue() {      
      return this.Dequeue();
    }
  }

  public class StatsProgress : IProgress<ContainerStatsResponse> {

    private string id;
    private string name;
    private string tag;
    private LimitedQueue<Heartbeat> status;


    public StatsProgress(string _id, string _name, string _tag, LimitedQueue<Heartbeat> _status) {
      id = _id;
      name = _name;
      tag = _tag;
      status = _status;
    }

    public void Report(ContainerStatsResponse value) {
      //Console.WriteLine("CPU Usage Total:  " + value.CPUStats.CPUUsage.PercpuUsage);
      //Console.WriteLine("CPU Usage %:      " + value.CPUStats.CPUUsage.TotalUsage);
      //Console.WriteLine("CPU System Usage: " + value.CPUStats.SystemUsage);
      //Console.WriteLine("CPU Online:       " + value.CPUStats.OnlineCPUs);
      //Console.WriteLine("Memory Limit:     " + value.MemoryStats.Limit);
      //Console.WriteLine("Memory Max Usage: " + value.MemoryStats.MaxUsage);
      //Console.WriteLine("Memory Usage:     " + value.MemoryStats.Usage);

      var heartbeat = new Heartbeat() { ApplicationId = value.ID, ApplicationName = $"{name}:{tag}",
        CpuUtilization = Convert.ToInt32(value.CPUStats.CPUUsage.PercpuUsage.First()),
        MemoryUtilization = Convert.ToInt32(value.MemoryStats.Usage)
      };

      status.Enqueue(heartbeat);

      Console.WriteLine(heartbeat);
    }
  }

  #endregion data structures
}
