using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

public class ProcessSupervisor
{
    public static void ListAllProcesses()
    {
        // Getting all running processes
        var processList = Process.GetProcesses();

        Console.WriteLine(value: "List of running processes:");
        foreach (var process in processList) Console.WriteLine($"Process: {process.ProcessName} ID: {process.Id}");
    }

    public static void AttachToProcess()
    {
        var targetProcessId = GetProcessId(processName: "BIMCO.SKPI.BusinessDashboard.API");
        if (targetProcessId == -1)
        {
            Console.WriteLine(value: "Process not found");
            return;
        }

        Console.WriteLine($"Process found with ID: {targetProcessId}");

        var targetProcess = Process.GetProcessById(targetProcessId);
        var modules = targetProcess.Modules;
        foreach (ProcessModule module in modules)
        {
            Console.WriteLine($"Module: {module.ModuleName} Base Address: {module.BaseAddress}");
            Console.WriteLine($"Module: {module.FileVersionInfo}");
        }
    }

    public static nint GetBaseAddress(int processId,
        string moduleName)
    {
        var targetProcess = Process.GetProcessById(processId);
        var modules = targetProcess.Modules;
        foreach (ProcessModule module in modules)
            if (module.ModuleName == moduleName)
                return module.BaseAddress;
        return 0;
    }

    public static nint GetEndAddress(int processId,
        string moduleName)
    {
        var targetProcess = Process.GetProcessById(processId);
        var modules = targetProcess.Modules;
        foreach (ProcessModule module in modules)
            if (module.ModuleName == moduleName)
                return module.BaseAddress + module.ModuleMemorySize;
        return 0;
    }


    public static int GetProcessId(string processName)
    {
        var processList = Process.GetProcessesByName(processName);
        if (processList.Length == 0) return -1;
        return processList[0].Id;
    }


    public static void listenToKernelEvents()
    {
        var targetProcessId = GetProcessId(processName: "BIMCO.SKPI.BusinessDashboard.API");
        if (targetProcessId == -1)
        {
            Console.WriteLine(value: "Process not found");
            return;
        }

        Console.WriteLine($"Process found with ID: {targetProcessId}");

        using (var session = new TraceEventSession(
                   Environment.OSVersion.Version.Build >= 9200
                       ? "MyKernelSession"
                       : KernelTraceEventParser.KernelSessionName))
        {
            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.ImageLoad);
            var parser = session.Source.Kernel;

            parser.ProcessStart += e =>
            {
                if (e.ProcessID == targetProcessId)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(
                        $"{e.TimeStamp}.{e.TimeStamp.Millisecond:D3}: Process {e.ProcessID} ({e.ProcessName
                        }) Created by {e.ParentID}: {e.CommandLine}");
                }
            };
            parser.ProcessStop += e =>
            {
                if (e.ProcessID == targetProcessId)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"{e.TimeStamp}.{e.TimeStamp.Millisecond:D3}: Process {e.ProcessID} ({e.ProcessName}) Exited");
                }
            };

            parser.ImageLoad += e =>
            {
                if (e.ProcessID == targetProcessId)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    var name = TryGetProcessName(e);
                    Console.WriteLine(
                        $"{e.TimeStamp}.{e.TimeStamp.Millisecond:D3}: Image Loaded: {e.FileName} into process {
                            e.ProcessID} ({name}) Size=0x{e.ImageSize:X}");
                }
            };

            parser.ImageUnload += e =>
            {
                if (e.ProcessID == targetProcessId)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    var name = TryGetProcessName(e);
                    Console.WriteLine(
                        $"{e.TimeStamp}.{e.TimeStamp.Millisecond:D3}: Image Unloaded: {e.FileName} from process {
                            e.ProcessID
                        } ({name})");
                }
            };


            Task.Run(() => session.Source.Process());
            Thread.Sleep(TimeSpan.FromSeconds(60));
        }

        string TryGetProcessName(TraceEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.ProcessName))
                return evt.ProcessName;
            return "Unknown";
        }
    }


    public static void listenToProcess()
    {
        var targetProcessId = GetProcessId(processName: "BIMCO.SKPI.BusinessDashboard.API");

        var baseAddress = GetBaseAddress(targetProcessId, moduleName: "BIMCO.SKPI.BusinessDashboard.API");
        var endAddress = GetEndAddress(targetProcessId, moduleName: "BIMCO.SKPI.BusinessDashboard.API");

        if (targetProcessId == -1)
        {
            Console.WriteLine(value: "Process not found");
            return;
        }

        Console.WriteLine($"Process found with ID: {targetProcessId}");
        using (var session = new TraceEventSession(sessionName: "MySessionName"))
        {
            // Enable CLR provider with more detailed (Verbose) level
            session.EnableProvider(
                ClrTraceEventParser.ProviderName, TraceEventLevel.Verbose,
                unchecked((ulong)ClrTraceEventParser.Keywords.All));

            // Subscribe to various CLR events
            // these are left in code for future tinkering and reference

            //session.Source.Clr.AssemblyLoaderStart += data =>
            //{
            //    if (data.ProcessID == targetProcessId)
            //        Console.WriteLine($"Assembly Loader Started: {data.AssemblyName} {data}");
            //};

            //session.Source.Clr.ThreadCreating += data =>
            //{
            //    if (data.ProcessID == targetProcessId)
            //        Console.WriteLine($"Thread Creating: {data}");
            //};

            //session.Source.Clr.MethodLoadVerbose += data =>
            //{
            //    if (data.ProcessID == targetProcessId)
            //    {
            //        var methodTokenHex = data.MethodToken.ToString(format: "X");
            //        Console.WriteLine(
            //            $"JIT Started for Method: {data.MethodID}, Token: {methodTokenHex}, method signature: {
            //                data.MethodSignature}, method namespace: {data.MethodNamespace}");
            //        Console.WriteLine(value: "-------------------------------------------------------");
            //        Console.WriteLine(data);


            //        if (methodTokenHex.EndsWith(value: "60001EE"))
            //            Console.WriteLine($"Method Loaded: {data.MethodName}, Token: {methodTokenHex}");
            //    }
            //};


            //session.Source.Clr.MethodUnloadVerbose += data =>
            //{
            //    if (data.ProcessID == targetProcessId)
            //        Console.WriteLine($"Verbose Method Unloaded: {data.MethodName}, ID: {data.MethodID}");
            //};

            //session.Source.Clr.MethodJittingStarted += data =>
            //{
            //    if (data.ProcessID == targetProcessId)
            //        Console.WriteLine($"JIT Started for Method: {data.MethodID}, Token: {data.MethodToken:X}");
            //};


            session.Source.Clr.MethodJittingStarted += data =>
            {
                if (data.ProcessID == targetProcessId)
                    if (data.MethodNamespace.Contains(value: "BIMCO"))
                    {
                        Console.WriteLine(value: "----------------Event-----------------------------");
                        Console.WriteLine($"Provider name: {data.ProviderName}, data: {data}");
                    }
            };

            //session.Source.Clr.MethodLoadVerbose += data =>
            //{
            //    if (data.ProcessID == targetProcessId)
            //        if (data.MethodStartAddress >= (ulong)baseAddress.ToInt64()
            //            && (ulong)endAddress >= data.MethodStartAddress)
            //            Console.WriteLine($"Method Loaded: {data.MethodName}, Module: {data}");
            //};

            try
            {
                // Process events
                session.Source.Process();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}