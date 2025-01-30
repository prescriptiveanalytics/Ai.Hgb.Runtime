using System.Diagnostics;

namespace ai.hgb.application.demoapps.PerformanceTest {
  internal class Program {
    static async Task Main(string[] args) {
      Console.WriteLine("Performance Test");

      Stopwatch swatch = new Stopwatch();
      swatch.Start();
      var computeTask = Task.Factory.StartNew(() => Fib(10000000000));
      //var computeTask = Fib(10000000000); // 100 Mill ~27.5sec

      while(!computeTask.IsCompleted) {
        var stats = await GetCpuUsageForProcess();
        Console.WriteLine($"CPU:\t{stats}");
      }
      var result = await computeTask;
      swatch.Stop();
      Console.WriteLine($"Time elapsed: {swatch.ElapsedMilliseconds} ms\n\n");
    }

    private static long Fib(long x) {
      if (x == 0) return 0;

      long prev = 0;
      long next = 1;
      for (long i = 1; i < x; i++) {
        long sum = prev + next;
        prev = next;
        next = sum;
      }
      return next;      
    }

    private static async Task<double> GetCpuUsageForProcess() {
      var startTime = DateTime.UtcNow;
      var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
      await Task.Delay(500);

      var endTime = DateTime.UtcNow;
      var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
      var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
      var totalMsPassed = (endTime - startTime).TotalMilliseconds;
      var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
      return cpuUsageTotal * 100;
    }
  }
}
