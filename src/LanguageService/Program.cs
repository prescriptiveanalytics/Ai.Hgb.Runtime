using Ai.Hgb.Common.Entities;
using Ai.Hgb.Seidl.Data;
using Ai.Hgb.Seidl.Processor;
using Serilog;
using System.Reflection;

#region fields
// fields and default values
int httpPort = 7003;
string repositoryHost = "host.docker.internal";
int repositoryPort = 8001;


HttpClient repositoryClient;
#endregion fields

// read arguments
if (args.Length > 0) httpPort = int.Parse(args[0]);
if (args.Length == 2) repositoryPort = int.Parse(args[1]);
if (args.Length == 3) repositoryHost = args[2];
repositoryHost = repositoryHost.Replace("localhost", "host.docker.internal");
repositoryHost = repositoryHost.Replace("127.0.0.1", "host.docker.internal");

//repositoryHost = "127.0.0.1";
//repositoryPort = 8001;
//httpPort = 8003;
var repositoryUri = new Uri($"http://{repositoryHost}:{repositoryPort}");

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

// add swagger support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();


// setup repository client
HttpClientHandler clientHandler = new HttpClientHandler();
clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
repositoryClient = new HttpClient(clientHandler);
repositoryClient.BaseAddress = repositoryUri;

// map routes
MapRoutes();

// setup basic packages within repository
await SetupPackages();




// test file
//var text = File.ReadAllText(@"G:\My Drive\FHHAGENBERG\FE\Publications\2024_Eurocast\Presentation\Samples\eurocast2024.3l");
//try {
//  var sst = ParseSST(text);
//  var gr = sst.GetGraph();
//  Console.WriteLine(string.Join(", ", gr.nodes.Select(x => x.name)));
//}
//catch (Exception ex) {
//  Console.WriteLine(ex.Message);
//}

// startup
app.Run();


void MapRoutes() {
  app.MapGet("/", (HttpContext ctx, LinkGenerator link) => "Seidl Language Support");

  app.MapGet("/atomictypes", () => {
    Log.Information("Requesting atomictypes");
    return Results.Ok(Utils.GetAtomicTypeDisplayNames().OrderBy(x => x));
  });

  app.MapGet("/basetypes", () => {
    return Results.Ok(Utils.GetBaseTypeDisplayNames().OrderBy(x => x));
  });

  app.MapGet("/keywords", () => {
    return Results.Ok(Utils.GetKeywordDisplayNames().OrderBy(x => x));
  });

  app.MapGet("/packages", async () => {
    var getResponse = await repositoryClient.GetAsync("packages");
    if(getResponse.IsSuccessStatusCode) {
      var storedPackages = await getResponse.Content.ReadFromJsonAsync<List<Package>>();
      var pkgNameTags = storedPackages.Select(x => $"{x.Name}:{x.Tag}");
      return Results.Ok(pkgNameTags);
    } else {
      Log.Fatal("Could not retrieve any packages from the repository.");
      return Results.Ok("Could not retrieve any packages from the repository.");
    }     
  });

  app.MapGet("/descriptions/images", async () => {
    var getResponse = await repositoryClient.GetAsync($"descriptions");
    if (getResponse.IsSuccessStatusCode) {
      var storedDescriptions = await getResponse.Content.ReadFromJsonAsync<List<Description>>();
      var imageNameTagList = new List<string>();
      foreach(var desc in storedDescriptions) {
        var sst = ParseSST(desc.Text);
        var nodedefs = sst[null].Where(x => x.Type is Node && x.IsTypedef).Select(x => x.Type as Node);
        var imageNameTags = nodedefs.Select(x => $"{x.ImageName}:{x.ImageTag}").ToList();
        imageNameTagList.AddRange(imageNameTags);
      }

      return Results.Ok(imageNameTagList);
    }
    else {
      Log.Fatal($"Could not retrieve the requested descriptions from the repository.");
      return Results.Ok($"Could not retrieve the requested descriptions from the repository.");
    }
  });

  app.MapGet("/descriptions/{id}/images", async (string id) => {
    var getResponse = await repositoryClient.GetAsync($"descriptions/{id}");
    if(getResponse.IsSuccessStatusCode) {
      var storedDescription = await getResponse.Content.ReadFromJsonAsync<Description>();
      var sst = ParseSST(storedDescription.Text);
      var nodedefs = sst[null].Where(x => x.Type is Node && x.IsTypedef).Select(x => x.Type as Node);
      var imageNameTags = nodedefs.Select(x => $"{x.ImageName}:{x.ImageTag}").ToList();
      return Results.Ok(imageNameTags);
    } else {
      Log.Fatal($"Could not retrieve the requested description {id} from the repository.");
      return Results.Ok($"Could not retrieve the requested description {id} from the repository.");
    }
  });

  app.MapPost("/validate", (ProgramRecord req) => {
    try {
      var sst = ParseSST(req.programText);
      return Results.Ok("ok");
    }
    catch (Exception exc) {      
      Log.Fatal(exc.Message);
      return Results.Ok(exc.Message);
    }
  });

  app.MapPost("/nodetypes", (LintRequest req) => {            
    var sst = ParseSST(req.programText);
    var s = sst.GetScope(req.line, req.character);        
    var symbols = sst[s].Where(x => x.Type is Node && x.IsTypedef).Select(x => x.Name);    
    return Results.Ok(symbols);
  });

  app.MapPost("/fields", (LintSymbolRequest req) => {
    try {
      var sst = ParseSST(req.programText);
      var s = sst.GetScope(req.line, req.character);
      var symbol = sst[s, req.symbolName];
      var fields = new List<string>();

      if(symbol.Type is Node) {
        var node = (Node)symbol.Type;
        // add properties
        fields.AddRange(node.Inputs.Keys);
        fields.AddRange(node.Outputs.Keys);
        fields.AddRange(node.Properties.Keys);
      } else if(symbol.Type is Struct) {
        var stru = (Struct)symbol.Type;
        fields.AddRange(stru.Properties.Keys);
      }
      
      return Results.Ok(fields);
    } catch(Exception exc) {
      Log.Fatal(exc.Message);
      return Results.Ok(exc.Message);
    }
  });

  app.MapPost("/visualization/graph", async (ProgramRecord pr) => {
    try {
      var sst = ParseSST(pr.programText);
      var gr = sst.GetGraph();
      return Results.Ok(gr);
    }
    catch (Exception exc) {
      Log.Fatal(exc.Message);
      return Results.Ok(exc.Message);
    }
  });

  app.MapPost("/translate/description", (ProgramRecord pr) => {
    try {
      var sst = ParseSST(pr.programText);
      var desc = new Description() {
        Name = sst.Name,
        Tag = sst.Tag,
        Text = pr.programText
      };
      return Results.Ok(desc);
    } catch(Exception exc) {
      Log.Fatal(exc.Message);
      return Results.Ok(exc.Message);
    }
  });

}

ScopedSymbolTable ParseSST(string programText) {
  SeidlParser parser = Utils.TokenizeAndParse(programText);
  Linter linter = new Linter(parser);
  linter.RepositoryClient = repositoryClient;
  return linter.CreateScopedSymbolTable();  
}

ScopedSymbolTable IdentifySST(string programText) {
  SeidlParser parser = Utils.TokenizeAndParse(programText);
  Linter linter = new Linter(parser);
  linter.RepositoryClient = repositoryClient;
  return linter.IdentifyScopedSymbolTable();
}

async Task SetupPackages() {
  try {

    string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    string packagesDir = Path.Join(currentPath, "packages");

    var packageInformations = new List<PackageInformation>();

    foreach (var dir in Directory.GetDirectories(packagesDir)) {
      foreach (var file in Directory.GetFiles(dir, "*.3l")) {
        var descName = Path.GetFileNameWithoutExtension(file);
        var descTag = "latest";

        var programText = Utils.ReadFile(file);
        var sst = IdentifySST(programText);
        if (!string.IsNullOrEmpty(sst.Name)) descName = sst.Name;
        if (!string.IsNullOrEmpty(sst.Tag)) descTag = sst.Tag;

        var desc = new Description() { Name = sst.Name, Tag = sst.Tag };
        desc.Text = programText;

        // check if description = package description
        var pkgIs = sst[null].Where(x => x.Type is PackageInformation).Select(x => x.Type as PackageInformation);

        if (pkgIs != null && pkgIs.Any()) {
          packageInformations.AddRange(pkgIs);
        }
        //else {
          // persist description
          var postResponse = await repositoryClient.PostAsJsonAsync("descriptions", desc);          
          if (postResponse.IsSuccessStatusCode) Log.Information("Persisted description.");
          else Log.Fatal(postResponse.ReasonPhrase);
        //}
      }
    }

    // persist package(s)
    foreach (var pkgi in packageInformations) {
      var pkg = new Package() { Name = pkgi.Identifier.Name, Tag = pkgi.Identifier.Tag };
      var postResponse = await repositoryClient.PostAsJsonAsync("packages", pkg);
      if (postResponse.IsSuccessStatusCode) {
        Log.Information("Persisted package.");
        var pkgId = await postResponse.Content.ReadFromJsonAsync<string>();

        // add descriptions
        var descNameTags = pkgi.DescriptionIdentifiers.Select(x => Tuple.Create(x.Name, x.Tag));
        var postResponse2 = await repositoryClient.PostAsJsonAsync($"packages/{pkgId}/descriptions", descNameTags);
        if (postResponse2.IsSuccessStatusCode) Log.Information("Added descriptions to package.");
        else Log.Fatal(postResponse2.ReasonPhrase);
      }
    }
  }catch(Exception exc) {
    Log.Fatal(exc.Message);
    Console.WriteLine(exc.Message);
  }
}