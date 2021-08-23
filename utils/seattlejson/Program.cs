using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace seattlejson
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Expected path to King County .geojson file");
                return;
            }

            string kingCountyPath = args[0];
            string raw = File.ReadAllText(kingCountyPath);
            JObject o = JObject.Parse(raw);
            JArray seattleFeatures = new JArray();
            foreach (JObject feature in (JArray)o["features"]) {
                string name = (string)feature["properties"]["NAME"];
                if (name.StartsWith("SEA ")) {
                    seattleFeatures.Add(feature);
                }
            }
            o["features"] = seattleFeatures;
            string inputDirectory = Path.GetDirectoryName(kingCountyPath);
            string outputFile = Path.Combine(inputDirectory, "seattle_districts.geojson");
            File.WriteAllText(outputFile, o.ToString(Formatting.None));
            Console.WriteLine("Done");
        }
    }
}
