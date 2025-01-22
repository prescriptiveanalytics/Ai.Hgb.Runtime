using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using Ai.Hgb.Seidl.Data;
using Ai.Hgb.Seidl.Processor;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;

namespace Ai.Hgb.Runtime {

  #region data structures

  public class RuntimeComponent : Enumeration {
    public static RuntimeComponent Docker => new(1, "docker");
    public static RuntimeComponent Repository => new(2, "repository");
    public static RuntimeComponent LanguageService => new(3, "languageserver");
    public static RuntimeComponent Broker => new(4, "broker");

    public RuntimeComponent(int id, string name) : base(id, name) { }
  }

  public class ReplConfiguration {
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

  public class Repl {

    #region properties
    public List<RuntimeComponent> Startup { get; set; }
    public Uri DockerUri { get; set; }
    public Uri RepositoryUri { get; set; }
    public Uri LanguageServiceUri { get; set; }
    public HostAddress BrokerUri { get; set; }
    public HostAddress BrokerWebsocketUri { get; set; }

    public string RepositoryImageName { get; set; }
    public string RepositoryImageTag { get; set; }
    public int RepositoryImageExposedPort { get; set; }
    public string RepositoryContainerName { get; set; }
    public string LanguageServiceImageName { get; set; }
    public string LanguageServiceImageTag { get; set; }
    public int LanguageServiceImageExposedPort { get; set; }
    public string LanguageServiceContainerName { get; set; }
    public string BrokerImageName { get; set; }
    public string BrokerImageTag { get; set; }
    public int BrokerImageExposedMqttPort { get; set; }
    public int BrokerImageExposedWebsocketPort { get; set; }
    public string BrokerContainerName { get; set; }
    #endregion properties

    #region fields
    private static string[] COMMANDS = { "run", "run-demo", "run-process", "demo", "state", "start", "pause", "stop", "attach", "detach", "cancel", "save", "exit", "q", "quit", "list" };
    private static string DOCKER_RUNTIME_ID_PREFIX = "ai.hgb.runtime";
    private static string DOCKER_APPLICATION_ID_PREFIX = "ai.hgb.application";

    private DockerClient dockerClient;
    private HttpClient repositoryClient;
    private Socket brokerClient;
    private HttpClient languageServiceClient;

    private Image brokerImage, repositoryImage, languageServiceImage;
    private Container brokerContainer, repositoryContainer, languageServiceContainer;
    private List<Image> activeImages;
    private List<CreateContainerResponse> activeContainers;
    private List<Process> activeProcesses;

    private CancellationTokenSource cts;
    private bool exit;
    private string runningTask;
    private BrokerConfiguration brokerConfigBase;
    private SocketConfiguration socketConfigBase;
    #endregion fields

    public Repl() {
      cts = new CancellationTokenSource();
      exit = false;
    }

    public Repl(ReplConfiguration config) {
      cts = new CancellationTokenSource();
      exit = false;
      activeContainers = new List<CreateContainerResponse>();
      activeProcesses = new List<Process>();

      Startup = new List<RuntimeComponent>();
      foreach(var i in config.Startup) {
        if (RuntimeComponent.Docker.NameEquals(i.ToLower())) Startup.Add(RuntimeComponent.Docker);
        else if (RuntimeComponent.Repository.NameEquals(i.ToLower())) Startup.Add(RuntimeComponent.Repository);
        else if (RuntimeComponent.LanguageService.NameEquals(i.ToLower())) Startup.Add(RuntimeComponent.LanguageService);
        else if (RuntimeComponent.Broker.NameEquals(i.ToLower())) Startup.Add(RuntimeComponent.Broker);
      }
      
      DockerUri = new Uri(config.DockerUri);
      RepositoryUri = new Uri(config.RepositoryUri);
      LanguageServiceUri = new Uri(config.LanguageServiceUri);
      var brokerUri = config.BrokerUri.Split(':');
      BrokerUri = new HostAddress(brokerUri[0], int.Parse(brokerUri[1]));
      var brokerWsUri = config.BrokerWebsocketUri.Split(':');
      BrokerWebsocketUri = new HostAddress(brokerWsUri[0], int.Parse(brokerWsUri[1]));

      RepositoryImageName = config.RepositoryImageName;
      RepositoryImageTag = config.RepositoryImageTag;
      RepositoryContainerName = config.RepositoryContainerName;
      RepositoryImageExposedPort = config.RepositoryImageExposedPort;

      LanguageServiceImageName = config.LanguageServiceImageName;
      LanguageServiceImageTag = config.LanguageServiceImageTag;
      LanguageServiceContainerName = config.LanguageServiceContainerName;
      LanguageServiceImageExposedPort = config.LanguageServiceImageExposedPort;

      BrokerImageName = config.BrokerImageName;
      BrokerImageTag = config.BrokerImageTag;
      BrokerContainerName = config.BrokerContainerName;
      BrokerImageExposedMqttPort = config.BrokerImageExposedMqttPort;
      BrokerImageExposedWebsocketPort = config.BrokerImageExposedWebsocketPort;
    }

    #region core
    public Task Run(string[] arguments) {
      return Task.Factory.StartNew(() =>
      {
        try {
          if (arguments == null || arguments.Length == 0) {
            Console.WriteLine("seidlr v0.0.1-alpha\n");
            Console.WriteLine("> starting up...");
            StartupRuntime();

            do {
              Console.Write("> ");
              var line = Console.ReadLine();
              var commands = ParseLine(line.Trim().Split(' '));
              ExecuteCommands(commands);
            } while (!exit);
          }
          else {
            var commands = ParseLine(arguments);
            ExecuteCommands(commands);
          }
        }
        catch (Exception exc) {
        }
        finally {
          TeardownRuntime();
        }
      }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private Dictionary<string, List<string>> ParseLine(string[] arguments) {
      var commands = new Dictionary<string, List<string>>();

      string curCmd = "";
      for (int i = 0; i < arguments.Length; i++) {
        var arg = arguments[i].Trim();
        if (COMMANDS.Contains(arg)) {
          commands.Add(arg, new List<string>());
          curCmd = arg;
        }
        else if (!string.IsNullOrEmpty(curCmd)) {
          commands[curCmd].Add(arg);
        }
      }

      return commands;
    }

    private void ExecuteCommands(Dictionary<string, List<string>> commands) {
      foreach (var cmd in commands) {
        if (cmd.Key == "exit" || cmd.Key == "q" || cmd.Key == "quit") {
          exit = true;
        }
        else if (cmd.Key == "run") {
          string path = null;
          if (cmd.Value != null && cmd.Value.Count > 0) path = cmd.Value[0];
          if (string.IsNullOrEmpty(path)) {
            // look for *.3l inside current dir and subdirs
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.3l", SearchOption.AllDirectories);
            if(files.Length > 0) path = files[0];
          }

          if(!string.IsNullOrEmpty(path)) {
            PerformRunSeidl_Docker(path).Wait();
            runningTask = Guid.NewGuid().ToString();
          }
        }
        else if (cmd.Key == "run-demo") {
          string path = @"..\..\..\..\DemoApps";

          string demofile = "main";
          if(cmd.Value != null && cmd.Value.Count > 0) demofile = cmd.Value[0];
          path = Path.Combine(path, demofile+".3l");

          PerformRunSeidl_Docker(path).Wait();
          runningTask = Guid.NewGuid().ToString();
        }
        else if (cmd.Key == "run-process") {
          string path = @"..\..\..\..\DemoApps";

          string demofile = "main";
          if (cmd.Value != null && cmd.Value.Count > 0) demofile = cmd.Value[0];
          path = Path.Combine(path, demofile + ".3l");

          PerformRunSeidl_Process(path).Wait();
          runningTask = Guid.NewGuid().ToString();
        }
        else if (cmd.Key == "demo") {
          PerformDemo().Wait();
          runningTask = Guid.NewGuid().ToString();
        }
        else if (cmd.Key == "stop") {
          PerformStop().Wait();          
          runningTask = null;
        }
        else if (cmd.Key == "attach") {
          PerformAttachBroker();
        }
        else if (cmd.Key == "state") {
          GetStateBroker().Wait();
        }
        else if (cmd.Key == "list") {          
          if (cmd.Value.Contains("images")) Repository_ListImages().Wait();
          if (cmd.Value.Contains("containers")) Repository_ListContainers().Wait();
          if (cmd.Value.Contains("descriptions")) Repository_ListDescriptions().Wait();
          if (cmd.Value.Contains("packages")) Repository_ListPackages().Wait();
        }
      }
    }
    #endregion core

    #region runtime
    private void StartupRuntime() {
      foreach(var component in Startup) {
        if(component == RuntimeComponent.Docker) StartupDocker();
        else if (component == RuntimeComponent.Repository) StartupRepository();
        else if (component == RuntimeComponent.LanguageService) StartupLanguageService();
        else if (component == RuntimeComponent.Broker) StartupBroker();
      }

      if(Startup.Contains(RuntimeComponent.Repository) && Startup.Contains(RuntimeComponent.LanguageService)) {
        InitRepository();
      }
    }

    private void TeardownRuntime() {
      var teardown = Startup.ToList();
      teardown.Reverse();
      foreach (var component in teardown) {
        if (component == RuntimeComponent.Docker) TeardownDocker();
        else if (component == RuntimeComponent.Repository) TeardownRepository();
        else if (component == RuntimeComponent.LanguageService) TeardownLanguageService();
        else if (component == RuntimeComponent.Broker) TeardownBroker();
      }
    }

    private void StartupDocker() {
      dockerClient = new DockerClientConfiguration(DockerUri).CreateClient();
      ClearupContainers().Wait();
    }

    private void TeardownDocker() {
      ClearupContainers().Wait();
      dockerClient.Dispose();
    }

    private void StartupRepository() {
      // startup repository container
      string containerPath = "/configuration/";
      string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      path = Path.Join(path, "configurations");
      path = Path.Join(path, "repository");
      
      var portBindings = new Dictionary<string, IList<PortBinding>> { { RepositoryImageExposedPort.ToString(), new List<PortBinding>() } };      
      portBindings[RepositoryImageExposedPort.ToString()].Add(new PortBinding() { HostIP = RepositoryUri.Host, HostPort = RepositoryUri.Port.ToString() });
      var hostConfig = new HostConfig() { PortBindings = portBindings };      
      var exposedPorts = new Dictionary<string, EmptyStruct> { { RepositoryImageExposedPort.ToString(), new EmptyStruct() } };

      var parameters = new CreateContainerParameters() {
        Image = $"{RepositoryImageName}:{RepositoryImageTag}",
        Name = RepositoryContainerName,                
        Cmd = new string[] { "spa2.db", "true" },
        Volumes = new Dictionary<string, EmptyStruct>() { { $"{path}:{containerPath}", new EmptyStruct() } },
        ExposedPorts = exposedPorts,
        HostConfig = hostConfig
        //Env = new List<string> { "ASPNETCORE_HTTPS_PORTS=7001", "ASPNETCORE_HTTP_PORTS=7002" }        
        //HostConfig = new HostConfig { Binds = new[] { path } }
      };
      var c = dockerClient.Containers.CreateContainerAsync(parameters).Result;
      repositoryContainer = new Container { Hash = c.ID, Name = parameters.Name };
      dockerClient.Containers.StartContainerAsync(repositoryContainer.Hash, new ContainerStartParameters(), cts.Token).Wait();

     // startup repository client
     //https://learn.microsoft.com/de-de/aspnet/web-api/overview/advanced/calling-a-web-api-from-a-net-client
     HttpClientHandler clientHandler = new HttpClientHandler();
      clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

      repositoryClient = new HttpClient(clientHandler);
      repositoryClient.BaseAddress = RepositoryUri;
      //repositoryClient.DefaultRequestHeaders.Accept.Clear();
      //repositoryClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

      Task.Delay(2000).Wait(); // wait for container and service to start up
      //InitRepositoryDepr().Wait();
    }

    private Task InitRepositoryDepr() {
      return Task.Run(async () =>
      {
        IList<ImagesListResponse> imageList = await dockerClient.Images.ListImagesAsync(new ImagesListParameters() { All = true });
        IList<ContainerListResponse> containerList = await dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
        
        imageList = imageList.Where(x => x.RepoTags.Any()).ToList();


        foreach (var i in imageList.Where(x => x.RepoTags.Last().StartsWith(DOCKER_APPLICATION_ID_PREFIX + "."))) {
          foreach (var t in i.RepoTags) {
            Console.WriteLine(t);
            var nameAndTag = t.Split(':');
            var hash = i.ID.Split(":")[1];
            var img = new Image() { Hash = hash, Name = nameAndTag[0], Tag = nameAndTag[1], Created = i.Created, Size = i.Size };            
            var postResponse = await repositoryClient.PostAsJsonAsync("images", img);
            var x = postResponse.Headers.Location;
            Console.WriteLine(x);
          }
        }


        Console.WriteLine("\n\nStorage results:");
        Console.WriteLine("\nImages:\n");
        var getResponse = repositoryClient.GetAsync("images").Result;
        if (getResponse.IsSuccessStatusCode) {
          var storedImages = await getResponse.Content.ReadFromJsonAsync<List<Image>>();
          foreach (var image in storedImages) {
            Console.WriteLine($"{image.Id} | {image.Name}:{image.Tag}");
          }
        }

      });
    }

    private void TeardownRepository() {
      Task.Run(async () => {
        if (repositoryClient != null) repositoryClient.Dispose();
        if (repositoryContainer != null) {
          await dockerClient.Containers.StopContainerAsync(repositoryContainer.Hash, new ContainerStopParameters() { WaitBeforeKillSeconds = 1 });
          await dockerClient.Containers.RemoveContainerAsync(repositoryContainer.Hash, new ContainerRemoveParameters() { Force = true });
        }
      }).Wait();
    }

    private void StartupLanguageService() {
      Task.Delay(1000).Wait();
      var portBindings = new Dictionary<string, IList<PortBinding>> { { LanguageServiceImageExposedPort.ToString(), new List<PortBinding>() } };
      portBindings[LanguageServiceImageExposedPort.ToString()].Add(new PortBinding() { HostIP = LanguageServiceUri.Host, HostPort = LanguageServiceUri.Port.ToString() });
      var hostConfig = new HostConfig() { PortBindings = portBindings };
      var exposedPorts = new Dictionary<string, EmptyStruct> { { LanguageServiceImageExposedPort.ToString(), new EmptyStruct() } };

      var parameters = new CreateContainerParameters() {
        Image = $"{LanguageServiceImageName}:{LanguageServiceImageTag}",
        Name = LanguageServiceContainerName,
        Cmd = new string[] { LanguageServiceImageExposedPort.ToString(), RepositoryUri.Port.ToString() },        
        ExposedPorts = exposedPorts,
        HostConfig = hostConfig
      };
      var c = dockerClient.Containers.CreateContainerAsync(parameters).Result;
      languageServiceContainer = new Container { Hash = c.ID, Name = parameters.Name };
      dockerClient.Containers.StartContainerAsync(languageServiceContainer.Hash, new ContainerStartParameters(), cts.Token).Wait();

      // setup repository client
      HttpClientHandler clientHandler = new HttpClientHandler();
      clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
      languageServiceClient = new HttpClient(clientHandler);
      languageServiceClient.BaseAddress = LanguageServiceUri;

      Task.Delay(2000).Wait(); // wait for container and service to start up
    }

    private void TeardownLanguageService() {
      Task.Run(async () => {
        if(languageServiceClient != null) languageServiceClient.Dispose();
        if(languageServiceContainer != null) {
          await dockerClient.Containers.StopContainerAsync(languageServiceContainer.Hash, new ContainerStopParameters() { WaitBeforeKillSeconds = 1 });
          await dockerClient.Containers.RemoveContainerAsync(languageServiceContainer.Hash, new ContainerRemoveParameters() { Force = true });
        }
      });
    }

    private void StartupBroker() {
      //// load internal configs
      //brokerConfigBase = Parser.Parse<BrokerConfiguration>("./configurations/Broker.yml");
      //socketConfigBase = Parser.Parse<SocketConfiguration>("./configurations/Socket.yml");

      string containerPath = "/configuration/";
      string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      path = Path.Join(path, "configurations");
      path = Path.Join(path, "broker");

      // https://stackoverflow.com/questions/70900149/what-equivalent-in-docker-dotnet-net-core-docker-parameters-expose-and-p
      var portBindings = new Dictionary<string, IList<PortBinding>> { { BrokerUri.Port.ToString(), new List<PortBinding>() }, { BrokerWebsocketUri.Port.ToString(), new List<PortBinding>() } };
      portBindings[BrokerUri.Port.ToString()].Add(new PortBinding() { HostIP = BrokerUri.Name, HostPort = BrokerUri.Port.ToString() });
      portBindings[BrokerWebsocketUri.Port.ToString()].Add(new PortBinding() { HostIP = BrokerWebsocketUri.Name, HostPort = BrokerWebsocketUri.Port.ToString() });
      var hostConfig = new HostConfig() { 
        PortBindings = portBindings
        //,Binds = new[] { path }
      };
      var exposedPorts = new Dictionary<string, EmptyStruct> { { BrokerUri.Port.ToString(), new EmptyStruct() }, { BrokerWebsocketUri.Port.ToString(), new EmptyStruct() } };

      // startup repository container
      var parameters = new CreateContainerParameters() {
        Image = $"{BrokerImageName}:{BrokerImageTag}",
        Name = BrokerContainerName,
        Cmd = new string[] { BrokerUri.Name, BrokerUri.Port.ToString() },
        Volumes = new Dictionary<string, EmptyStruct>() { { $"{path}:{containerPath}", new EmptyStruct() } },
        ExposedPorts = exposedPorts,
        HostConfig = hostConfig
      };
      var c = dockerClient.Containers.CreateContainerAsync(parameters).Result;
      brokerContainer = new Container { Hash = c.ID, Name = parameters.Name };
      dockerClient.Containers.StartContainerAsync(brokerContainer.Hash, new ContainerStartParameters(), cts.Token).Wait();
    }

    private void TeardownBroker() {
      Task.Run(async () => {        
        if (brokerContainer != null) {
          await dockerClient.Containers.StopContainerAsync(brokerContainer.Hash, new ContainerStopParameters() { WaitBeforeKillSeconds = 1 });
          await dockerClient.Containers.RemoveContainerAsync(brokerContainer.Hash, new ContainerRemoveParameters() { Force = true });
        }
      }).Wait();
    }

    private Task InitRepository() {
      return Task.Run(async () => {
        var getResponse = await languageServiceClient.GetAsync("descriptions/images");
        if(getResponse.IsSuccessStatusCode) {
          var describedImages = await getResponse.Content.ReadFromJsonAsync<List<string>>(); //name:tag

          IList<ImagesListResponse> imageList = await dockerClient.Images.ListImagesAsync(new ImagesListParameters() { All = true });
          imageList = imageList.Where(x => x.RepoTags.Any()).ToList();

          foreach(var describedImage in describedImages) {
            var dockerImage = imageList.Where(x => x.RepoTags.Contains(describedImage)).FirstOrDefault();
            if(dockerImage != null) {
              var nameTag = describedImage.Split(':');
              var hash = dockerImage.ID.Split(':')[1];
              var img = new Image() { Hash = hash, Name = nameTag[0], Tag = nameTag[1], Created = dockerImage.Created, Size = dockerImage.Size };
              var postResponse = await repositoryClient.PostAsJsonAsync("images", img);
              var x = postResponse.Headers.Location;
            }
          }

        }
      }, cts.Token);     
    }
    #endregion runtime~

    #region commands

    private Task PerformList() {
      return Task.Run(async () =>
      {
        IList<ImagesListResponse> imageList = await dockerClient.Images.ListImagesAsync(new ImagesListParameters() { All = true });
        IList<ContainerListResponse> containerList = await dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });

        Console.WriteLine("\nImages:\n");
        foreach (var i in imageList.Where(x => x.RepoTags.Last().StartsWith(DOCKER_APPLICATION_ID_PREFIX + "."))) {
          foreach (var t in i.RepoTags) {
            Console.WriteLine(t);
          }
        }
        Console.WriteLine("\nContainers:\n");
        foreach (var c in containerList.Where(x => x.Names.First().Contains(DOCKER_APPLICATION_ID_PREFIX + "."))) {
          Console.WriteLine(c.Names.Last() + "\timage: " + c.Image + " | " + c.ImageID);
        }
      }, cts.Token);
    }

    private Task Repository_ListImages() {
      return Task.Run(async () => {
        var getResponse = await repositoryClient.GetAsync("images");
        if (getResponse.IsSuccessStatusCode) {
          var storedImages = await getResponse.Content.ReadFromJsonAsync<List<Image>>();
          foreach (var image in storedImages) {
            Console.WriteLine($"{image.Id} | {image.Name}:{image.Tag}");
          }
        }
      }, cts.Token);
    }

    private Task Repository_ListContainers() {
      return Task.Run(async () => {
        var getResponse = repositoryClient.GetAsync("containers").Result;
        if (getResponse.IsSuccessStatusCode) {
          var storedContainers = await getResponse.Content.ReadFromJsonAsync<List<Container>>();
          foreach (var container in storedContainers) {
            Console.WriteLine($"{container.Id} | {container.Name}");
          }
        }
      }, cts.Token);
    }

    private Task Repository_ListDescriptions() {
      return Task.Run(async () => {
        var getResponse = repositoryClient.GetAsync("descriptions").Result;
        if (getResponse.IsSuccessStatusCode) {
          var storedDescriptions = await getResponse.Content.ReadFromJsonAsync<List<Description>>();
          foreach (var description in storedDescriptions) {
            Console.WriteLine($"{description.Id} | {description.Name}:{description.Tag}");
          }
        }
      }, cts.Token);
    }

    private Task Repository_ListPackages() {
      return Task.Run(async () => {
        var getResponse = repositoryClient.GetAsync("packages").Result;
        if (getResponse.IsSuccessStatusCode) {
          var storedPackages = await getResponse.Content.ReadFromJsonAsync<List<Package>>();
          foreach (var package in storedPackages) {
            Console.WriteLine($"{package.Id} | {package.Name}:{package.Tag}");
          }
        }
      }, cts.Token);
    }

    private Task GetStateBroker() {
      //https://github.com/dotnet/Docker.DotNet/issues/379
      return Task.Run(async () =>
      {
        try {
          if (brokerContainer != null) {
            var parameters = new ContainerLogsParameters() { ShowStdout = true, ShowStderr = true };
            var stream = await dockerClient.Containers.GetContainerLogsAsync(brokerContainer.Hash, parameters, cts.Token);
            using (var reader = new StreamReader(stream)) {
              string log = reader.ReadToEnd();
              Console.WriteLine(log);
            }
          }
        }
        catch (Exception ex) {
          Console.WriteLine(ex.Message);
        }
      }, cts.Token);
    }

    private Task PerformStop() {
      return Task.Run(async () => {
        foreach (var p in activeProcesses) {
          p.Kill(true);
        }
        foreach (var c in activeContainers) {
          try {
            IList<ContainerListResponse> containerList = await dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
            var removalContainerList = containerList.Where(x => activeContainers.Select(x => x.ID).Contains(c.ID)).ToList();
            foreach (var rc in removalContainerList) {
              await dockerClient.Containers.StopContainerAsync(rc.ID, new ContainerStopParameters() { WaitBeforeKillSeconds = 1 });
              await dockerClient.Containers.RemoveContainerAsync(rc.ID, new ContainerRemoveParameters() { Force = true });
            }
          }
          catch (Exception ex) {
            Console.WriteLine(ex.Message);
          }
        }
      });
    }
    #endregion commands

    #region helper
    private Task ClearupContainers() {
      return Task.Run(async () => {
        try {
          IList<ContainerListResponse> containerList = await dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
          var removalContainerList = containerList.Where(x => x.Names.Any(y => y.Contains(DOCKER_RUNTIME_ID_PREFIX + ".") || y.Contains(DOCKER_APPLICATION_ID_PREFIX + ".")));
          foreach (var c in removalContainerList) {
            await dockerClient.Containers.StopContainerAsync(c.ID, new ContainerStopParameters() { WaitBeforeKillSeconds = 1 });
            await dockerClient.Containers.RemoveContainerAsync(c.ID, new ContainerRemoveParameters() { Force = true });
          }
        }
        catch (Exception ex) {

        }
      }, cts.Token);
    }

    #endregion helper

    #region sandbox
    private Task PerformAttachBroker() {
      return Task.Run(async () =>
      {
        try {
          if (brokerContainer != null) {
            var stream = await dockerClient.Containers.AttachContainerAsync(brokerContainer.Hash, false, new ContainerAttachParameters() { Stdin = true, Stderr = true, Stdout = true, Stream = true, DetachKeys = "q" });
            var output = await stream.ReadOutputToEndAsync(cts.Token);
            Console.WriteLine(output.stdout);
          }
        }
        catch (Exception ex) {
          Console.WriteLine(ex.Message);
        }
      }, cts.Token);
    }

    private Task PerformRunSeidl_Process(string path) {
      return Task.Run(async () =>
      {
        try {
          string runId = Guid.NewGuid().ToString();
          string text = File.ReadAllText(path);
          var pr = new ProgramRecord(text);

          // 
          var routingResponse = await languageServiceClient.PostAsJsonAsync("translate/routing", pr);
          if (routingResponse.IsSuccessStatusCode) {
            var rt = await routingResponse.Content.ReadFromJsonAsync<RoutingTable>();
          }

          var postResponse = await languageServiceClient.PostAsJsonAsync("translate/initializations", pr);
          if (postResponse.IsSuccessStatusCode) {
            var inits = await postResponse.Content.ReadFromJsonAsync<List<InitializationRecord>>();

            // TODO:
            //var containerTasks = new List<Task<CreateContainerResponse>>();
            // DONE:
            var processList = new List<Process>();

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

                //Console.WriteLine(_point.Id);
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
              init.parameters.Add("applicationParametersNetworking", new ApplicationParametersNetworking(BrokerUri.Name, BrokerUri.Port));

              Process process = new Process();              
              process.StartInfo.WorkingDirectory = init.exe.workingDirectory;
              process.StartInfo.FileName = init.exe.command;
              
              process.StartInfo.Arguments = init.exe.arguments 
                + " \"" + JsonSerializer.Serialize(init.parameters).Replace("\"", "\\\"") + "\""
                + " \"" + JsonSerializer.Serialize(rt).Replace("\"", "\\\"") + "\"";
              process.StartInfo.UseShellExecute = true;
              //process.StartInfo.CreateNoWindow = true;
              processList.Add(process);
            }

            foreach(var p in processList) {
              p.Start();              
              activeProcesses.Add(p);
            }


          }
          else {
            Console.WriteLine(postResponse.StatusCode);
          }
        }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
      }, cts.Token);
    }

    private Task PerformRunSeidl_Docker(string path) {
      return Task.Run(async () =>
      {
        try {
          string runId = Guid.NewGuid().ToString();
          string text = File.ReadAllText(path);
          var pr = new ProgramRecord(text);

          // 
          var routingResponse = await languageServiceClient.PostAsJsonAsync("translate/routing", pr);
          if(routingResponse.IsSuccessStatusCode) {
            var rt = await routingResponse.Content.ReadFromJsonAsync<RoutingTable>();
          }

          var postResponse = await languageServiceClient.PostAsJsonAsync("translate/initializations", pr);
          if (postResponse.IsSuccessStatusCode) {            
            var inits = await postResponse.Content.ReadFromJsonAsync<List<InitializationRecord>>();                       
            
            var containerTasks = new List<Task<CreateContainerResponse>>();
            foreach(var init in inits) {
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
                foreach(var _port in _point.Ports.Where(x => x.Type == PortType.Out || x.Type == PortType.Server)) {
                  _port.Address = $"{runId}/{_point.Id}/{_port.Id}";
                }

                //Console.WriteLine(_point.Id);
              }

              //Console.WriteLine("\nRoutes:");
              foreach(var _route in rt.Routes) {
                _route.SourcePort.Address = $"{runId}/{_route.Source.Id}/{_route.SourcePort.Id}";
                _route.SinkPort.Address = $"{runId}/{_route.Source.Id}/{_route.SourcePort.Id}";

                //Console.WriteLine(_route.Source.Id + "." + _route.SourcePort.Id + " --> " + _route.Sink.Id + "." + _route.SinkPort.Id);
              }

              init.parameters["name"] = init.name;
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
            foreach(var t in containerTasks) {
              containerStarts.Add(dockerClient.Containers.StartContainerAsync(t.Result.ID, new ContainerStartParameters()));              
            }

            await Task.WhenAll(containerStarts);
            foreach (var c in containerTasks) {
              activeContainers.Add(c.Result);
            }
          }
          else {
            Console.WriteLine(postResponse.StatusCode);
          }

          //brokerConfigBase.Routing = CreateRoutingTable(table);


        }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
      }, cts.Token);
    }


    private ScopedSymbolTable ReadSeidl(string path) {
      if (string.IsNullOrEmpty(path)) path = @"..\..\..\..\DemoApps\main.3l";
      string text = File.ReadAllText(path);
      var parser = Utils.TokenizeAndParse(text);
      var linter = new Linter(parser);
      linter.ProgramTextUrl = path;

      return linter.CreateScopedSymbolTableSecured();
    }



    #endregion sandbox

    #region demo

    private Task PerformDemo() {
      return Task.Run(async () =>
      {
        try {
          string producerContainerName = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.producer.ctn";
          string consumerContainerName = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.consumer.ctn";
          var consumerTask = dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters()
          {
            Image = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.consumer.img:latest",
            Name = consumerContainerName,
            Cmd = new string[] { "Consumer!!!" }    ,            
          });
          
          string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
          path = Path.Join(path, "configurations");
          path = Path.Join(path, "broker");
          var vol = await dockerClient.Volumes.CreateAsync(new VolumesCreateParameters()
          {
            Name = producerContainerName + ".vol"
          });
          vol.Mountpoint = path;


          var producerTask = dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters()
          {
            Image = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.producer.img:latest",
            Name = producerContainerName,
            Cmd = new string[] { "Producer!!!" },            
            Volumes = new Dictionary<string, EmptyStruct>() { { vol.Name + ":/configurations/broker", new EmptyStruct() } }            
          });

          //string path = Directory.GetCurrentDirectory();
          //string localPath = "/configuration2/";
          //var parameters = new CreateContainerParameters()
          //{
          //  Image = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.consumer.img:latest",
          //  Name = consumerContainerName,
          //  Cmd = new string[] { "blablu" },
          //  Volumes = new Dictionary<string, EmptyStruct>() { { $"{path}:{localPath}", new EmptyStruct() } }
          //};
          CreateContainerResponse consumer = consumerTask.Result;
          CreateContainerResponse producer = producerTask.Result;

          // start containers          
          var consumerStart = dockerClient.Containers.StartContainerAsync(consumer.ID, new ContainerStartParameters());
          var producerStart = dockerClient.Containers.StartContainerAsync(producer.ID, new ContainerStartParameters());

          cts.Token.ThrowIfCancellationRequested();
        }
        catch (Exception ex) {
        }
        finally {
        }
      }, cts.Token);
    }

    #endregion demo
  }
}
