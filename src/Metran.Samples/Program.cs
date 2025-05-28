using Metran.Samples;

var tasks = Enumerable.Range(0,100) 
    .Select(_ => TestApp.RunAsync())
    .ToArray();

Task.WaitAll(tasks);
Console.WriteLine("Done");