using System.Collections.Generic;
using Newtonsoft.Json;

namespace CreateNeighborhoodGeoJson
{
    public class NeighborhoodObject
    {
        [JsonProperty("neighborhoods")]
        public List<Neighborhood> Neighborhoods { get; set; }
    }
    public class Neighborhood
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("precincts")]
        public List<string> Precincts { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }
    }
}
