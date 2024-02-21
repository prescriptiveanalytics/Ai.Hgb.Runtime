// TODO:
// fix certificate bug: https://stackoverflow.com/questions/61197086/unable-to-configure-asp-net-https-endpoint-in-linux-docker-on-windows

using Ai.Hgb.Common.Entities;
using Ai.Hgb.Runtime.Repository;
using Serilog;

string dbName = "spa.db";
bool initialize = true;
string pathBase = "/spa/repository";
string httpIp = "127.0.0.1";
int httpPort = 7001;
string httpsIp = "127.0.0.1";
int httpsPort = 7002;

if (args.Length > 0) dbName = args[0];
if (args.Length > 1) initialize = bool.Parse(args[1]);
Console.WriteLine(args.Length);
Console.WriteLine(dbName);
Console.WriteLine(initialize);

var builder = WebApplication.CreateBuilder();
//var builder = WebApplication.CreateBuilder(args);

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

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection(); // TODO: fix https certificate error
//app.UsePathBase("/spa/repository");
//app.UseRouting();

// setup repository
var repMan = new Manager(dbName, initialize);
//TestManager.Run(repMan);

// map routes
MapRoutes();

// startup
app.Run();


void MapRoutes() {

  app.MapGet("/images", async () =>
  {
    var images = await repMan.GetImagesAsync();
    return Results.Ok(images);
  })
  .WithName("GetImages")
  .WithOpenApi();


  app.MapGet("/containers", async () =>
  {
    var containers = await repMan.GetContainersAsync();
    return Results.Ok(containers);
  });

  app.MapGet("/descriptions", async () =>
  {
    var descriptions = await repMan.GetDescriptionsAsync();
    return Results.Ok(descriptions);
  });

  app.MapGet("/packages", async () =>
  {
    var packages = await repMan.GetPackagesAsync();
    return Results.Ok(packages);
  });

  app.MapGet("/images/{id}", async (string id) =>
  {
    return Results.Ok(await repMan.GetImageAsync(id));
  });

  app.MapGet("/containers/{id}", async (string id) =>
  {
    return Results.Ok(await repMan.GetContainerAsync(id));
  });

  app.MapGet("/descriptions/{id}", async (string id) =>
  {
    return Results.Ok(await repMan.GetDescriptionAsync(id));
  });

  app.MapGet("/packages/{id}", async (string id) =>
  {
    return Results.Ok(await repMan.GetPackageAsync(id));
  });

  app.MapGet("/images/find/{name}/{tag}", async (string name, string tag) =>
  {
    return Results.Ok(await repMan.GetImageByNameTagAsync(name, tag));
  });

  app.MapGet("/containers/find/{name}", async (string name) =>
  {
    return Results.Ok(await repMan.GetContainerByNameAsync(name));
  });

  app.MapGet("/descriptions/find/{name}/{tag}", async (string name, string tag) =>
  {
    return Results.Ok(await repMan.GetDescriptionByNameTagAsync(name, tag));
  });

  app.MapGet("/packages/find/{name}/{tag}", async (string name, string tag) =>
  {
    return Results.Ok(await repMan.GetPackageByNameTagAsync(name, tag));
  });

  app.MapPost("/images", async (Image img) =>
  {
    var newImg = await repMan.UpsertImageAsync(img);
    return Results.Ok(newImg.Id);
  });

  app.MapPost("/containers", async (Container ctn) =>
  {
    var newCtn = await repMan.UpsertContainerAsync(ctn);
    return Results.Ok(newCtn.Id);
  });

  app.MapPost("/descriptions", async (Description dsc) =>
  {
    var newDsc = await repMan.UpsertDescriptionAsync(dsc);
    return Results.Ok(newDsc.Id);
  });

  app.MapPost("/packages", async (Package pkg) =>
  {
    var newPkg = await repMan.UpsertPackageAsync(pkg);
    return Results.Ok(newPkg.Id);
  });

  app.MapPost("/packages/{id}/descriptions", async (string id, List<Tuple<string, string>> descNameTags) =>
  {
    var pkg = await repMan.GetPackageAsync(id);
    foreach (var nameTag in descNameTags) {
      var desc = await repMan.GetDescriptionByNameTagAsync(nameTag.Item1, nameTag.Item2);
      pkg.Descriptions.Add(desc);
    }
    var updatedPkg = await repMan.UpsertPackageAsync(pkg);

    return Results.Ok(updatedPkg.Id);
  });

  app.MapDelete("/images/{id}", async (string id) =>
  {
    await repMan.RemoveImageAsync(id);
    return Results.NoContent();
  });

  app.MapDelete("/containers/{id}", async (string id) =>
  {
    await repMan.RemoveContainerAsync(id);
    return Results.NoContent();
  });

  app.MapDelete("/descriptions/{id}", async (string id) =>
  {
    await repMan.RemoveDescriptionAsync(id);
    return Results.NoContent();
  });

  app.MapDelete("/packages/{id}", async (string id) =>
  {
    await repMan.RemovePackageAsync(id);
    return Results.NoContent();
  });

  app.MapDelete("/images", async () =>
  {
    await repMan.RemoveImagesAsync();
    return Results.NoContent();
  });

  app.MapDelete("/containers", async () =>
  {
    await repMan.RemoveContainersAsync();
    return Results.NoContent();
  });

  app.MapDelete("/descriptions", async () =>
  {
    await repMan.RemoveDescriptionsAsync();
    return Results.NoContent();
  });

  app.MapDelete("/packages", async () =>
  {
    await repMan.RemovePackagesAsync();
    return Results.NoContent();
  });
}

