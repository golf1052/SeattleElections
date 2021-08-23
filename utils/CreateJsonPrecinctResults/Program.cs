using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CreateJsonPrecinctResults
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Expected path to Seattle election precinct results .csv file");
                return;
            }

            string seattleResultsPath = args[0];
            string[] lines = File.ReadAllLines(seattleResultsPath);
            Dictionary<string, Precinct> allPrecincts = new Dictionary<string, Precinct>();
            bool sawHeader = false;
            foreach (string line in lines)
            {
                if (!sawHeader)
                {
                    sawHeader = true;
                    continue;
                }

                List<string> splitLine = SplitLine(line);
                ResultLine resultLine = new ResultLine()
                {
                    Precinct = splitLine[0],
                    Race = splitLine[1],
                    CounterType = splitLine[7],
                    SumOfCount = int.Parse(splitLine[8])
                };

                if (!allPrecincts.ContainsKey(resultLine.Precinct))
                {
                    allPrecincts.Add(resultLine.Precinct, new Precinct()
                    {
                        Name = resultLine.Precinct,
                        DistrictNumber = -1,
                        Races = new List<Race>()
                    });
                }

                Precinct precinct = allPrecincts[resultLine.Precinct];
                Race race;
                if (!precinct.Races.Any(r => r.Name == resultLine.Race))
                {
                    precinct.Races.Add(race = new Race()
                    {
                        Name = resultLine.Race,
                        Votes = new List<VoteItem>()
                    });
                }
                else
                {
                    race = precinct.Races.Find(r => r.Name == resultLine.Race);
                }

                if (resultLine.CounterType == "Registered Voters")
                {
                    race.RegisteredVoters = resultLine.SumOfCount;
                }
                else if (resultLine.CounterType == "Times Counted")
                {
                    race.TotalVotes = resultLine.SumOfCount;
                }
                else
                {
                    if (resultLine.CounterType != "Times Under Voted" && resultLine.CounterType != "Times Over Voted")
                    {
                        race.Votes.Add(new VoteItem()
                        {
                            Item = resultLine.CounterType,
                            Votes = resultLine.SumOfCount
                        });
                    }
                }
            }

            List<Precinct> allPrecinctsList = allPrecincts.Values.ToList();
            string json = JsonConvert.SerializeObject(allPrecinctsList, Formatting.None);
            string inputDirectory = Path.GetDirectoryName(seattleResultsPath);
            string fileName = Path.GetFileNameWithoutExtension(seattleResultsPath);
            string outputFile = Path.Combine(inputDirectory, $"converted-{fileName}.json");
            File.WriteAllText(outputFile, json);
            Console.WriteLine("Done");
        }

        public static List<string> SplitLine(string line)
        {
            List<string> items = new List<string>();
            int lastCommaIndex = -1;
            bool seenQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (!seenQuote)
                    {
                        seenQuote = true;
                        continue;
                    }
                    else
                    {
                        seenQuote = false;
                    }
                }
                if (c == ',' && !seenQuote)
                {
                    if (lastCommaIndex == -1)
                    {
                        items.Add(line.Substring(0, i - 0));
                    }
                    else
                    {
                        items.Add(line.Substring(lastCommaIndex + 1, i - lastCommaIndex - 1));
                    }
                    lastCommaIndex = i;
                }
            }
            items.Add(line.Substring(lastCommaIndex + 1, line.Length - lastCommaIndex - 1));
            return items;
        }
    }

    class ResultLine
    {
        public string Precinct { get; set; }
        public string Race { get; set; }
        public string CounterType { get; set; }
        public int SumOfCount { get; set; }
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
}
