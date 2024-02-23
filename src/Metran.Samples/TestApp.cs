namespace Metran.Samples;

public static class TestApp
{
  public static void Run() {
    var guid = Guid.NewGuid();
    var idList = new List<long>() {
      new Random().Next(1, 10),
      new Random().Next(1, 10),
      new Random().Next(1, 10),
      new Random().Next(1, 10)
    };
    Thread.Sleep( new Random().Next(100, 500)); 
    using var metran = TestMetranContainer.Container.BeginTransaction(idList.ToHashSet());

    if (metran == null) {
      // Console.WriteLine($"[{guid}]Transaction already exists");
      return;
    }
    if(new Random().Next(1000, 5000) % 2 == 0) {
      // Console.WriteLine($"[{guid}] random error");
      return;
    }
    /*
     GOAL IS TO MAKE SURE THAT ONLY ONE TRANSACTION IS EXECUTED AT A TIME
     FOR EXAMPLE IF WE HAVE 100 THREADS RUNNING THIS CODE, ONLY ONE THREAD SHOULD BE ABLE TO EXECUTE THE CODE BELOW 
     */
    Console.WriteLine($"[{guid}]Transaction started ids: {string.Join(",", idList)}");
    Thread.Sleep( new Random().Next(100, 1000)); 
    Console.WriteLine($"[{guid}]Transaction ended ids: {string.Join(",", idList)}");
  }
  
}