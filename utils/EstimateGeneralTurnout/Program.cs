using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace EstimateGeneralTurnout
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: EstimateGeneralTurnout <primary neighborhood election results json file>");
                return;
            }

            char splitChar;
            if (args[0].Contains('\\'))
            {
                splitChar = '\\';
            }
            else
            {
                splitChar = '/';
            }
            string[] splitPath = args[0].Split(splitChar);
            int year = 0;
            foreach (string item in splitPath)
            {
                if (int.TryParse(item, out int foundYear))
                {
                    year = foundYear;
                    break;
                }
            }

            if (year == 0)
            {
                Console.WriteLine($"Could not find year in path {args[0]}");
                return;
            }

            // List<InputNeighborhood> currentResults = JsonConvert.DeserializeObject<List<InputNeighborhood>>(File.ReadAllText(args[0]));

            // Get primary and general election results from 2017
            string _2017RootPath = Path.Combine(string.Join("\\", splitPath[0..^4]), "2017");

            string _2017PrimaryResultsPath = Path.Combine(_2017RootPath, "Primary/Final/neighborhoods-converted-seattle-August_2017_primary_ecanvass.json");
            List<InputNeighborhood> _2017PrimaryResults = JsonConvert.DeserializeObject<List<InputNeighborhood>>(File.ReadAllText(_2017PrimaryResultsPath));

            string _2017GeneralResultsPath = Path.Combine(_2017RootPath, "General/Final/neighborhoods-converted-seattle-November_2017_general_ecanvass.json");
            List<InputNeighborhood> _2017GeneralResults = JsonConvert.DeserializeObject<List<InputNeighborhood>>(File.ReadAllText(_2017GeneralResultsPath));

            List<string> _2017ExpectedRaces = new List<string>() { "City of Seattle Mayor" };
            var _2017TurnoutGrowth = GetGeneralTurnoutGrowth(_2017GeneralResults,
                GetPrimaryNeighborhoodTurnout(_2017PrimaryResults, _2017ExpectedRaces), _2017ExpectedRaces);
            var _2017RegisteredVotersChange = GetGeneralRegisteredVotersChange(_2017GeneralResults,
                GetPrimaryNeighborhoodRegisteredVoters(_2017PrimaryResults, _2017ExpectedRaces), _2017ExpectedRaces);

            // Get primary and general election results from 2019
            string _2019RootPath = Path.Combine(string.Join("\\", splitPath[0..^4]), "2019");

            string _2019PrimaryResultsPath = Path.Combine(_2019RootPath, "Primary/Final/neighborhoods-converted-seattle-final-precinct-results-2019-08.json");
            List<InputNeighborhood> _2019PrimaryResults = JsonConvert.DeserializeObject<List<InputNeighborhood>>(File.ReadAllText(_2019PrimaryResultsPath));

            string _2019GeneralResultsPath = Path.Combine(_2019RootPath, "General/Final/neighborhoods-converted-seattle-final-precinct-results-2019-11.json");
            List<InputNeighborhood> _2019GeneralResults = JsonConvert.DeserializeObject<List<InputNeighborhood>>(File.ReadAllText(_2019GeneralResultsPath));

            List<string> _2019ExpectedRaces = new List<string>()
            {
                "City of Seattle Council District No. 1 Council District No. 1",
                "City of Seattle Council District No. 2 Council District No. 2",
                "City of Seattle Council District No. 3 Council District No. 3",
                "City of Seattle Council District No. 4 Council District No. 4",
                "City of Seattle Council District No. 5 Council District No. 5",
                "City of Seattle Council District No. 6 Council District No. 6",
                "City of Seattle Council District No. 7 Council District No. 7"
            };
            var _2019TurnoutGrowth = GetGeneralTurnoutGrowth(_2019GeneralResults,
                GetPrimaryNeighborhoodTurnout(_2019PrimaryResults, _2019ExpectedRaces), _2019ExpectedRaces);
            var _2019RegisteredVotersChange = GetGeneralRegisteredVotersChange(_2019GeneralResults,
                GetPrimaryNeighborhoodRegisteredVoters(_2019PrimaryResults, _2019ExpectedRaces), _2019ExpectedRaces);

            // var list = _2017TurnoutGrowth.ToList();
            // list.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));

            List<OutputNeighborhood> outputTurnout = new List<OutputNeighborhood>();
            foreach (var pair in _2017TurnoutGrowth)
            {
                string neighborhoodName = pair.Key;
                double _2017TurnoutValue = pair.Value;
                double _2019TurnoutValue = _2019TurnoutGrowth[neighborhoodName];
                double _2017RegisteredValue = _2017RegisteredVotersChange[neighborhoodName];
                double _2019RegisteredValue = _2019RegisteredVotersChange[neighborhoodName];
                OutputNeighborhood neighborhood = new OutputNeighborhood()
                {
                    Name = neighborhoodName
                };

                if (_2017TurnoutValue < _2019TurnoutValue)
                {
                    neighborhood.TurnoutGrowthMin = _2017TurnoutValue;
                    neighborhood.TurnoutGrowthMax = _2019TurnoutValue;
                }
                else
                {
                    neighborhood.TurnoutGrowthMin = _2019TurnoutValue;
                    neighborhood.TurnoutGrowthMax = _2017TurnoutValue;
                }

                if (_2017RegisteredValue < _2019RegisteredValue)
                {
                    neighborhood.RegisteredVotersChangeMin = _2017RegisteredValue;
                    neighborhood.RegisteredVotersChangeMax = _2019RegisteredValue;
                }
                else
                {
                    neighborhood.RegisteredVotersChangeMin = _2019RegisteredValue;
                    neighborhood.RegisteredVotersChangeMax = _2017RegisteredValue;
                }
                outputTurnout.Add(neighborhood);
            }

            string estimatesDirectoryPath = Path.Combine(string.Join("\\", splitPath[0..^2]), "Estimates");
            string estimatedGeneralTurnoutGrowthPath = Path.Combine(estimatesDirectoryPath, $"neighborhood-estimated-turnout-growth-{year}.json");
            File.WriteAllText(estimatedGeneralTurnoutGrowthPath, JsonConvert.SerializeObject(outputTurnout, Formatting.None));
            Console.WriteLine("Done");
        }

        static Dictionary<string, double> GetPrimaryNeighborhoodTurnout(List<InputNeighborhood> primaryResults, List<string> expectedRaces)
        {
            Dictionary<string, double> primaryNeighborhoodTurnout = new Dictionary<string, double>();
            foreach (InputNeighborhood neighborhood in primaryResults)
            {
                foreach (Race race in neighborhood.Races)
                {
                    if (expectedRaces.Contains(race.Name))
                    {
                        double turnout;
                        if (race.RegisteredVoters == 0)
                        {
                            turnout = 0;
                        }
                        else
                        {
                            turnout = (double)race.TotalVotes / (double)race.RegisteredVoters;
                        }

                        if (!primaryNeighborhoodTurnout.ContainsKey(neighborhood.Name))
                        {
                            primaryNeighborhoodTurnout.Add(neighborhood.Name, turnout);
                        }
                        else
                        {
                            // If the neighborhood is in multiple districts, average the turnout growth. No neighborhood
                            // is in more than 2 districts.
                            primaryNeighborhoodTurnout[neighborhood.Name] = (primaryNeighborhoodTurnout[neighborhood.Name] + turnout) / 2;
                        }
                    }
                }
            }
            return primaryNeighborhoodTurnout;
        }

        static Dictionary<string, int> GetPrimaryNeighborhoodRegisteredVoters(List<InputNeighborhood> primaryResults, List<string> expectedRaces)
        {
            Dictionary<string, int> primaryNeighborhoodRegisteredVoters = new Dictionary<string, int>();
            foreach (InputNeighborhood neighborhood in primaryResults)
            {
                foreach (Race race in neighborhood.Races)
                {
                    if (expectedRaces.Contains(race.Name))
                    {
                        if (!primaryNeighborhoodRegisteredVoters.ContainsKey(neighborhood.Name))
                        {
                            primaryNeighborhoodRegisteredVoters.Add(neighborhood.Name, race.RegisteredVoters);
                        }
                        else
                        {
                            // If the neighborhood is in multiple districts, average the number of registered voters.
                            // No neighborhood is in more than 2 districts.
                            primaryNeighborhoodRegisteredVoters[neighborhood.Name] = (primaryNeighborhoodRegisteredVoters[neighborhood.Name] + race.RegisteredVoters) / 2;
                        }
                    }
                }
            }
            return primaryNeighborhoodRegisteredVoters;
        }

        static Dictionary<string, double> GetGeneralTurnoutGrowth(List<InputNeighborhood> generalResults,
            Dictionary<string, double> primaryNeighborhoodTurnout, List<string> expectedRaces)
        {
            Dictionary<string, double> turnoutGrowth = new Dictionary<string, double>();
            foreach (InputNeighborhood neighborhood in generalResults)
            {
                foreach (Race race in neighborhood.Races)
                {
                    if (expectedRaces.Contains(race.Name))
                    {
                        double turnout;
                        if (race.RegisteredVoters == 0)
                        {
                            turnout = 0;
                        }
                        else
                        {
                            turnout = (double)race.TotalVotes / (double)race.RegisteredVoters;
                        }
                        
                        double turnoutChange = turnout - primaryNeighborhoodTurnout[neighborhood.Name];
                        if (!turnoutGrowth.ContainsKey(neighborhood.Name))
                        {
                            turnoutGrowth.Add(neighborhood.Name, turnoutChange);
                        }
                        else
                        {
                            // If the neighborhood is in multiple districts, average the turnout growth. No neighborhood
                            // is in more than 2 districts.
                            turnoutGrowth[neighborhood.Name] = (turnoutGrowth[neighborhood.Name] + turnoutChange) / 2;
                        }
                    }
                }
            }
            return turnoutGrowth;
        }

        static Dictionary<string, double> GetGeneralRegisteredVotersChange(List<InputNeighborhood> generalResults,
            Dictionary<string, int> primaryNeighborhoodRegisteredVoters, List<string> expectedRaces)
        {
            Dictionary<string, double> registeredVotersChange = new Dictionary<string, double>();
            foreach (InputNeighborhood neighborhood in generalResults)
            {
                foreach (Race race in neighborhood.Races)
                {
                    if (expectedRaces.Contains(race.Name))
                    {
                        double registeredVotersChangePercentage = (double)(race.RegisteredVoters - primaryNeighborhoodRegisteredVoters[neighborhood.Name]) /
                            primaryNeighborhoodRegisteredVoters[neighborhood.Name];
                        if (primaryNeighborhoodRegisteredVoters[neighborhood.Name] == 0)
                        {
                            registeredVotersChangePercentage = 0;
                        }
                        if (!registeredVotersChange.ContainsKey(neighborhood.Name))
                        {
                            registeredVotersChange.Add(neighborhood.Name, registeredVotersChangePercentage);
                        }
                        else
                        {
                            // If the neighborhood is in multiple districts, average the number of registered voters.
                            // No neighborhood is in more than 2 districts.
                            registeredVotersChange[neighborhood.Name] = (registeredVotersChange[neighborhood.Name] + registeredVotersChangePercentage) / 2;
                        }
                    }
                }
            }
            return registeredVotersChange;
        }
    }

    class InputNeighborhood
    {
        [JsonProperty("neighborhood")]
        public string Name { get; set; }

        [JsonProperty("races")]
        public List<Race> Races { get; set; }
    }

    class Race
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("registered_voters")]
        public int RegisteredVoters { get; set; }

        [JsonProperty("total_votes")]
        public int TotalVotes { get; set; }

        [JsonProperty("votes")]
        public List<VoteItem> Votes { get; set; }
    }

    class VoteItem
    {
        [JsonProperty("item")]
        public string Item { get; set; }

        [JsonProperty("votes")]
        public int Votes { get; set; }
    }

    class OutputNeighborhood
    {
        [JsonProperty("neighborhood")]
        public string Name { get; set; }

        [JsonProperty("turnout_growth_min")]
        public double TurnoutGrowthMin { get; set; }

        [JsonProperty("turnout_growth_max")]
        public double TurnoutGrowthMax { get; set; }

        [JsonProperty("registered_voters_change_min")]
        public double RegisteredVotersChangeMin { get; set; }

        [JsonProperty("registered_voters_change_max")]
        public double RegisteredVotersChangeMax { get; set; }
    }
}
