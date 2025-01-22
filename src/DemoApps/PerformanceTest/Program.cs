using System.Diagnostics;

namespace ai.hgb.application.demoapps.PerformanceTest {
  internal class Program {
    static void Main(string[] args) {
      Console.WriteLine("Performance Test");

      Stopwatch swatch = new Stopwatch();
      swatch.Start();
      Fib(100000000000); // 100 Mill ~27.5sec
      swatch.Stop();
      Console.WriteLine($"Time elapsed: {swatch.ElapsedMilliseconds} ms\n\n");
    }

    static long Fib(long x) {
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
  }
}
