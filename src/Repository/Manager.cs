using Ai.Hgb.Common.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Xml.Linq;

namespace Ai.Hgb.Runtime.Repository {
  
  // TODO: implement all actions related to docker client
  // TODO: implement data base access
  // TODO implement pocos for images and containers
  // TODO: list, save, update, remove actions (CRUD) for images and containers

  public class Manager {
    public string DatabaseFileName { get; set; }
    public string DatabasePath { get; }

    private Context ctx;

    public Manager() { }

    public Manager(string databaseFileName, bool initialize = true) {
      DatabaseFileName = databaseFileName;

      string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      string dir = Path.Join(currentPath, "database");
      DatabasePath = Path.Join(dir, databaseFileName);

      if(initialize) Initialize();
      else ctx = new Context(DatabasePath);
    }

    private void Initialize() {
      try {
        if (File.Exists(DatabasePath)) {
          File.Delete(DatabasePath);
        }
        else {
          Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
          File.Create(DatabasePath);          
        }
      } catch (Exception exc) {
        Console.WriteLine(exc.Message);
      }

      try {
        ctx = new Context(DatabasePath);        
        ctx.Database.EnsureCreated();
      } catch(Exception exc) {
        Console.WriteLine(exc.Message);
      }
    }

    public Task<List<Image>> GetImagesAsync() {      
      return ctx.Images.ToListAsync();
    }

    public Task<List<Container>> GetContainersAsync() {
      return ctx.Containers.ToListAsync();      
    }

    public Task<List<Description>> GetDescriptionsAsync() {
      return ctx.Descriptions.ToListAsync();
    }

    public Task<List<Package>> GetPackagesAsync() {
      return ctx.Packages.ToListAsync();
    }

    public ValueTask<Image> GetImageAsync(string id) {
      return ctx.Images.FindAsync(id)!;      
    }

    public Task<Image> GetImageByHashTagAsync(string hash, string tag) {
      return ctx.Images.FirstOrDefaultAsync(x => x.Hash == hash && x.Tag == tag)!;
    }

    public Task<Image> GetImageByNameTagAsync(string name, string tag) {
      return ctx.Images.FirstOrDefaultAsync(x => x.Name == name && x.Tag == tag)!;
    }

    public ValueTask<Container> GetContainerAsync(string id) {
      return ctx.Containers.FindAsync(id)!;
    }

    public Task<Container> GetContainerByHashAsync(string hash) {
      return ctx.Containers.FirstOrDefaultAsync(x => x.Hash == hash)!;
    }

    public Task<Container> GetContainerByNameAsync(string name) {     
      return ctx.Containers.FirstOrDefaultAsync(x => x.Name == name)!;
    }

    public ValueTask<Description> GetDescriptionAsync(string id) {
      return ctx.Descriptions.FindAsync(id)!;      
    }

    public Task<Description> GetDescriptionByNameTagAsync(string name, string tag) {
      return ctx.Descriptions.FirstOrDefaultAsync(x => x.Name == name && x.Tag == tag)!;
    }

    public ValueTask<Package> GetPackageAsync(string id) { 
      return ctx.Packages.FindAsync(id)!;
    }

    public Task<Package> GetPackageByNameTagAsync(string name, string tag) {
      return ctx.Packages.FirstOrDefaultAsync(x => x.Name == name && x.Tag == tag)!;
    }

    public async Task<Image> UpsertImageAsync(Image image) {
      Image updatedImage = null!;
      if (!string.IsNullOrEmpty(image.Id)) updatedImage = await GetImageAsync(image.Id);

      if (updatedImage != null) {
        var tmpId = updatedImage.Id;
        updatedImage = image;
        updatedImage.Id = tmpId;
      }
      else {
        updatedImage = (await ctx.Images.AddAsync(image)).Entity;
      }

      await ctx.SaveChangesAsync();
      return updatedImage;
    }

    public async Task<Container> UpsertContainerAsync(Container container) {
      Container updatedContainer = null!;
      if (!string.IsNullOrEmpty(container.Id)) updatedContainer = await GetContainerAsync(container.Id);

      if (updatedContainer != null) {
        var tmpId = updatedContainer.Id;
        updatedContainer = container;
        updatedContainer.Id = tmpId;
      }
      else {
        updatedContainer = (await ctx.Containers.AddAsync(container)).Entity;
      }

      await ctx.SaveChangesAsync();
      return updatedContainer;
    }

    public async Task<Description> UpsertDescriptionAsync(Description description) {
      Description updatedDescription = null!;
      if (!string.IsNullOrEmpty(description.Id)) updatedDescription = await GetDescriptionAsync(description.Id);

      if (updatedDescription != null) {
        var tmpId = updatedDescription.Id;
        updatedDescription = description;
        updatedDescription.Id = tmpId;
      }
      else {
        updatedDescription = (await ctx.Descriptions.AddAsync(description)).Entity;
      }

      await ctx.SaveChangesAsync();
      return updatedDescription;
    }

    public async Task<Package> UpsertPackageAsync(Package package) {
      Package updatedPackage = null!;
      if (!string.IsNullOrEmpty(package.Id)) updatedPackage = await GetPackageAsync(package.Id);

      if (updatedPackage != null) {
        var tmpId = updatedPackage.Id;
        updatedPackage = package;
        updatedPackage.Id = tmpId;
      }
      else {
        updatedPackage = (await ctx.Packages.AddAsync(package)).Entity;
      }

      await ctx.SaveChangesAsync();
      return updatedPackage;
    }

    public async Task RemoveImagesAsync() {
      ctx.RemoveRange(ctx.Images);
      await ctx.SaveChangesAsync();
    }

    public async Task RemoveImageAsync(string id) {
      var image = await GetImageAsync(id);
      if(image != null) {
        ctx.Remove(image);        
        await ctx.SaveChangesAsync();
      }
    }

    public async Task RemoveImageByHashTagAsync(string hash, string tag) {
      var image = await GetImageByHashTagAsync(hash, tag);
      if (image != null) {
        ctx.Remove(image);
        await ctx.SaveChangesAsync();
      }
    }

    public async Task RemoveContainersAsync() {
      ctx.RemoveRange(ctx.Containers);
      await ctx.SaveChangesAsync();
    }

    public async Task RemoveContainerAsync(string id) {
      var container = await GetContainerAsync(id);
      if(container != null) {
        ctx.Remove(container);
        await ctx.SaveChangesAsync();
      }
    }

    public async Task RemoveDescriptionsAsync() {
      ctx.RemoveRange(ctx.Descriptions);
      await ctx.SaveChangesAsync();
    }

    public async Task RemoveDescriptionAsync(string id) {
      var description = await GetDescriptionAsync(id);      
      if (description != null) {
        ctx.Remove(description);
        await ctx.SaveChangesAsync();
      }      
    }

    public async Task RemovePackagesAsync() {
      ctx.RemoveRange(ctx.Packages);
      await ctx.SaveChangesAsync();
    }

    public async Task RemovePackageAsync(string id) {
      var package = await GetPackageAsync(id);
      if (package != null) {
        ctx.Remove(package);
        await ctx.SaveChangesAsync();        
      }
    }

  }
}
