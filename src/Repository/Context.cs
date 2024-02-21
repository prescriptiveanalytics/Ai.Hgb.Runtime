using Ai.Hgb.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ai.Hgb.Runtime.Repository {
  public class Context : DbContext {

    public DbSet<Image> Images { get; set; }
    public DbSet<Container> Containers { get; set; }
    public DbSet<Description> Descriptions { get; set; }
    public DbSet<Package> Packages { get; set; }

    public string DbPath { get; }

    public Context(string databasePath) {
      DbPath = databasePath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
      //optionsBuilder.UseSqlite($"Data Source={DbPath}");
      optionsBuilder.UseSqlite($"Filename={DbPath}");
    }
  }
}
