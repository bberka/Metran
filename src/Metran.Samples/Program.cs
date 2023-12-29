// See https://aka.ms/new-console-template for more information

using Metran.Samples;

var action = new Action(TestApp.Run);
var tasks = new List<Task>();
for (var i = 0; i < 100; i++) {
  tasks.Add(Task.Run(action));
}
 
Task.WaitAll(tasks.ToArray());
Console.WriteLine("Done");