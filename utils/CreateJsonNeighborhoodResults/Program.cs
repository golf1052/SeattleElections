using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CreateJsonNeighborhoodResults
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: CreateJsonNeighborhoodResults.exe <neighborhoods.json> <results.json>");
                return;
            }

            string neighborhoodsPath = args[0];
            string resultsPath = args[1];

            InputNeighborhoodsObject neighborhoods = JsonConvert.DeserializeObject<InputNeighborhoodsObject>(File.ReadAllText(neighborhoodsPath));
            List<Precinct> inputResults = JsonConvert.DeserializeObject<List<Precinct>>(File.ReadAllText(resultsPath));

            List<OutputNeighborhood> outputResults = new List<OutputNeighborhood>();
            foreach (var neighborhood in neighborhoods.InputNeighborhoods)
            {
                OutputNeighborhood outputNeighborhood = new OutputNeighborhood();
                outputNeighborhood.Name = neighborhood.Name;
                outputNeighborhood.Races = new List<Race>();
                foreach (var precinct in neighborhood.Precincts)
                {
                    var foundPrecinct = GetPrecinct(precinct, inputResults);
                    if (foundPrecinct == null)
                    {
                        continue;
                    }

                    foreach (var race in foundPrecinct.Races)
                    {
                        var foundRace = GetRace(race.Name, outputNeighborhood.Races);
                        if (foundRace == null)
                        {
                            foundRace = new Race()
                            {
                                Name = race.Name,
                                RegisteredVoters = 0,
                                TotalVotes = 0,
                                Votes = new List<VoteItem>()
                            };
                            outputNeighborhood.Races.Add(foundRace);
                        }

                        foundRace.RegisteredVoters += race.RegisteredVoters;

                        foreach (var voteItem in race.Votes)
                        {
                            var foundVoteItem = GetVoteItem(voteItem.Item, foundRace.Votes);
                            if (foundVoteItem == null)
                            {
                                foundVoteItem = new VoteItem()
                                {
                                    Item = voteItem.Item,
                                    Votes = 0
                                };
                                foundRace.Votes.Add(foundVoteItem);
                            }

                            foundVoteItem.Votes += voteItem.Votes;
                            // Do not use race.TotalVotes because the total votes reported by the county does not always
                            // match the total votes cast in this race, instead add all the votes in each VoteItem to
                            // determine the total votes cast in this race.
                            foundRace.TotalVotes += voteItem.Votes;
                        }
                    }
                }
                outputResults.Add(outputNeighborhood);
            }

            string inputDirectory = Path.GetDirectoryName(resultsPath);
            string fileName = Path.GetFileName(resultsPath);
            string outputFile = Path.Combine(inputDirectory, $"neighborhoods-{fileName}");
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(outputResults, Formatting.None));
            Console.WriteLine("Done");
        }

        static Precinct GetPrecinct(string name, List<Precinct> precincts)
        {
            foreach (var precinct in precincts)
            {
                if (precinct.Name == name)
                {
                    return precinct;
                }
            }
            return null;
        }

        static Race GetRace(string name, List<Race> races)
        {
            foreach (var race in races)
            {
                if (race.Name == name)
                {
                    return race;
                }
            }
            return null;
        }

        static VoteItem GetVoteItem(string item, List<VoteItem> votes)
        {
            foreach (var vote in votes)
            {
                if (vote.Item == item)
                {
                    return vote;
                }
            }
            return null;
        }
    }

    class Precinct
    {
        [JsonProperty("precinct")]
        public string Name { get; set; }

        [JsonProperty("district_number")]
        public int DistrictNumber { get; set; }

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

    class InputNeighborhoodsObject
    {
        [JsonProperty("neighborhoods")]
        public List<InputNeighborhood> InputNeighborhoods { get; set; }
    }

    class InputNeighborhood
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("precincts")]
        public List<string> Precincts { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }
    }

    class OutputNeighborhood
    {
        [JsonProperty("neighborhood")]
        public string Name { get; set; }

        [JsonProperty("races")]
        public List<Race> Races { get; set; }
    }
}
