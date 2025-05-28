namespace Metran.Samples;

public static class TestApp
{
  public static async Task RunAsync() {
    var guid = Guid.NewGuid();
    var idList = new List<long>() {
      new Random().Next(1, 10),
      new Random().Next(1, 10),
      new Random().Next(1, 10),
      new Random().Next(1, 10)
    };
    Thread.Sleep( new Random().Next(100, 500)); 
    var exists = TestMetranContainer.Container.TryAddTransactionList(idList.ToHashSet(), out var transactionList);

    if (exists) {
      var waitResult = await transactionList.SafeWaitAllAsync();
      if (!waitResult) {
        Console.WriteLine($"[{guid}] wait failed");
        return;
      }
    }
  
    if(new Random().Next(1000, 5000) % 2 == 0) {
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