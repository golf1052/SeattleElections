using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace EstimateNeighborhoodLean
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: EstimateNeighborhoodLean <primary neighborhood election results json file>");
                return;
            }

            List<InputNeighborhood> inputNeighborhoods = JsonConvert.DeserializeObject<List<InputNeighborhood>>(File.ReadAllText(args[0]));
            List<OutputNeighborhood> estimatedNeighborhoodLean = new List<OutputNeighborhood>();

            List<string> mayorTopTwoCandidates = DetermineTopTwoCandidates(inputNeighborhoods, "City of Seattle Mayor");

            foreach (InputNeighborhood neighborhood in inputNeighborhoods)
            {
                OutputNeighborhood estimatedNeighborhood = new OutputNeighborhood()
                {
                    Name = neighborhood.Name,
                    Races = new List<OutputRace>()
                };
                foreach (InputRace race in neighborhood.Races)
                {
                    if (race.Name == "City of Seattle Mayor")
                    {
                        OutputRace estimatedRace = new OutputRace()
                        {
                            Name = race.Name
                        };
                        int totalVotes = 0;
                        int candidate1Votes = 0;
                        int candidate2Votes = 0;

                        foreach (VoteItem item in race.Votes)
                        {
                            if (item.Item == mayorTopTwoCandidates[0])
                            {
                                totalVotes += item.Votes;
                                candidate1Votes = item.Votes;
                            }
                            else if (item.Item == mayorTopTwoCandidates[1])
                            {
                                totalVotes += item.Votes;
                                candidate2Votes = item.Votes;
                            }
                        }

                        if (candidate1Votes >= candidate2Votes)
                        {
                            estimatedRace.Candidate = mayorTopTwoCandidates[0];
                            estimatedRace.Lean = candidate1Votes / (double)totalVotes;
                        }
                        else
                        {
                            estimatedRace.Candidate = mayorTopTwoCandidates[1];
                            estimatedRace.Lean = candidate2Votes / (double)totalVotes;
                        }
                        estimatedNeighborhood.Races.Add(estimatedRace);
                    }
                    // else if (race.Name == "City of Seattle Council Position No. 8")
                    // {
                    //     OutputRace estimatedRace = new OutputRace()
                    //     {
                    //         Name = race.Name
                    //     };
                    //     int totalVotes = 0;
                    //     int mosquedaVotes = 0;
                    //     int wilsonVotes = 0;

                    //     foreach (VoteItem item in race.Votes)
                    //     {
                    //         if (item.Item == "Teresa Mosqueda")
                    //         {
                    //             totalVotes += item.Votes;
                    //             mosquedaVotes = item.Votes;
                    //         }
                    //         else if (item.Item == "Kenneth Wilson")
                    //         {
                    //             totalVotes += item.Votes;
                    //             wilsonVotes = item.Votes;
                    //         }
                    //     }

                    //     if (mosquedaVotes >= wilsonVotes)
                    //     {
                    //         estimatedRace.Candidate = "Teresa Mosqueda";
                    //         estimatedRace.Lean = mosquedaVotes / (double)totalVotes;
                    //     }
                    //     else
                    //     {
                    //         estimatedRace.Candidate = "Kenneth Wilson";
                    //         estimatedRace.Lean = wilsonVotes / (double)totalVotes;
                    //     }
                    //     estimatedNeighborhood.Races.Add(estimatedRace);
                    // }
                    // else if (race.Name == "City of Seattle Council Position No. 9")
                    // {
                    //     OutputRace estimatedRace = new OutputRace()
                    //     {
                    //         Name = race.Name
                    //     };
                    //     int totalVotes = 0;
                    //     int oliverVotes = 0;
                    //     int nelsonVotes = 0;

                    //     foreach (VoteItem item in race.Votes)
                    //     {
                    //         if (item.Item == "Nikkita Oliver")
                    //         {
                    //             totalVotes += item.Votes;
                    //             oliverVotes = item.Votes;
                    //         }
                    //         else if (item.Item == "Sara Nelson")
                    //         {
                    //             totalVotes += item.Votes;
                    //             nelsonVotes = item.Votes;
                    //         }
                    //     }

                    //     if (oliverVotes >= nelsonVotes)
                    //     {
                    //         estimatedRace.Candidate = "Nikkita Oliver";
                    //         estimatedRace.Lean = oliverVotes / (double)totalVotes;
                    //     }
                    //     else
                    //     {
                    //         estimatedRace.Candidate = "Sara Nelson";
                    //         estimatedRace.Lean = nelsonVotes / (double)totalVotes;
                    //     }
                    //     estimatedNeighborhood.Races.Add(estimatedRace);
                    // }
                    // else if (race.Name == "City of Seattle City Attorney")
                    // {
                    //     OutputRace estimatedRace = new OutputRace()
                    //     {
                    //         Name = race.Name
                    //     };
                    //     int totalVotes = 0;
                    //     int kennedyVotes = 0;
                    //     int davisonVotes = 0;

                    //     foreach (VoteItem item in race.Votes)
                    //     {
                    //         if (item.Item == "Nicole Thomas-Kennedy")
                    //         {
                    //             totalVotes += item.Votes;
                    //             kennedyVotes = item.Votes;
                    //         }
                    //         else if (item.Item == "Ann Davison")
                    //         {
                    //             totalVotes += item.Votes;
                    //             davisonVotes = item.Votes;
                    //         }
                    //     }

                    //     if (kennedyVotes >= davisonVotes)
                    //     {
                    //         estimatedRace.Candidate = "Nicole Thomas-Kennedy";
                    //         estimatedRace.Lean = kennedyVotes / (double)totalVotes;
                    //     }
                    //     else
                    //     {
                    //         estimatedRace.Candidate = "Ann Davison";
                    //         estimatedRace.Lean = davisonVotes / (double)totalVotes;
                    //     }
                    //     estimatedNeighborhood.Races.Add(estimatedRace);
                    // }
                }
                estimatedNeighborhoodLean.Add(estimatedNeighborhood);
            }

            string estimatesDirectoryPath = Path.Combine(Path.GetDirectoryName(args[0]), "../Estimates");
            string estimatedNeighborhoodLeanPath = Path.Combine(estimatesDirectoryPath, $"neighborhood-estimated-lean.json");
            File.WriteAllText(estimatedNeighborhoodLeanPath, JsonConvert.SerializeObject(estimatedNeighborhoodLean, Formatting.None));
            Console.WriteLine("Done");
        }

        static List<string> DetermineTopTwoCandidates(List<InputNeighborhood> neighborhoods, string raceName)
        {
            List<string> topTwoCandidates = new List<string>();
            Dictionary<string, int> candidateVotes = new Dictionary<string, int>();
            foreach (InputNeighborhood neighborhood in neighborhoods)
            {
                foreach (InputRace race in neighborhood.Races)
                {
                    if (race.Name == raceName)
                    {
                        foreach (VoteItem votes in race.Votes)
                        {
                            if (!candidateVotes.ContainsKey(votes.Item))
                            {
                                candidateVotes.Add(votes.Item, 0);
                            }
                            candidateVotes[votes.Item] += votes.Votes;
                        }
                    }
                }
            }

            var sortedCandidates = candidateVotes.OrderByDescending(x => x.Value);
            topTwoCandidates.Add(sortedCandidates.First().Key);
            topTwoCandidates.Add(sortedCandidates.ElementAt(1).Key);
            return topTwoCandidates;
        }
    }

    class InputNeighborhood
    {
        [JsonProperty("neighborhood")]
        public string Name { get; set; }

        [JsonProperty("races")]
        public List<InputRace> Races { get; set; }
    }

    class InputRace
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

        [JsonProperty("races")]
        public List<OutputRace> Races { get; set; }
    }

    class OutputRace
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("candidate")]
        public string Candidate { get; set; }

        [JsonProperty("lean")]
        public double Lean { get; set; }
    }
}
