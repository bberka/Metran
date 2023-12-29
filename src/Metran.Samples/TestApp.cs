namespace Metran.Samples;

public static class TestApp
{
  public static void Run() {
    var guid = Guid.NewGuid();
    var userId = 3L;
    Thread.Sleep( new Random().Next(100, 500)); 
    using var metran = GlobalMetranContainer.BeginTransaction(userId);

    if (metran == null) {
      // Console.WriteLine($"[{guid}]Transaction already exists");
      return;
    }
    if(new Random().Next(1000, 5000) % 2 == 0) {
      // Console.WriteLine($"[{guid}] random error");
      metran.EndTransaction();
      return;
    }
    /*
     GOAL IS TO MAKE SURE THAT ONLY ONE TRANSACTION IS EXECUTED AT A TIME
     FOR EXAMPLE IF WE HAVE 100 THREADS RUNNING THIS CODE, ONLY ONE THREAD SHOULD BE ABLE TO EXECUTE THE CODE BELOW 
     */
    Console.WriteLine($"[{guid}]Transaction started");
    Thread.Sleep( new Random().Next(100, 1000)); 
    Console.WriteLine($"[{guid}]Transaction ended");
  }
  
}