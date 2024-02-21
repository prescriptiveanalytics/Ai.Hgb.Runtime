using Ai.Hgb.Common.Entities;

namespace Ai.Hgb.Runtime.Repository {
  public static class TestManager {

    public static void Run(Manager repMan) {
      var img = new Image() {
        Hash = "XXX000XXX",
        Name = "spa-runtime-repositorytest-image",
        Tag = "latest",
        Created = DateTime.Now,
        Size = 32000 };
      var newImg = repMan.UpsertImageAsync(img).Result;
      Console.WriteLine(newImg.Name);

      var pkg = new Package() {
        Name = "Bal",
        Tag = "latest"
      };
      var newPkg = repMan.UpsertPackageAsync(pkg).Result;
      Console.WriteLine(newPkg.Id);

    }

  }
}
