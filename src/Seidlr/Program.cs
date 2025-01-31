using Ai.Hgb.Dat.Configuration;
using Ai.Hgb.Runtime;
using YamlDotNet.Serialization;

var dser = new DeserializerBuilder()
  .IgnoreFields()
  .IgnoreUnmatchedProperties()
  .Build();

ReplConfiguration config = null;
string configUri = null;

if ((args == null || args.Length == 0) && File.Exists(@"configurations/repl/config.yml")) {
  configUri = @"configurations/repl/config.yml";
}
else if (File.Exists(args[0])) {
  configUri = args[0];
  args = args.Skip(1).ToArray();
}

if (configUri != null) {
  string doc = Parser.ReadText(configUri);
  config = dser.Deserialize<ReplConfiguration>(doc);
  var repl = new Repl(config);
  await repl.Run(args);
}
else {
  Console.WriteLine("No configuration found. Bye bye.\n");
}









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