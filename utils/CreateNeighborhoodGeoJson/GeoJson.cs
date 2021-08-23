using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CreateNeighborhoodGeoJson
{
    public class GeoJsonObject
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("crs")]
        public CoordinateReferenceSystem CoordinateReferenceSystem { get; set; }

        [JsonProperty("features")]
        public List<Feature> Features { get; set; }
    }

    public class CoordinateReferenceSystem
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("properties")]
        public CoordinateReferenceSystemProperties Properties { get; set; }
    }

    public class CoordinateReferenceSystemProperties
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class Feature
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("properties")]
        public JObject Properties { get; set; }

        [JsonProperty("geometry")]
        public Geometry Geometry { get; set; }
    }

    public class Geometry
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("coordinates")]
        public List<List<List<double>>> Coordinates { get; set; }
    }
}
