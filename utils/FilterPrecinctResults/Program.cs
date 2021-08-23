using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FilterPrecinctResults
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Expected path to King County precinct results .csv file");
                return;
            }

            string resultsPath = args[0];
            List<string> lines = File.ReadAllLines(resultsPath).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                // skip header
                if (i == 0)
                {
                    continue;
                }
                string line = lines[i];

                if (!line.Contains("SEA "))
                {
                    lines.RemoveAt(i);
                    i--;
                }
            }
            string inputDirectory = Path.GetDirectoryName(resultsPath);
            string fileName = Path.GetFileName(resultsPath);
            string outputFile = Path.Combine(inputDirectory, $"seattle-{fileName}");
            File.WriteAllLines(outputFile, lines);
            Console.WriteLine("Done");
        }
    }
}
