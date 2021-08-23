using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CreateNeighborhoodGeoJson
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: CreateNeighborhoodGeoJson.exe <Seattle .geojson file> <neighborhoods .json file>");
                return;
            }

            string geojsonFile = args[0];
            string neighborhoodsFile = args[1];

            GeoJsonObject geoJson = JsonConvert.DeserializeObject<GeoJsonObject>(File.ReadAllText(geojsonFile))!;
            NeighborhoodObject neighborhoods = JsonConvert.DeserializeObject<NeighborhoodObject>(File.ReadAllText(neighborhoodsFile))!;

            // Tag all precincts with their respective neighborhood
            foreach (Feature feature in geoJson.Features)
            {
                string precinctName = (string)feature.Properties["NAME"]!;
                string? neighborhoodName = FindNeighborhood(precinctName, neighborhoods);
                if (neighborhoodName == null)
                {
                    continue;
                }
                feature.Properties["neighborhood"] = neighborhoodName;
            }

            // Now try to eliminate lat/lngs that are too close to each other
            // foreach (Feature feature in geoJson.Features)
            // {
            //     string precinctName = (string)feature.Properties["NAME"]!;
            //     string neighborhoodName = (string)feature.Properties["neighborhood"]!;
            //     foreach (Feature otherFeature in geoJson.Features)
            //     {
            //         // skip itself
            //         string otherPrecinctName = (string)otherFeature.Properties["NAME"]!;
            //         if (precinctName == otherPrecinctName)
            //         {
            //             continue;
            //         }

            //         string otherNeighborhoodName = (string)otherFeature.Properties["neighborhood"]!;
            //         if (neighborhoodName == otherNeighborhoodName)
            //         {
            //             List<List<double>> pointsToRemove = new List<List<double>>();
            //             foreach (List<double> point in feature.Geometry.Coordinates[0])
            //             {
            //                 List<List<double>> otherPointsToRemove = new List<List<double>>();
            //                 foreach (List<double> otherPoint in otherFeature.Geometry.Coordinates[0])
            //                 {
            //                     double distance = Haversine(point, otherPoint);
            //                     if (distance < 0.00001)
            //                     {
            //                         pointsToRemove.Add(point);
            //                         otherPointsToRemove.Add(otherPoint);
            //                     }
            //                 }

            //                 foreach (var otherPointToRemove in otherPointsToRemove)
            //                 {
            //                     otherFeature.Geometry.Coordinates[0].Remove(otherPointToRemove);
            //                 }
            //             }

            //             foreach (var pointToRemove in pointsToRemove)
            //             {
            //                 feature.Geometry.Coordinates[0].Remove(pointToRemove);
            //             }
            //         }
            //     }
            // }

            // Finally write the modified .geojson file
            string inputDirectory = Path.GetDirectoryName(geojsonFile)!;
            string fileName = Path.GetFileName(geojsonFile)!;
            string outputFile = Path.Combine(inputDirectory, $"neighborhoods_{fileName}");
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(geoJson, Formatting.None));
            Console.WriteLine("Done");
        }

        static string? FindNeighborhood(string precinctName, NeighborhoodObject neighborhoods)
        {
            foreach (Neighborhood neighborhood in neighborhoods.Neighborhoods)
            {
                foreach (string precinct in neighborhood.Precincts)
                {
                    if (precinct == precinctName)
                    {
                        return neighborhood.Name;
                    }
                }
            }
            return null;
        }

        public static double Haversine(List<double> point1, List<double> point2)
        {
            return Haversine(point1[1], point2[1], point1[0], point2[0]);
        }

        // Taken from https://stackoverflow.com/questions/41621957/a-more-efficient-haversine-function
        public static double Haversine(double lat1, double lat2, double lon1, double lon2)
        {
            const double r = 6371; // meters

            var sdlat = Math.Sin((lat2 - lat1) / 2);
            var sdlon = Math.Sin((lon2 - lon1) / 2);
            var q = sdlat * sdlat + Math.Cos(lat1) * Math.Cos(lat2) * sdlon * sdlon;
            var d = 2 * r * Math.Asin(Math.Sqrt(q));

            return d;
        }
    }
}
