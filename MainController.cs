using Mono.Cecil;
using Mono.Cecil.Cil;
using QuikGraph.Data;

namespace ProgramAnalysis;

public class MainController
{
    private static readonly string _pathToExecutable =
        @"C:\Code\BIMCO.SKPI - CSharp\Presentation\BIMCO.SKPI.BusinessDashboard.API\bin\Release\net7.0\publish\BIMCO.SKPI.BusinessDashboard.API.dll";

    private readonly EntityAnalysisHelper _entityAnalysisHelper = new();

    public void Run()
    {
        AnalyzeAssembly(_pathToExecutable);
        AnalyzeAssemblyPath(_pathToExecutable);
        var structure = GetTypesAndMethods(_pathToExecutable);
        var entities = _entityAnalysisHelper.GetModelTypesAndMembers(_pathToExecutable);
        var dataSet = _entityAnalysisHelper.ConvertToDataSet(entities);
        var graph = dataSet.ToGraph();
        var dot = graph.ToGraphviz();
        //var graph = _entityAnalysisHelper.CreateGraphFromDataSet(dataSet);
        _entityAnalysisHelper.RenderGraphToImage(dot, imagePath: "output.png");
        _entityAnalysisHelper.DisplayGraph(imagePath: "output.png");
    }


    public static void AnalyzeAssembly(string assemblyPath)
    {
        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

        // Iterate through all types in the assembly
        foreach (var type in assembly.MainModule.Types)
        {
            Console.WriteLine("Type: " + type.FullName);

            // Iterate through all methods in the type
            foreach (var method in type.Methods) Console.WriteLine("  Method: " + method.Name);
        }
    }

    private static void AnalyzeAssemblyPath(string assemblyPath)
    {
        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
        var entryPoint = assembly.EntryPoint;

        Console.WriteLine($"Entry point: {entryPoint.FullName}");
        AnalyzeMethod(entryPoint, new HashSet<string>(), assembly);
    }

    private static void AnalyzeMethod(MethodDefinition method,
        HashSet<string> visitedMethods,
        AssemblyDefinition mainAssembly)
    {
        // Avoid re-analyzing methods
        if (visitedMethods.Contains(method.FullName)) return;
        visitedMethods.Add(method.FullName);

        if (method.HasBody)
            foreach (var instruction in method.Body.Instructions)
                if (instruction.OpCode == OpCodes.Call
                    || instruction.OpCode == OpCodes.Callvirt)
                {
                    var methodCall = instruction.Operand as MethodReference;
                    if (methodCall != null)
                    {
                        Console.WriteLine($"Method {method.FullName} calls {methodCall.FullName}");
                        if (method.Module.Assembly != mainAssembly)
                        {
                            var resolvedMethod = methodCall.Resolve();
                            if (resolvedMethod != null) AnalyzeMethod(resolvedMethod, visitedMethods, mainAssembly);
                        }
                    }
                }
    }


    private static Dictionary<string, List<string>> GetTypesAndMethods(string assemblyPath)
    {
        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
        var typesAndMethods = new Dictionary<string, List<string>>();

        foreach (var type in assembly.MainModule.Types)
        {
            var methodNames = new List<string>();
            foreach (var method in type.Methods) methodNames.Add(method.Name);
            typesAndMethods[type.FullName] = methodNames;
        }

        return typesAndMethods;
    }
}