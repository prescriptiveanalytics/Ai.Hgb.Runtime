using Ai.Hgb.Common.Entities;
using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.IO;
using System.Net.Http.Json;
using System.Reflection;

namespace Ai.Hgb.Runtime {

  public enum RuntimeComponents {
    Docker,
    Repository,
    LanguageService,
    Broker
  }

  public class Repl {

    #region properties
    public List<RuntimeComponents> Startup { get; set; }
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
    private static string[] COMMANDS = { "run", "state", "start", "pause", "stop", "attach", "detach", "cancel", "save", "exit", "q", "quit", "list" };
    private static string DOCKER_RUNTIME_ID_PREFIX = "ai.hgb.runtime";
    private static string DOCKER_APPLICATION_ID_PREFIX = "ai.hgb.application";

    private DockerClient dockerClient;
    private HttpClient repositoryClient;
    private Socket brokerClient;
    private HttpClient languageServiceClient;

    private Image brokerImage, repositoryImage, languageServiceImage;
    private Container brokerContainer, repositoryContainer, languageServiceContainer;
    private List<Image> activeImages;
    private List<Container> activeContainers;

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
          //PerformRun(path).Wait();
          //PerformRunSidl(path).Wait();
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
        if(component == RuntimeComponents.Docker) StartupDocker();
        else if (component == RuntimeComponents.Repository) StartupRepository();
        else if (component == RuntimeComponents.LanguageService) StartupLanguageService();
        else if (component == RuntimeComponents.Broker) StartupBroker();
      }

      if(Startup.Contains(RuntimeComponents.Repository) && Startup.Contains(RuntimeComponents.LanguageService)) {
        InitRepository();
      }
    }

    private void TeardownRuntime() {
      var teardown = Startup.ToList();
      teardown.Reverse();
      foreach (var component in teardown) {
        if (component == RuntimeComponents.Docker) TeardownDocker();
        else if (component == RuntimeComponents.Repository) TeardownRepository();
        else if (component == RuntimeComponents.LanguageService) TeardownLanguageService();
        else if (component == RuntimeComponents.Broker) TeardownBroker();
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
    #endregion runtime

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

    private Task PerformRun(string path) {
      return Task.Run(async () =>
      {
        try {
          string producerContainerName = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.producer.container";
          string consumerContainerName = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.consumer.container";
          var consumerTask = dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters()
          {
            Image = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.consumer.image:latest",
            Name = consumerContainerName,
            Cmd = new string[] { "blablu" }
          });
          var producerTask = dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters()
          {
            Image = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.producer.image:latest",
            Name = producerContainerName
          });

          string localPath = "/configuration2/";
          var parameters = new CreateContainerParameters()
          {
            Image = DOCKER_APPLICATION_ID_PREFIX + ".demoapps.consumer.image:latest",
            Name = consumerContainerName,
            Cmd = new string[] { "blablu" },
            Volumes = new Dictionary<string, EmptyStruct>() { { $"{path}:{localPath}", new EmptyStruct() } }
          };
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

    private Task PerformStop() {
      return Task.Run(async () => { });
    }

    #region helper
    private Task ClearupContainers() {
      return Task.Run(async () => {
        try {
          IList<ContainerListResponse> containerList = await dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
          foreach (var c in containerList.Where(x => x.Names.Any(y => y.Contains(DOCKER_RUNTIME_ID_PREFIX + ".")))) {
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
    //private Task PerformRunSidl(string path) {
    //  return Task.Run(async () =>
    //  {
    //    try {
    //      var table = ReadSidl(path);
    //      brokerConfigBase.Routing = CreateRoutingTable(table);


    //    }
    //    catch (Exception ex) { Console.WriteLine(ex.Message); }
    //  }, cts.Token);
    //}

    //private void TestSidl() {
    //  var table = ReadSidl(null);
    //  var printout = table.Print(null);
    //  Console.WriteLine("\nSidl:\n");
    //  Console.WriteLine(printout.ToString());
    //}
    
    //private RoutingTable CreateRoutingTable(ScopedSymbolTable table) {
    //  var routing = new RoutingTable();

    //  // loop over all nodes
    //  foreach (ISymbol s in table.Symbols.Where(x => x.Type == typeof(Seidl.Data.Node))) {
    //    // add nodes to routing table
    //    var n = (Seidl.Data.Node)s.Type;
    //    routing.AddNode(new Dat.Configuration.Node(s.Name, n.GetIdentifier(), n.GetIdentifier())); // TODO: extract fully qualified name from container service layer
    //  }

    //  // loop over all edges
    //  foreach (ISymbol s in table.Symbols.Where(x => x.Type == typeof(Seidl.Data.Edge))) {
    //    // add edges to routing table
    //    var e = (Seidl.Data.Edge)s.Type;
    //    //routing.AddEdge(new DAT.Configuration.Edge(s.Name, e.From)); // TODO see above

    //    var fromNode = routing.Nodes.Where(x => x.Id == e.FromNode).First();
    //    var toNode = routing.Nodes.Where(x => x.Id == e.ToNode).First();
    //    routing.AddEdge(new Dat.Configuration.Edge(s.Name, fromNode, toNode));
    //  }

    //  return routing;
    //}

    //private ScopedSymbolTable ReadSidl(string path) {
    //  if (string.IsNullOrEmpty(path)) path = @"..\..\..\..\DemoApps\main.3l";
    //  string text = File.ReadAllText(path);      
    //  var parser = Utils.TokenizeAndParse(text);
    //  var linter = new Linter(parser);
    //  linter.ProgramTextUrl = path;

    //  return linter.CreateScopedSymbolTableSecured();
    //}
    

    #endregion sandbox
  }
}
