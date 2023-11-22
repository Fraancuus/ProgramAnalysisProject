// See https://aka.ms/new-console-template for more information

using ProgramAnalysis;

Console.WriteLine(value: "Hello, World!");
var controller = new MainController();
// call the run method
controller.Run();


while (true)
{
    Console.WriteLine(value: "Type 'exit' to close the program.");
    var input = Console.ReadLine();
    if (input.ToLower() == "exit") break;
}