using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.Random;
using MathNet.Numerics.Distributions;
using Newtonsoft.Json;

namespace SimulateElection
{
    class Program
    {
        // Mayor
        public const string Gonzalez = "M. Lorena González";
        public const string Harrell = "Bruce Harrell";

        public const string Durkan = "Jenny Durkan";
        public const string Moon = "Cary Moon";

        // Pos 8
        public const string Mosqueda = "Teresa Mosqueda";
        public const string Wilson = "Kenneth Wilson";

        // Pos 9
        public const string Oliver = "Nikkita Oliver";
        public const string Nelson = "Sara Nelson";

        // Attorney
        public const string Kennedy = "Nicole Thomas-Kennedy";
        public const string Davison = "Ann Davison";

        public const string Tie = "Tie";

        public const int SimulationRounds = 40000;

        public enum VoteSwing
        {
            Strong,
            Moderate,
            Tossup
        }

        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: SimulateElection <primary neighborhood election results json file> <neighborhood lean json file> <neighborhood turnout growth json file>");
                return;
            }

            string primaryResultsFile = args[0];
            string neighborhoodLeanFile = args[1];
            string neighborhoodTurnoutGrowthFile = args[2];

            List<NeighborhoodPrimaryResult> inputResults = JsonConvert.DeserializeObject<List<NeighborhoodPrimaryResult>>(File.ReadAllText(primaryResultsFile));
            List<NeighborhoodLean> neighborhoodLean = JsonConvert.DeserializeObject<List<NeighborhoodLean>>(File.ReadAllText(neighborhoodLeanFile));
            List<NeighborhoodTurnoutGrowth> turnoutGrowth = JsonConvert.DeserializeObject<List<NeighborhoodTurnoutGrowth>>(File.ReadAllText(neighborhoodTurnoutGrowthFile));

            List<Round> rounds = new List<Round>(SimulationRounds);
            for (int i = 0; i < SimulationRounds; i++)
            {
                rounds.Add(new Round()
                {
                    Number = i + 1,
                    Neighborhoods = new List<RoundNeighborhood>()
                });
            }

            Dictionary<string, (string, string)> raceToCandidateMap = new Dictionary<string, (string, string)>()
            {
                { "City of Seattle Mayor", (Gonzalez, Harrell) },
                { "City of Seattle Council Position No. 8", (Mosqueda, Wilson) },
                { "City of Seattle Council Position No. 9", (Oliver, Nelson) },
                { "City of Seattle City Attorney", (Kennedy, Davison) }
            };

            // Dictionary<string, (string, string)> raceTo2017CandidateMap = new Dictionary<string, (string, string)>()
            // {
            //     { "City of Seattle Mayor", (Durkan, Moon) }
            // };
            CreateAllRoundCollections(rounds, inputResults, raceToCandidateMap);

            System.Diagnostics.Debug.WriteLine("Starting simulation");

            Parallel.ForEach(inputResults, neighborhood =>
            {
                // Estimate the number of registered voters in this neighborhood, we're doing here so the number of
                // of registered voters is the same for all citywide races.
                NeighborhoodTurnoutGrowth neighborhoodTurnoutGrowth = turnoutGrowth.First(t => t.Name == neighborhood.Name);
                
                (double, double) registeredVotersChangeRange = (neighborhoodTurnoutGrowth.RegisteredVotersChangeMin,
                    neighborhoodTurnoutGrowth.RegisteredVotersChangeMax);
                
                // Get a citywide race
                Race citywideRace = neighborhood.Races.First(r => r.Name == "City of Seattle Mayor");

                Parallel.ForEach(rounds, round =>
                {
                    // Check to see if we have this neighborhood in this round already
                    RoundNeighborhood roundNeighborhood = round.Neighborhoods.First(n => n.Name == neighborhood.Name);

                    // Simulate a registered voter change distribution
                    double randomBetweenRegisteredVotersChangeRange = GetRandomDoubleBetweenRange(registeredVotersChangeRange);

                    // Now determine the new number of registered voters
                    int registeredVoters = (int)Math.Round(citywideRace.RegisteredVoters +
                        (citywideRace.RegisteredVoters * randomBetweenRegisteredVotersChangeRange));
                    
                    roundNeighborhood.RegisteredVoters = registeredVoters;
                });

                Parallel.ForEach(neighborhood.Races, race =>
                {
                    // Get the estimated lean for this neighborhood
                    RaceLean neighborhoodRaceLean = GetNeighborhoodRaceLean(neighborhood.Name, race.Name, neighborhoodLean);

                    if (race.Name == "City of Seattle Mayor")
                    {
                        SimulateRace(race, Gonzalez, Harrell, GetMayorVoteSwing, neighborhoodRaceLean, rounds,
                            neighborhood, turnoutGrowth, true);
                        // SimulateRace(race, Durkan, Moon, Get2017MayorVoteSwing, neighborhoodRaceLean, rounds,
                        //     neighborhood, turnoutGrowth, true);
                    }
                    else if (race.Name == "City of Seattle Council Position No. 8")
                    {
                        SimulateRace(race, Mosqueda, Wilson, GetPos8VoteSwing, neighborhoodRaceLean, rounds,
                            neighborhood, turnoutGrowth, true);
                    }
                    else if (race.Name == "City of Seattle Council Position No. 9")
                    {
                        SimulateRace(race, Oliver, Nelson, GetPos9VoteSwing, neighborhoodRaceLean, rounds,
                            neighborhood, turnoutGrowth, true);
                    }
                    else if (race.Name == "City of Seattle City Attorney")
                    {
                        SimulateRace(race, Kennedy, Davison, GetAttorneyVoteSwing, neighborhoodRaceLean, rounds,
                            neighborhood, turnoutGrowth, true);
                    }
                });
            });

            System.Diagnostics.Debug.WriteLine("Simulation complete");

            SimulationResult result = new SimulationResult()
            {
                Simulations = SimulationRounds,
                Races = new List<SimulationResultRace>(),
                Neighborhoods = new List<SimulationResultNeighborhood>()
            };

            CreateAllSimulationResultCollections(result, inputResults, raceToCandidateMap);

            System.Diagnostics.Debug.WriteLine("Aggregating results");

            // Aggregate the round results together
            Parallel.ForEach(rounds, round =>
            {
                Parallel.ForEach(round.Neighborhoods, neighborhood =>
                {
                    SimulationResultNeighborhood resultNeighborhood = result.Neighborhoods.First(n => n.Name == neighborhood.Name);

                    Parallel.ForEach(neighborhood.Races, race =>
                    {
                        SimulationResultNeighborhoodRace resultNeighborhoodRace = resultNeighborhood.Races.First(r => r.Name == race.Name);
                        resultNeighborhoodRace.AddRound(race);
                        
                        Parallel.ForEach(race.Votes, vote =>
                        {
                            SimulationResultVoteItem resultVote = resultNeighborhoodRace.Votes.First(i => i.Item == vote.Item);
                            resultVote.Votes.AddValue(vote.votes);
                        });
                    });
                });

                Parallel.ForEach(round.Races, race =>
                {
                    SimulationResultRace resultRace = result.Races.First(r => r.Name == race.Name);
                    resultRace.AddRound(race);
                    resultRace.Candidates[0].Votes.AddValue(race.Votes[0].votes);
                    resultRace.Candidates[1].Votes.AddValue(race.Votes[1].votes);

                    if (race.Votes[0].votes > race.Votes[1].votes)
                    {
                        Interlocked.Increment(ref resultRace.Candidates[0].Wins);
                    }
                    else if (race.Votes[1].votes > race.Votes[0].votes)
                    {
                        Interlocked.Increment(ref resultRace.Candidates[1].Wins);
                    }
                    else
                    {
                        // Tie
                    }
                });
            });

            List<OutputNeighborhood> outputResults = new List<OutputNeighborhood>();

            foreach (SimulationResultRace race in result.Races)
            {
                Console.WriteLine($"Race: {race.Name}");
                int ties = result.Simulations - race.Candidates[0].Wins - race.Candidates[1].Wins;
                Console.WriteLine($"{race.Candidates[0].Name} wins {GetWinPercentage(race.Candidates[0].Wins, result.Simulations):P}");
                Console.WriteLine($"{race.Candidates[1].Name} wins {GetWinPercentage(race.Candidates[1].Wins, result.Simulations):P}");
                if (ties > 0)
                {
                    Console.WriteLine($"Candidates tied {GetWinPercentage(ties, result.Simulations):P}");
                }
                Console.WriteLine();
                Console.WriteLine(race.GetElectionResults());

                Console.WriteLine();
                Console.WriteLine(race.Candidates[0].GetStats());
                Console.WriteLine();
                Console.WriteLine(race.Candidates[1].GetStats());
                Console.WriteLine();

                List<SimulationResultNeighborhoodRace> neighborhoods = new List<SimulationResultNeighborhoodRace>();
                foreach (SimulationResultNeighborhood neighborhood in result.Neighborhoods)
                {
                    SimulationResultNeighborhoodRace resultNeighborhoodRace = neighborhood.Races.First(r => r.Name == race.Name);
                    resultNeighborhoodRace.NeighborhoodName = neighborhood.Name;
                    neighborhoods.Add(resultNeighborhoodRace);

                    OutputNeighborhood outputNeighborhood = outputResults.FirstOrDefault(n => n.Name == neighborhood.Name);
                    if (outputNeighborhood == null)
                    {
                        outputNeighborhood = new OutputNeighborhood()
                        {
                            Name = neighborhood.Name,
                            Races = new List<Race>()
                        };
                        outputResults.Add(outputNeighborhood);
                    }

                    Race outputRace = outputNeighborhood.Races.FirstOrDefault(r => r.Name == race.Name);
                    if (outputRace == null)
                    {
                        outputRace = new Race()
                        {
                            Name = race.Name,
                            RegisteredVoters = (int)Math.Round(resultNeighborhoodRace.RegisteredVoters.Mean),
                            TotalVotes = resultNeighborhoodRace.TotalVotes,
                            Votes = new List<VoteItem>()
                        };
                        VoteItem candidate1VoteItem = new VoteItem()
                        {
                            Item = resultNeighborhoodRace.Votes[0].Item,
                            votes = (int)Math.Round(resultNeighborhoodRace.Votes[0].Votes.Mean)
                        };
                        VoteItem candidate2VoteItem = new VoteItem()
                        {
                            Item = resultNeighborhoodRace.Votes[1].Item,
                            votes = (int)Math.Round(resultNeighborhoodRace.Votes[1].Votes.Mean)
                        };
                        outputRace.Votes.Add(candidate1VoteItem);
                        outputRace.Votes.Add(candidate2VoteItem);
                        outputNeighborhood.Races.Add(outputRace);
                    }
                }

                // Registered voters table
                neighborhoods.Sort((n1, n2) => n2.RegisteredVoters.Mean.CompareTo(n1.RegisteredVoters.Mean));
                Console.WriteLine("Neighborhoods with the most registered voters");
                neighborhoods.Take(10).ToList().ForEach(n => Console.WriteLine($"{n.NeighborhoodName} {n.RegisteredVoters.MeanInt} {(n.RegisteredVoters.Mean / race.RegisteredVoters.Mean):P}"));
                Console.WriteLine();
                Console.WriteLine("Neighborhoods with the least registered voters");
                neighborhoods.TakeLast(10).ToList().ForEach(n => Console.WriteLine($"{n.NeighborhoodName} {n.RegisteredVoters.MeanInt} {(n.RegisteredVoters.Mean / race.RegisteredVoters.Mean):P}"));
                Console.WriteLine();

                // Impactful neighborhoods table
                neighborhoods.Sort((n1, n2) => n2.TotalVotes.CompareTo(n1.TotalVotes));
                Console.WriteLine("Neighborhoods with the most votes");
                neighborhoods.Take(10).ToList().ForEach(n => Console.WriteLine($"{n.NeighborhoodName} {n.TotalVotes} {(n.TotalVotes / (double)race.TotalVotes):P}"));
                Console.WriteLine();
                Console.WriteLine("Neighborhoods with the least votes");
                neighborhoods.TakeLast(10).ToList().ForEach(n => Console.WriteLine($"{n.NeighborhoodName} {n.TotalVotes} {(n.TotalVotes / (double)race.TotalVotes):P}"));
                Console.WriteLine();

                // Best performing neighborhoods for candidate 1
                neighborhoods.Sort((n1, n2) =>
                {
                    double n1Difference = (n1.Votes[0].Votes.Mean / n1.TotalVotes) - (n1.Votes[1].Votes.Mean / n1.TotalVotes);
                    double n2Difference = (n2.Votes[0].Votes.Mean / n2.TotalVotes) - (n2.Votes[1].Votes.Mean / n2.TotalVotes);
                    return n2Difference.CompareTo(n1Difference);
                });
                Console.WriteLine($"Neighborhoods with the highest vote percentage for {race.Candidates[0].Name}");
                neighborhoods.Take(10).ToList().ForEach(n => Console.WriteLine($"{n.NeighborhoodName} {n.Votes[0].Item} {n.Votes[0].Votes.MeanInt} {(n.Votes[0].Votes.Mean / n.TotalVotes):P} {n.Votes[1].Item} {n.Votes[1].Votes.MeanInt} {(n.Votes[1].Votes.Mean / n.TotalVotes):P}"));
                Console.WriteLine();

                // Best performing neighborhoods for candidate 2
                neighborhoods.Sort((n1, n2) =>
                {
                    double n1Difference = (n1.Votes[1].Votes.Mean / n1.TotalVotes) - (n1.Votes[0].Votes.Mean / n1.TotalVotes);
                    double n2Difference = (n2.Votes[1].Votes.Mean / n2.TotalVotes) - (n2.Votes[0].Votes.Mean / n2.TotalVotes);
                    return n2Difference.CompareTo(n1Difference);
                });
                Console.WriteLine($"Neighborhoods with the highest vote percentage for {race.Candidates[1].Name}");
                neighborhoods.Take(10).ToList().ForEach(n => Console.WriteLine($"{n.NeighborhoodName} {n.Votes[1].Item} {n.Votes[1].Votes.MeanInt} {(n.Votes[1].Votes.Mean / n.TotalVotes):P} {n.Votes[0].Item} {n.Votes[0].Votes.MeanInt} {(n.Votes[0].Votes.Mean / n.TotalVotes):P}"));
                Console.WriteLine();

                // Most competitive neighborhoods
                neighborhoods.Sort((n1, n2) =>
                {
                    double n1AbsDifference = Math.Abs(n1.Votes[0].Votes.Mean - n1.Votes[1].Votes.Mean);
                    double n2AbsDifference = Math.Abs(n2.Votes[0].Votes.Mean - n2.Votes[1].Votes.Mean);
                    return n1AbsDifference.CompareTo(n2AbsDifference);
                });
                Console.WriteLine($"Neighborhoods with the closest vote percentages");
                neighborhoods.Take(10).ToList().ForEach(n => Console.WriteLine($"{n.NeighborhoodName} {(int)Math.Round(Math.Abs(n.Votes[0].Votes.Mean - n.Votes[1].Votes.Mean))} {Math.Abs((n.Votes[0].Votes.Mean / n.TotalVotes) - (n.Votes[1].Votes.Mean / n.TotalVotes)):P} {n.Votes[0].Item} {n.Votes[0].Votes.MeanInt} {(n.Votes[0].Votes.Mean / n.TotalVotes):P} {n.Votes[1].Item} {n.Votes[1].Votes.MeanInt} {(n.Votes[1].Votes.Mean / n.TotalVotes):P}"));
                Console.WriteLine();
            }

            // Get estimates directory
            string estimatesDirectory = Path.GetDirectoryName(neighborhoodLeanFile);
            string outputFile = Path.Combine(estimatesDirectory, "neighborhood-estimated-results.json");
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(outputResults, Formatting.None));
            Console.WriteLine("Done");
        }

        static void CreateAllRoundCollections(List<Round> rounds, List<NeighborhoodPrimaryResult> neighborhoods,
            Dictionary<string, (string, string)> raceToCandidatesMap)
        {
            foreach (var neighborhood in neighborhoods)
            {
                foreach (var round in rounds)
                {
                    var roundNeighborhood = new RoundNeighborhood()
                    {
                        Name = neighborhood.Name
                    };
                    round.Neighborhoods.Add(roundNeighborhood);

                    foreach (var pair in raceToCandidatesMap)
                    {
                        var roundRace = new RoundRace()
                        {
                            Name = pair.Key
                        };
                        roundNeighborhood.Races.Add(roundRace);

                        roundRace.Votes.Add(new VoteItem()
                        {
                            Item = pair.Value.Item1
                        });
                        roundRace.Votes.Add(new VoteItem()
                        {
                            Item = pair.Value.Item2
                        });
                    }
                }
            }

            foreach (var round in rounds)
            {
                foreach (var pair in raceToCandidatesMap)
                {
                    var roundRace = new RoundRace()
                    {
                        Name = pair.Key
                    };
                    round.Races.Add(roundRace);

                    roundRace.Votes.Add(new VoteItem()
                    {
                        Item = pair.Value.Item1
                    });
                    roundRace.Votes.Add(new VoteItem()
                    {
                        Item = pair.Value.Item2
                    });
                }
            }
        }

        static void CreateAllSimulationResultCollections(SimulationResult result, List<NeighborhoodPrimaryResult> neighborhoods,
            Dictionary<string, (string, string)> raceToCandidatesMap)
        {
            foreach (var pair in raceToCandidatesMap)
            {
                var resultRace = new SimulationResultRace()
                {
                    Name = pair.Key
                };
                result.Races.Add(resultRace);

                resultRace.Candidates.Add(new CandidateWin()
                {
                    Name = pair.Value.Item1
                });
                resultRace.Candidates.Add(new CandidateWin()
                {
                    Name = pair.Value.Item2
                });
            }

            foreach (var neighborhood in neighborhoods)
            {
                var resultNeighborhood = new SimulationResultNeighborhood()
                {
                    Name = neighborhood.Name
                };
                result.Neighborhoods.Add(resultNeighborhood);

                foreach (var pair in raceToCandidatesMap)
                {
                    var resultNeighborhoodRace = new SimulationResultNeighborhoodRace()
                    {
                        Name = pair.Key
                    };
                    resultNeighborhood.Races.Add(resultNeighborhoodRace);

                    resultNeighborhoodRace.Votes.Add(new SimulationResultVoteItem()
                    {
                        Item = pair.Value.Item1
                    });
                    resultNeighborhoodRace.Votes.Add(new SimulationResultVoteItem()
                    {
                        Item = pair.Value.Item2
                    });
                }
            }
        }

        static void SimulateRace(Race race, string candidate1, string candidate2,
            Func<string, (VoteSwing swing, string candidateName)> getRaceVoteSwing, RaceLean neighborhoodRaceLean,
            List<Round> rounds, NeighborhoodPrimaryResult neighborhood, List<NeighborhoodTurnoutGrowth> turnoutGrowth,
            bool isCityWide)
        {
            SimulateOtherCandidatesVoteDistribution(race, candidate1, candidate2, getRaceVoteSwing, neighborhood,
                neighborhoodRaceLean, rounds);
            SimulateNeighborhoodTurnoutGrowthVoteDistribution(race, candidate1, candidate2, neighborhood,
                turnoutGrowth, neighborhoodRaceLean, rounds, isCityWide);
        }

        static void SimulateOtherCandidatesVoteDistribution(Race race, string candidate1, string candidate2,
            Func<string, (VoteSwing swing, string candidateName)> getRaceVoteSwing, NeighborhoodPrimaryResult neighborhood,
            RaceLean neighborhoodRaceLean, List<Round> rounds)
        {
            Parallel.ForEach(race.Votes, votes =>
            {
                if (votes.Item != candidate1 && votes.Item != candidate2)
                {
                    // First get the expected swing
                    var swing = getRaceVoteSwing(votes.Item);

                    // If the race is a tossup lean towards the neighborhood lean.
                    if (swing.candidateName == string.Empty)
                    {
                        swing.candidateName = neighborhoodRaceLean.Candidate;
                    }

                    // Next get the vote range based upon the expected swing
                    var swingRange = GetVoteSwingRange(swing.swing);

                    // Next adjust the vote range based upon the neighborhood lean
                    var adjustedSwingRange = AdjustVoteSwingRangeWithNeighborhoodLean(swingRange,
                        swing.candidateName, neighborhoodRaceLean.Lean, neighborhoodRaceLean.Candidate);

                    // Now simulate votes for this race in this neighborhood and add those results to the
                    // city wide simulation results.
                    Parallel.ForEach(rounds, round =>
                    {
                        // Check to see if we have this neighborhood in this round already
                        RoundNeighborhood roundNeighborhood = round.Neighborhoods.First(n => n.Name == neighborhood.Name);

                        // Check to see if we have this race in this round already
                        RoundRace roundNeighborhoodRace = roundNeighborhood.Races.First(r => r.Name == race.Name);
                        RoundRace roundRace = round.Races.First(r => r.Name == race.Name);

                        // Add this candidates votes to the vote totals for the race
                        Interlocked.Add(ref roundNeighborhoodRace.totalVotes, votes.votes);
                        Interlocked.Add(ref roundNeighborhoodRace.totalReassignedVotes, votes.votes);
                        Interlocked.Add(ref roundRace.totalVotes, votes.votes);
                        Interlocked.Add(ref roundRace.totalReassignedVotes, votes.votes);

                        // Get the 2 candidate vote totals
                        var candidate1NeighborhoodVoteItem = roundNeighborhoodRace.Votes.First(r => r.Item == candidate1);
                        var candidate2NeighborhoodVoteItem = roundNeighborhoodRace.Votes.First(r => r.Item == candidate2);
                        var candidate1VoteItem = roundRace.Votes.First(r => r.Item == candidate1);
                        var candidate2VoteItem = roundRace.Votes.First(r => r.Item == candidate2);

                        // Now simulate a vote distribution
                        double randomBetweenRange = GetRandomDoubleBetweenRange(adjustedSwingRange);
                        var voteDistribution = GetVoteDistribution(randomBetweenRange, votes.votes);

                        // Finally, if the swing is towards candidate1, add the *higher* vote total to them.
                        if (swing.candidateName == candidate1)
                        {
                            Interlocked.Add(ref candidate1NeighborhoodVoteItem.votes, voteDistribution.Item1);
                            Interlocked.Add(ref candidate2NeighborhoodVoteItem.votes, voteDistribution.Item2);
                            Interlocked.Add(ref candidate1VoteItem.votes, voteDistribution.Item1);
                            Interlocked.Add(ref candidate2VoteItem.votes, voteDistribution.Item2);
                        }
                        // Otherwise add the *higher* vote total to candidate2.
                        else if (swing.candidateName == candidate2)
                        {
                            Interlocked.Add(ref candidate2NeighborhoodVoteItem.votes, voteDistribution.Item1);
                            Interlocked.Add(ref candidate1NeighborhoodVoteItem.votes, voteDistribution.Item2);
                            Interlocked.Add(ref candidate2VoteItem.votes, voteDistribution.Item1);
                            Interlocked.Add(ref candidate1VoteItem.votes, voteDistribution.Item2);
                        }
                    });
                }
                // Also add the votes for candidate1
                else if (votes.Item == candidate1)
                {
                    AddCandidateVotes(candidate1, neighborhood, race, rounds, votes);
                }
                else if (votes.Item == candidate2)
                {
                    AddCandidateVotes(candidate2, neighborhood, race, rounds, votes);
                }
            });
        }

        static void SimulateNeighborhoodTurnoutGrowthVoteDistribution(Race race, string candidate1, string candidate2,
            NeighborhoodPrimaryResult neighborhood, List<NeighborhoodTurnoutGrowth> turnoutGrowth,
            RaceLean neighborhoodRaceLean, List<Round> rounds, bool isCityWide)
        {
            NeighborhoodTurnoutGrowth neighborhoodTurnoutGrowth = turnoutGrowth.First(t => t.Name == neighborhood.Name);
            (double, double) registeredVotersChangeRange = (neighborhoodTurnoutGrowth.RegisteredVotersChangeMin,
                neighborhoodTurnoutGrowth.RegisteredVotersChangeMax);

            // Also create a range for the neighborhood lean
            (double, double) neighborhoodRaceLeanRange = (neighborhoodRaceLean.Lean - 0.1, neighborhoodRaceLean.Lean + 0.1);
            Parallel.ForEach(rounds, round =>
            {
                // Get neighborhood for this round (it will exist since we created it for distributing candidate votes)
                RoundNeighborhood roundNeighborhood = round.Neighborhoods.First(n => n.Name == neighborhood.Name);

                // Get race for this round (it will exist since we created it for distributing candidate votes)
                RoundRace roundNeighborhoodRace = roundNeighborhood.Races.First(r => r.Name == race.Name);
                RoundRace roundRace = round.Races.First(r => r.Name == race.Name);

                // If this is a citywide race use the citywide registered voter count, if it is not determine the
                // number of registered voters here.
                int registeredVoters;
                if (isCityWide)
                {
                    registeredVoters = roundNeighborhood.RegisteredVoters;
                }
                else
                {
                    // Simulate a registered voter change distribution
                    double randomBetweenRegisteredVotersChangeRange = GetRandomDoubleBetweenRange(registeredVotersChangeRange);

                    // Now determine the new number of registered voters
                    registeredVoters = (int)Math.Round(race.RegisteredVoters +
                        (race.RegisteredVoters * randomBetweenRegisteredVotersChangeRange));
                }

                // Now determine the turnout percentage and turnout growth range
                double turnoutPercent = (double)race.TotalVotes / registeredVoters;
                (double, double) turnoutGrowthRange = (turnoutPercent + Math.Max(neighborhoodTurnoutGrowth.TurnoutGrowthMin - 0.05, 0.0),
                    turnoutPercent + (neighborhoodTurnoutGrowth.TurnoutGrowthMax + 0.05));

                // Simulate a turnout distribution
                double randomBetweenTurnoutRange = GetRandomDoubleBetweenRange(turnoutGrowthRange);

                // Simulate a neighborhood lean
                double randomBetweenNeighborhoodLeanRange = GetRandomDoubleBetweenRange(neighborhoodRaceLeanRange);

                // Now get the new vote total and the new votes
                int voteGrowthTotal = (int)Math.Floor(race.RegisteredVoters * randomBetweenTurnoutRange);
                int voteGrowth = voteGrowthTotal - race.TotalVotes;

                // Add the new votes to the vote totals and add the estimated registered voters to the round total
                Interlocked.Add(ref roundNeighborhoodRace.totalVotes, voteGrowth);
                Interlocked.Add(ref roundNeighborhoodRace.totalNewVotes, voteGrowth);
                Interlocked.Add(ref roundNeighborhoodRace.registeredVoters, registeredVoters);
                Interlocked.Add(ref roundRace.totalVotes, voteGrowth);
                Interlocked.Add(ref roundRace.totalNewVotes, voteGrowth);
                Interlocked.Add(ref roundRace.registeredVoters, registeredVoters);
                
                if (voteGrowth > 0)
                {
                    // Now if we have vote growth, distribute the votes along the random neighborhood lean
                    // Get the 2 candidate vote totals
                    var candidate1NeighborhoodVoteItem = roundNeighborhoodRace.Votes.First(r => r.Item == candidate1);
                    var candidate2NeighborhoodVoteItem = roundNeighborhoodRace.Votes.First(r => r.Item == candidate2);
                    var candidate1VoteItem = roundRace.Votes.First(r => r.Item == candidate1);
                    var candidate2VoteItem = roundRace.Votes.First(r => r.Item == candidate2);

                    double neighborhoodRaceLeanValue = randomBetweenNeighborhoodLeanRange;
                    string neighborhoodRaceLeanCandidate = neighborhoodRaceLean.Candidate;
                    if (randomBetweenNeighborhoodLeanRange < 0.5)
                    {
                        neighborhoodRaceLeanValue = 1 - randomBetweenNeighborhoodLeanRange;
                        if (neighborhoodRaceLean.Candidate == candidate1)
                        {
                            neighborhoodRaceLeanCandidate = candidate2;
                        }
                        else if (neighborhoodRaceLean.Candidate == candidate2)
                        {
                            neighborhoodRaceLeanCandidate = candidate1;
                        }
                    }

                    var voteDistribution = GetVoteDistribution(neighborhoodRaceLeanValue, voteGrowth);
                    if (neighborhoodRaceLeanCandidate == candidate1)
                    {
                        Interlocked.Add(ref candidate1NeighborhoodVoteItem.votes, voteDistribution.Item1);
                        Interlocked.Add(ref candidate2NeighborhoodVoteItem.votes, voteDistribution.Item2);
                        Interlocked.Add(ref candidate1VoteItem.votes, voteDistribution.Item1);
                        Interlocked.Add(ref candidate2VoteItem.votes, voteDistribution.Item2);
                    }
                    else if (neighborhoodRaceLeanCandidate == candidate2)
                    {
                        Interlocked.Add(ref candidate2NeighborhoodVoteItem.votes, voteDistribution.Item1);
                        Interlocked.Add(ref candidate1NeighborhoodVoteItem.votes, voteDistribution.Item2);
                        Interlocked.Add(ref candidate2VoteItem.votes, voteDistribution.Item1);
                        Interlocked.Add(ref candidate1VoteItem.votes, voteDistribution.Item2);
                    }
                }
            });
        }

        static void AddCandidateVotes(string candidateName, NeighborhoodPrimaryResult neighborhood, Race race,
            List<Round> rounds, VoteItem votes)
        {
            Parallel.ForEach(rounds, round =>
            {
                // Check to see if we have this neighborhood in this round already
                RoundNeighborhood roundNeighborhood = round.Neighborhoods.First(n => n.Name == neighborhood.Name);

                // Check to see if we have this race in this round already
                RoundRace roundNeighborhoodRace = roundNeighborhood.Races.First(r => r.Name == race.Name);
                RoundRace roundRace = round.Races.First(r => r.Name == race.Name);

                // Add this candidates votes to the vote totals for the race
                Interlocked.Add(ref roundNeighborhoodRace.totalVotes, votes.votes);
                Interlocked.Add(ref roundNeighborhoodRace.totalTranferredVotes, votes.votes);
                Interlocked.Add(ref roundRace.totalVotes, votes.votes);
                Interlocked.Add(ref roundRace.totalTranferredVotes, votes.votes);

                // Get this candidate's vote totals
                var candidateNeighborhoodVoteItem = roundNeighborhoodRace.Votes.First(r => r.Item == candidateName);
                var candidateVoteItem = roundRace.Votes.First(r => r.Item == candidateName);
                Interlocked.Add(ref candidateNeighborhoodVoteItem.votes, votes.votes);
                Interlocked.Add(ref candidateVoteItem.votes, votes.votes);
            });
        }

        /// <summary>
        /// Get the expected swing for a mayor vote
        /// </summary>
        /// <param name="candidateName">The name of the Mayoral candidate</param>
        /// <returns>A tuple of (VoteSwing, candidateName) where VoteSwing is the level of swing and candidateName
        /// is the candidate the vote is expected to swing towards. If VoteSwing == Tossup, candidateName will be
        /// empty.</returns>
        static (VoteSwing swing, string candidateName) GetMayorVoteSwing(string candidateName)
        {
            if (candidateName == "Colleen Echohawk")
            {
                return (VoteSwing.Tossup, string.Empty);
            }
            else if (candidateName == "Jessyn Farrell")
            {
                return (VoteSwing.Moderate, Harrell);
            }
            else if (candidateName == "Arthur K. Langlie")
            {
                return (VoteSwing.Strong, Harrell);
            }
            else if (candidateName == "Casey Sixkiller")
            {
                return (VoteSwing.Strong, Harrell);
            }
            else if (candidateName == "Andrew Grant Houston")
            {
                return (VoteSwing.Strong, Gonzalez);
            }
            else if (candidateName == "Lance Randall")
            {
                return (VoteSwing.Moderate, Harrell);
            }
            else
            {
                return (VoteSwing.Tossup, string.Empty);
            }
        }

        static (VoteSwing swing, string candidateName) Get2017MayorVoteSwing(string candidateName)
        {
            if (candidateName == "Nikkita Oliver")
            {
                return (VoteSwing.Strong, Moon);
            }
            else if (candidateName == "Mike McGinn")
            {
                return (VoteSwing.Strong, Moon);
            }
            else if (candidateName == "Bob Hasegawa")
            {
                return (VoteSwing.Moderate, Durkan);
            }
            else if (candidateName == "Jessyn Farrell")
            {
                return (VoteSwing.Moderate, Durkan);
            }
            else
            {
                return (VoteSwing.Tossup, string.Empty);
            }
        }

        static (VoteSwing swing, string candidateName) GetPos8VoteSwing(string candidateName)
        {
            if (candidateName == "Kate Martin")
            {
                return (VoteSwing.Strong, Wilson);
            }
            else
            {
                return (VoteSwing.Tossup, string.Empty);
            }
        }

        static (VoteSwing swing, string candidateName) GetPos9VoteSwing(string candidateName)
        {
            return (VoteSwing.Tossup, string.Empty);
        }

        static (VoteSwing swing, string candidateName) GetAttorneyVoteSwing(string candidateName)
        {
            return (VoteSwing.Tossup, string.Empty);
        }

        /// <summary>
        /// Get the vote range based upon the VoteSwing
        /// </summary>
        /// <param name="voteSwing">The VoteSwing</param>
        /// <returns>A vote range (double, double)</returns>
        static (double, double) GetVoteSwingRange(VoteSwing voteSwing)
        {
            if (voteSwing == VoteSwing.Strong)
            {
                return (0.7, 1.0);
            }
            else if (voteSwing == VoteSwing.Moderate)
            {
                return (0.55, 0.7);
            }
            else if (voteSwing == VoteSwing.Tossup)
            {
                return (0.45, 0.55);
            }
            else
            {
                throw new ArgumentException("voteSwing");
            }
        }

        /// <summary>
        /// Adjust the vote range based upon the expected candidate lean and the estimated neighborhood lean.
        /// 
        /// If the range and neighborhood lean in towards the same candidate the vote probability will be increased.
        /// If the range candidate lean does not match the neighborhood candidate lean, the vote probability will be
        /// decreased. This is because it's expected that the lean of the neighborhood will pull towards the
        /// neighborhood candidate.
        /// </summary>
        /// <param name="range">The vote range</param>
        /// <param name="rangeCandidateLean">The candidate lean of the range</param>
        /// <param name="neighborhoodLean">The neighborhood lean</param>
        /// <param name="neighborhoodLeanCandidate">The candidate the neighborhood leans towards</param>
        /// <returns>The adjusted vote range (double, double).</returns>
        static (double, double) AdjustVoteSwingRangeWithNeighborhoodLean((double, double) range,
            string rangeCandidateLean, double neighborhoodLean, string neighborhoodLeanCandidate)
        {
            // The neighborhood lean for assigning other votes is actually the difference between the percentages of the
            // two candidates. This is because if the neighborhood lean was 0.5 (50% voted for a candidate) the chance
            // of voting for someone shouldn't be 0.05 but should instead be 0 because the neighborhood was split 50/50.
            double adjustedNeighborhoodLean = (neighborhoodLean - (1 - neighborhoodLean)) * 0.5;
            if (rangeCandidateLean == neighborhoodLeanCandidate || rangeCandidateLean == string.Empty)
            {
                
                return (Math.Clamp(range.Item1 + adjustedNeighborhoodLean, 0.0, 1.0),
                    Math.Clamp(range.Item2 + adjustedNeighborhoodLean, 0.0, 1.0));
            }
            else
            {
                return (Math.Clamp(range.Item1 - adjustedNeighborhoodLean, 0.0, 1.0),
                    Math.Clamp(range.Item2 - adjustedNeighborhoodLean, 0.0, 1.0));
            }
        }

        /// <summary>
        /// Get the estimated lean of a given neighborhood for a given race.
        /// </summary>
        /// <param name="neighborhoodName">The neighborhood</param>
        /// <param name="raceName">The race</param>
        /// <param name="neighborhoodLean">The neighborhood lean info</param>
        /// <returns>The estimated lean (double) towards a candidate (string)</returns>
        static RaceLean GetNeighborhoodRaceLean(string neighborhoodName, string raceName, List<NeighborhoodLean> neighborhoodLean)
        {
            foreach (NeighborhoodLean neighborhood in neighborhoodLean)
            {
                if (neighborhood.Name == neighborhoodName)
                {
                    foreach (RaceLean race in neighborhood.Races)
                    {
                        if (race.Name == raceName)
                        {
                            return race;
                        }
                    }
                }
            }
            return null;
        }

        static double GetRandomDoubleBetweenRange((double, double) range)
        {
            // Instead of using a uniform distribution here use a triangular distribution. The mode will just be the
            // middle of the range.
            return Triangular.Sample(range.Item1, range.Item2, (range.Item1 + range.Item2) / 2.0);
        }

        static (int, int) GetVoteDistribution(double random, int totalVotes)
        {
            int left = (int)Math.Round(totalVotes * random);
            int right = totalVotes - left;
            return (left, right);
        }

        static double GetWinPercentage(int wins, int simulations)
        {
            return ((double)wins / (double)simulations);
        }
    }

    class NeighborhoodPrimaryResult
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

        public int votes;
    }

    class NeighborhoodLean
    {
        [JsonProperty("neighborhood")]
        public string Name { get; set; }

        [JsonProperty("races")]
        public List<RaceLean> Races { get; set; }
    }

    class RaceLean
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("candidate")]
        public string Candidate { get; set; }

        [JsonProperty("lean")]
        public double Lean { get; set; }
    }

    class NeighborhoodTurnoutGrowth
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

    class Round
    {
        public int Number { get; set; }
        public List<RoundNeighborhood> Neighborhoods { get; set; }
        public List<RoundRace> Races { get; set; }

        public Round()
        {
            Neighborhoods = new List<RoundNeighborhood>();
            Races = new List<RoundRace>();
        }
    }

    class RoundNeighborhood
    {
        public string Name { get; set; }
        public int RegisteredVoters { get; set; }
        public List<RoundRace> Races { get; set; }

        public RoundNeighborhood()
        {
            RegisteredVoters = 0;
            Races = new List<RoundRace>();
        }
    }

    class RoundRace
    {
        public string Name { get; set; }
        public int totalVotes;
        public int registeredVoters;
        public List<VoteItem> Votes { get; set; }

        /// <summary>
        /// Votes for a top 2 candidate that tranferred directly to the general.
        /// </summary>
        public int totalTranferredVotes;

        /// <summary>
        /// Votes that were reassigned from a candidate eliminated in the primary to a top 2 candidate in the general.
        /// </summary>
        public int totalReassignedVotes;

        /// <summary>
        /// New votes that were created because of increased turnout in the general.
        /// </summary>
        public int totalNewVotes;

        public RoundRace()
        {
            totalVotes = 0;
            registeredVoters = 0;
            Votes = new List<VoteItem>();
            totalTranferredVotes = 0;
            totalReassignedVotes = 0;
            totalNewVotes = 0;
        }
    }

    class SimulationResult
    {
        public int Simulations { get; set; }
        public List<SimulationResultRace> Races { get; set; }
        public List<SimulationResultNeighborhood> Neighborhoods { get; set; }
    }

    class SimulationResultRace
    {
        public string Name { get; set; }
        public Statistic RegisteredVoters;
        public Statistic TotalTranferredVotes;
        public Statistic TotalReassignedVotes;
        public Statistic TotalNewVotes;
        public List<CandidateWin> Candidates { get; set; }
        public int TotalVotes
        {
            get
            {
                return (int)Math.Round(Candidates[0].Votes.Mean + Candidates[1].Votes.Mean);
            }
        }

        public SimulationResultRace()
        {
            RegisteredVoters = new Statistic();
            TotalTranferredVotes = new Statistic();
            TotalReassignedVotes = new Statistic();
            TotalNewVotes = new Statistic();
            Candidates = new List<CandidateWin>();
        }

        public void AddRound(RoundRace race)
        {
            RegisteredVoters.AddValue(race.registeredVoters);
            TotalTranferredVotes.AddValue(race.totalTranferredVotes);
            TotalReassignedVotes.AddValue(race.totalReassignedVotes);
            TotalNewVotes.AddValue(race.totalNewVotes);
        }

        public string GetElectionResults()
        {
            string output = string.Empty;
            output += $"Estimated total votes: {TotalVotes}\n";
            output += $"Estimated registered voters: {RegisteredVoters.MeanInt}\n";
            double turnoutPercentage = TotalVotes / RegisteredVoters.Mean;
            output += $"Estimated turnout percentage: {turnoutPercentage:P}\n";
            double candidate1Percentage = Candidates[0].Votes.Mean / TotalVotes;
            output += $"Estimated {Candidates[0].Name} result: {Candidates[0].Votes.MeanInt} {candidate1Percentage:P}\n";
            double candidate2Percentage = Candidates[1].Votes.Mean / TotalVotes;
            output += $"Estimated {Candidates[1].Name} result: {Candidates[1].Votes.MeanInt} {candidate2Percentage:P}\n";
            double transferredPercentage = TotalTranferredVotes.Mean / TotalVotes;
            output += $"Votes for a top 2 candidate in the primary that transferred directly to the general: {TotalTranferredVotes.MeanInt} {transferredPercentage:P}\n";
            double reassignedPercentage = TotalReassignedVotes.Mean / TotalVotes;
            output += $"Votes that were reassigned from a candidate eliminated in the primary to a top 2 candidate in the general: {TotalReassignedVotes.MeanInt} {reassignedPercentage:P}\n";
            double newVotesPercentage = TotalNewVotes.Mean / TotalVotes;
            output += $"New votes that were created because of increased turnout in the general: {TotalNewVotes.MeanInt} {newVotesPercentage:P}";
            return output;
        }
    }

    class SimulationResultNeighborhood
    {
        public string Name { get; set; }
        public List<SimulationResultNeighborhoodRace> Races { get; set; }

        public SimulationResultNeighborhood()
        {
            Races = new List<SimulationResultNeighborhoodRace>();
        }
    }

    class SimulationResultNeighborhoodRace
    {
        public string Name { get; set; }
        public string NeighborhoodName { get; set; }
        public Statistic RegisteredVoters { get; set; }
        public Statistic TotalTranferredVotes { get; set; }
        public Statistic TotalReassignedVotes { get; set; }
        public Statistic TotalNewVotes { get; set; }
        public List<SimulationResultVoteItem> Votes { get; set; }
        public int TotalVotes
        {
            get
            {
                return (int)Math.Round(Votes[0].Votes.Mean + Votes[1].Votes.Mean);
            }
        }

        public SimulationResultNeighborhoodRace()
        {
            RegisteredVoters = new Statistic();
            TotalTranferredVotes = new Statistic();
            TotalReassignedVotes = new Statistic();
            TotalNewVotes = new Statistic();
            Votes = new List<SimulationResultVoteItem>();
        }

        public void AddRound(RoundRace roundRace)
        {
            RegisteredVoters.AddValue(roundRace.registeredVoters);
            TotalTranferredVotes.AddValue(roundRace.totalTranferredVotes);
            TotalReassignedVotes.AddValue(roundRace.totalReassignedVotes);
            TotalNewVotes.AddValue(roundRace.totalNewVotes);
        }
    }

    class SimulationResultVoteItem
    {
        public string Item { get; set; }
        public Statistic Votes { get; set; }

        public SimulationResultVoteItem()
        {
            Votes = new Statistic();
        }
    }

    class CandidateWin
    {
        public string Name { get; set; }
        public int Wins;
        public Statistic Votes { get; set; }

        public CandidateWin()
        {
            Wins = 0;
            Votes = new Statistic();
        }

        public string GetStats()
        {
            return $"{Name}\nWins: {Wins}\nAverage votes received: {Votes.MeanInt}";
            // return $"{Name}\nWins: {Wins}\nMinimum votes received: {Votes.MinValue}\nMaximum votes received: {Votes.MaxValue}\nAverage votes received: {Votes.Mean}";
        }
    }

    class Statistic
    {
        // public int MinValue { get; private set; } = int.MaxValue;
        // public int MaxValue { get; private set; } = int.MinValue;
        public double Mean
        {
            get
            {
                return valueSum / (double)seenValues;
            }
        }

        public int MeanInt
        {
            get
            {
                return (int)Math.Round(Mean);
            }
        }
        
        // private ConcurrentBag<int> values = new ConcurrentBag<int>();
        private long valueSum = 0;
        private int seenValues = 0;

        public void AddValue(int value)
        {
            // values.Add(value);
            Interlocked.Add(ref valueSum, value);
            Interlocked.Increment(ref seenValues);

            // if (value < MinValue)
            // {
            //     MinValue = value;
            // }

            // if (value > MaxValue)
            // {
            //     MaxValue = value;
            // }
        }
    }

    class OutputNeighborhood
    {
        [JsonProperty("neighborhood")]
        public string Name { get; set; }

        [JsonProperty("races")]
        public List<Race> Races { get; set; }
    }
}
