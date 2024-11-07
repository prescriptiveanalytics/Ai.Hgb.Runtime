using Ai.Hgb.Dat.Configuration;
using Ai.Hgb.Runtime;

var repl = new Repl {
  //Startup = new List<RuntimeComponents> { RuntimeComponents.Docker, RuntimeComponents.Repository, RuntimeComponents.LanguageService, RuntimeComponents.Broker },
  Startup = new List<RuntimeComponents> { RuntimeComponents.Docker, RuntimeComponents.Repository, RuntimeComponents.LanguageService },
  DockerUri = new Uri("npipe://./pipe/docker_engine"),
  RepositoryUri = new Uri("http://localhost:8001/"),
  LanguageServiceUri = new Uri("http://localhost:8003/"),
  BrokerUri = new HostAddress("127.0.0.1", 1883),
  BrokerWebsocketUri = new HostAddress("127.0.0.1", 5000),
  RepositoryImageName = "ai.hgb.runtime.repository.img",
  RepositoryImageTag = "latest",
  RepositoryImageExposedPort = 7001,
  RepositoryContainerName = "ai.hgb.runtime.repository.ctn",
  LanguageServiceImageName = "ai.hgb.runtime.languageservice.img",
  LanguageServiceImageTag = "latest",
  LanguageServiceImageExposedPort = 7003,
  LanguageServiceContainerName = "ai.hgb.runtime.languageservice.ctn",
  BrokerImageName = "ai.hgb.runtime.broker.img",
  BrokerImageTag = "latest",
  BrokerContainerName = "ai.hgb.runtime.broker.ctn",
  BrokerImageExposedMqttPort = 1883,
  BrokerImageExposedWebsocketPort = 5000
};
await repl.Run(args);
// tcp:// 10.20.71.151:2376

// TODO:
// run = run + 3l file or run + main.3l = read sidl file and...
// generate routing table based on sidl file
// filter routing table for each node
// write table to socket config for node
// write config to file system
// pass customized config to node using volume mounting
// pass volume/path via cmd argument

// // alternatives for repl setup: read from args, read from sidl file