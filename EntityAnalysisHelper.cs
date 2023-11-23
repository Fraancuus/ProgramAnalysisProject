using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Mono.Cecil;
using QuikGraph;
using QuikGraph.Graphviz;

namespace ProgramAnalysis;

public class EntityAnalysisHelper
{
    public Dictionary<string, List<string>> GetModelTypesAndMembers(string assemblyPath)
    {
        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
        var modelTypes = new Dictionary<string, List<string>>();

        foreach (var type in assembly.MainModule.Types)
        {
            var typeName = type.FullName;

            // Check if the type name contains "Models" but does not contain the excluded substrings
            if (typeName.Contains(value: "Models")
                && !typeName.Contains(value: "Models.Repositories.Interfaces")
                && !typeName.Contains(value: "Models.Repositories.Implementations")
                && !typeName.Contains(value: "Context"))
            {
                var memberDetails = new List<string>();
                foreach (var field in type.Fields)
                    // Including field type in the member details
                    memberDetails.Add(
                        $"{ExtractGenericType(field.FieldType.FullName)} {ExtractGenericType(field.Name)}");
                modelTypes[GetClassName(typeName)] = memberDetails;
            }
        }

        return modelTypes;
    }

    public string GetClassName(string typeName)
    {
        var lastDotIndex = typeName.LastIndexOf('.');
        if (lastDotIndex != -1
            && lastDotIndex < typeName.Length - 1) return typeName.Substring(lastDotIndex + 1);
        return typeName; // Return the original string if there's no dot
    }


    public string ExtractGenericType(string inputType)
    {
        var match = Regex.Match(inputType, pattern: @"\<([^>]*)\>");
        string result;

        if (match.Success)
        {
            result = match.Groups[1].Value;
            return result;
        }

        return inputType;
    }


    public DataSet ConvertToDataSet(Dictionary<string, List<string>> modelTypes)
    {
        var dataSet = new DataSet();

        foreach (var kvp in modelTypes)
        {
            var table = new DataTable(kvp.Key); // Use the type name as the table name

            // Add columns for each member in the list
            foreach (var member in kvp.Value)
            {
                var memberParts = member.Split(new[] { ' ' }, 2);
                var columnName = memberParts.Length == 2
                    ? $"{memberParts[1]}"
                    : member;
                var type = Type.GetType(memberParts[0]);
                if (type == null)
                {
                    type = typeof(object);
                    columnName = $"{columnName} ({GetClassName(memberParts[0])})";
                    // We extract the class name in this case since we are having trouble getting the type which means it's probably a custom type
                }

                table.Columns.Add(columnName, type);
            }


            dataSet.Tables.Add(table);
        }

        return dataSet;
    }


    public AdjacencyGraph<string, Edge<string>> CreateGraphFromDataSet(DataSet dataSet)
    {
        var graph = new AdjacencyGraph<string, Edge<string>>();

        foreach (DataTable table in dataSet.Tables)
        foreach (DataRow row in table.Rows)
        {
            var nodeId = $"{table.TableName}_{row[columnName: "ID"]}"; // Assuming each row has a unique ID
            graph.AddVertex(nodeId);

            // Example: Add edges if there are foreign key relationships
            // This is a simplified example, adjust according to your data schema
            if (table.TableName == "ParentTable"
                && row[columnName: "ChildId"] != DBNull.Value)
            {
                var childNodeId = $"ChildTable_{row[columnName: "ChildId"]}";
                graph.AddVertex(childNodeId);
                var edge = new Edge<string>(nodeId, childNodeId);
                graph.AddEdge(edge);
            }
        }

        return graph;
    }


    public AdjacencyGraph<string, Edge<string>> CreateGraph(Dictionary<string, List<string>> modelTypes)
    {
        var graph = new AdjacencyGraph<string, Edge<string>>();

        foreach (var kvp in modelTypes)
        {
            var parentVertex = kvp.Key;
            graph.AddVertex(parentVertex);

            foreach (var member in kvp.Value)
                if (member != null)
                {
                    graph.AddVertex(member);
                    var edge = new Edge<string>(parentVertex, member);
                    graph.AddEdge(edge);
                }
        }

        return graph;
    }


    public void RenderGraphToImage(string graph,
        string imagePath)
    {
        var dot = graph;

        File.WriteAllText(path: "graph.dot", dot);

        // Assuming Graphviz is installed and 'dot' command is available in system PATH
        var startInfo = new ProcessStartInfo(fileName: "dot")
                        {
                            Arguments = "-Tpng graph.dot -o " + imagePath,
                            UseShellExecute = true
                        };

        using (var process = Process.Start(startInfo))
        {
            process.WaitForExit();
        }
    }

    public void RenderGraphToImage(AdjacencyGraph<string, Edge<string>> graph,
        string imagePath)
    {
        var graphviz = new GraphvizAlgorithm<string, Edge<string>>(graph);
        var dot = graphviz.Generate();

        File.WriteAllText(path: "graph.dot", dot);

        // Assuming Graphviz is installed and 'dot' command is available in system PATH
        var startInfo = new ProcessStartInfo(fileName: "dot")
                        {
                            Arguments = "-Tpng graph.dot -o " + imagePath,
                            UseShellExecute = true
                        };

        using (var process = Process.Start(startInfo))
        {
            process.WaitForExit();
        }
    }

    public void DisplayGraph(string imagePath)
    {
        var form = new Form
                   {
                       Width = 800,
                       Height = 600
                   };

        var pictureBox = new PictureBox
                         {
                             Image = Image.FromFile(imagePath),
                             Dock = DockStyle.Fill,
                             SizeMode = PictureBoxSizeMode.StretchImage
                         };

        form.Controls.Add(pictureBox);
        Application.Run(form);
    }
}