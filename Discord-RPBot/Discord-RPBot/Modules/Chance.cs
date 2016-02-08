using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;

namespace Discord_RPBot.Modules
{
    internal class Chance : IModule
    {
        private ModuleManager _manager;
        private DiscordClient _client;
        private List<Channel> _channelsToListenTo = new List<Channel>();
        private List<string> _defaultDeck = new List<string>();
        private Dictionary<Channel, List<string>> _cards = new Dictionary<Channel, List<string>>();

        void IModule.Install(ModuleManager manager)
        {
            _manager = manager;
            _client = manager.Client;
            InitializeDeck();

            manager.CreateCommands("Roll", group =>
            {
                group.CreateCommand("")
                .Description("Provides info on rolling")
                .Do(async e =>
                {
                    await _client.SendPrivateMessage(e.User, $"To roll, wrap your roll with [[ and ]]. Example: [[3d6]] or [[1 + 4d6 + 1d20]]");
                });

                group.CreateCommand("Listen")
                .Description("Asks Rollbot to listen to the channel for rolls")
                .Do(async e =>
                {
                    if (!_channelsToListenTo.Contains(e.Channel))
                    {

                        _channelsToListenTo.Add(e.Channel);
                        await _client.SendMessage(e.Channel, "Now listening for rolls in here");
                    }
                    else
                        await _client.SendMessage(e.Channel, "Already listening in here");
                });

                group.CreateCommand("Ignore")
                .Description("Asks Rollbot to stop listening to the channel for rolls")
                .Do(async e =>
                {
                    if (_channelsToListenTo.Contains(e.Channel))
                    {
                        _channelsToListenTo.Remove(e.Channel);
                        await _client.SendMessage(e.Channel, "No longer listening for rolls here");
                    }
                    else
                        await _client.SendMessage(e.Channel, "Wasn't listening anyways, haha");

                });

                group.CreateCommand("Force")
                .Description("Asks for a roll, even if rollbot isn't listening.")
                .Parameter("What you'd like to roll.")
                .Do(async e =>
                {
                    if (e.Args == null)
                    {
                        await _client.SendMessage(e.Channel, "No roll provided");
                        return;
                    }
                    string message = e.Args[0];
                    GetRoll(_client, e, message);
                });
            });

            manager.CreateCommands("Cards", group =>
            {
                group.CreateCommand("")
                .Description("Provides info on cards")
                .Do(async e =>
                {
                    await _client.SendPrivateMessage(e.User, "To draw a card, use !cards draw. To reset the deck, say !cards reset");
                });

                group.CreateCommand("Reset") //Assumes 0 Jokers.
                .Description("Resets the deck.")
                .Do(async e =>
                {
                    InitializeDeck(0, e.Channel);
                    await _client.SendMessage(e.Channel, $"Resetting the deck with no jokers.");
                });

                group.CreateCommand("Reset") //Takes a parameter.
                .Description("Resets the deck with a number of jokers.")
                .Parameter("Number of jokers to include, if any.")
                .Do(async e =>
                {
                    try
                    {
                        int jokers = 0;
                        jokers = int.Parse(e.Args[0]);
                        if (jokers < 0)
                            jokers = 0;
                        InitializeDeck(jokers, e.Channel);
                        await _client.SendMessage(e.Channel, $"Resetting the deck with {jokers} jokers.");
                    }
                    catch (FormatException ex)
                    {
                        await _client.SendMessage(e.Channel, "Please provide a number of jokers." +ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to reload deck. " + ex.Message);
                    }
                });

                group.CreateCommand("Draw")
                .Description("Draws a card from the current deck.")
                .Do(async e =>
                {
                    await _client.SendMessage(e.Channel, DrawCard(e.Channel));
                });

                group.CreateCommand("PDraw")
                .Description("Draws a card privately")
                .Do(async e =>
                {
                    await _client.SendPrivateMessage(e.User, DrawCard(e.Channel));
                });

                group.CreateCommand("List")
                .Description("Reveals the entire deck")
                .Do(async e =>
                {
                    EnsureDeckExists(e.Channel);
                    string cards = "";
                    foreach (string s in _cards[e.Channel])
                    {
                        cards += $"{s}, ";
                    }
                    if (cards.Length >= 2)
                    {
                        cards = cards.Remove(cards.Length - 2);
                    }
                    else
                        cards = "Nothing";
                    await _client.SendMessage(e.Channel, $"Deck currently contains: {cards}");
                });
            }); 

            _client.MessageReceived += (s, e) =>
            {
                if (e.User.Id != _client.CurrentUser.Id && _channelsToListenTo.Contains(e.Channel))
                {
                    string message = e.Message.Text;
                    GetRoll(_client,e, message);
                }
            };
            
        }


        /// <summary>
        /// Performs BODMAS operations and rolls any dice found.
        /// </summary>
        /// <param name="RollString">The string to scour for any roll notations</param>
        /// <param name="Rolls">A list of strings to add our dice results to.</param>
        /// <returns></returns>
        static double Roll(string RollString, List<string> Rolls)
        {
            //So we have the roll string, need to BODMAS it, regarding nDn last.
            //Since we're doing this recursively, just hunt for signs in order of BODMASd.

            //So, search for any "d"s to denote dice rolls.
            int DIndex = RollString.IndexOf('d');
            int MinusIndex = RollString.IndexOf('-'); //Special case: (-N) 
            int PlusIndex = RollString.IndexOf('+'); //Special case: (+N)
            int StarIndex = RollString.IndexOf('*');
            int SlashIndex = RollString.IndexOf('/');
            int HatIndex = RollString.IndexOf('^');
            int FirstBracketIndex = RollString.IndexOf('(');
            if (FirstBracketIndex != -1)
            {
                int ClosingBracketIndex = -1;
                //Find this open bracket's end bracket.
                int counter = 0;
                char[] chars = RollString.ToCharArray();
                for (int i = FirstBracketIndex+1; i < chars.Length; i++)
                {
                    if (chars[i] == '(')
                        counter++; //Add for every close bracket between us and our end bracker.
                    if (chars[i] == ')')
                    {
                        if (counter == 0)
                        {
                            //Found our match.
                            ClosingBracketIndex = i;
                            break;
                        }
                        else
                        {
                            counter--; //Getting closer
                        }
                    }
                }
                if (ClosingBracketIndex == -1) //Still no end bracket? Guess they screwed up.
                {
                    throw new InvalidOperationException("No matching End Bracket.");
                }
                //Slice up the string, figure out the bracket's result, reroll the whole thing with the brackets replaced.
                string prebracket = "";
                if (FirstBracketIndex != 0)
                {
                    prebracket = RollString.Substring(0, FirstBracketIndex);
                }
                string postbracket = RollString.Substring(ClosingBracketIndex+1);
                string brackets = RollString.Substring(FirstBracketIndex+1, ClosingBracketIndex - FirstBracketIndex -1);
                double bracketResult = Roll(brackets, Rolls);
                string newRollString = prebracket + bracketResult + postbracket;
                return Roll(newRollString, Rolls);
            }
            else if (PlusIndex != -1) //Found a plus
            {
                string particle1 = RollString.Substring(0, PlusIndex);
                string particle2 = RollString.Substring(PlusIndex + 1);
                if (particle1 == "")
                {
                    int signsIndex = particle2.IndexOfAny(new char[] { '+', '(', '-', '*', '/', '^' });
                    if (signsIndex != -1) //Particle2 itself contains signs, so we'll break up particle2.
                    {
                        double positiveNumber = double.Parse(particle2.Substring(0, signsIndex));
                        double SecondPart = Roll(RollString.Substring(signsIndex), Rolls);
                        return positiveNumber + SecondPart;
                    }
                    else //Particle2 is just the positive number.
                    {
                        return double.Parse(particle2);
                    }
                }
                else //There is a particle1, so we're just gonna do this old school.
                {
                    double result1 = Roll(particle1, Rolls);
                    double result2 = Roll(particle2, Rolls);
                    return result1 + result2;
                }
            }
            else if (MinusIndex != -1) //Found a minus
            {
                string particle1 = RollString.Substring(0, MinusIndex);
                string particle2 = RollString.Substring(MinusIndex + 1);
                if (particle1 == "")
                {
                    int signsIndex = particle2.IndexOfAny(new char[] { '+', '(', '-', '*', '/', '^' }); 
                    if (signsIndex != -1) //Particle2 itself contains signs, so we'll break up particle2.
                    {
                        double negativeNumber = -double.Parse(particle2.Substring(0, signsIndex));
                        double SecondPart = Roll(RollString.Substring(signsIndex), Rolls);
                        return negativeNumber + SecondPart;
                    }
                    else //Particle2 is just the negative number.
                    {
                        return -double.Parse(particle2);
                    }
                }
                else //There is a particle1, so we're just gonna do this old school.
                {
                    double result1 = Roll(particle1, Rolls);
                    double result2 = Roll(particle2, Rolls);
                    return result1 - result2;
                }
            }
            else if (DIndex != -1)
            {
                //We have a "d" present in the string.
                string particle1 = RollString.Substring(0, DIndex);
                string particle2 = RollString.Substring(DIndex + 1);
                //Calculate left and right side.
                double result1 = Roll(particle1, Rolls);
                double result2 = Roll(particle2, Rolls);
                //Round it, since half die and half-faces don't exist.
                int NumberToRoll = (int)Math.Round(result1);
                int SizeOfDie = (int)Math.Round(result2);
                Random rand = new Random();
                double finalResult = 0;
                //Roll a die for every die we're supposed to roll.
                Rolls.Add("(");
                for (int i = 0; i < NumberToRoll; i++)
                {
                    int roll = rand.Next(1, SizeOfDie + 1);
                    finalResult += roll;
                    Rolls.Add(roll.ToString());
                }
                Rolls.Add(")");
                return finalResult;
            }
            else if (StarIndex != -1) //Found a *
            {
                string particle1 = RollString.Substring(0, StarIndex);
                string particle2 = RollString.Substring(StarIndex + 1);
                if (particle1 == "" || particle2 == "")
                    throw new InvalidOperationException("Tried to times nothing?");
                double result1 = Roll(particle1, Rolls);
                double result2 = Roll(particle2, Rolls);
                return result1 * result2;
            }
            else if (SlashIndex != -1) //Found a /
            {
                string particle1 = RollString.Substring(0, SlashIndex);
                string particle2 = RollString.Substring(SlashIndex + 1);
                if (particle1 == "" || particle2 == "")
                    throw new InvalidOperationException("Tried to divide nothing?");
                double result1 = Roll(particle1, Rolls);
                double result2 = Roll(particle2, Rolls);
                if (result2 == 0)
                    throw new DivideByZeroException("Tried to divide by zero.");
                return result1 / result2;
            }
            else if (HatIndex != -1) //Found a ^
            {
                string particle1 = RollString.Substring(0, HatIndex);
                string particle2 = RollString.Substring(HatIndex + 1);
                if (particle1 == "" || particle2 == "")
                    throw new InvalidOperationException("Tried to put nothing to the power of something?");
                double result1 = Roll(particle1, Rolls);
                double result2 = Roll(particle2, Rolls);
                return Math.Pow(result1, result2);
            }
            else //Just a number. Parse it, return it, done.
            {
                double finalResult = double.Parse(RollString);
                return finalResult;
            }
            throw new InvalidOperationException("No idea what this is.");
        }

        /// <summary>
        /// Message variant of GetRoll - Rolls all dice mentioned and posts the result.
        /// </summary>
        /// <param name="_client">Client to post through.</param>
        /// <param name="e">Message events for the message sent</param>
        /// <param name="message">The message to try to compute.</param>
        async static void GetRoll(DiscordClient _client,MessageEventArgs e, string message)
        {
            while (true)
            {
                string currentRoll = "";
                double rollsValue = 0;
                List<string> DiceRolls = new List<string>();
                try
                {
                    int index1 = message.IndexOf("[[");
                    if (index1 == -1)
                        break;
                    int index2 = message.IndexOf("]]");
                    if (index2 == -1)
                        break;
                    currentRoll = message.Substring(index1 + 2, index2 - index1 - 2).ToLower();
                    message = message.Remove(0, index2 + 2);

                    try
                    {
                        rollsValue = Roll(currentRoll, DiceRolls);
                    }
                    catch (Exception ex)
                    {
                        await _client.SendMessage(e.Channel, $"Failed to roll: {ex.Message}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await _client.SendMessage(e.Channel, "Failed to roll");
                    return;
                }
                //await _client.SendMessage(e.Channel, Format.Italics($"Rolling for {e.User.Name}:") + $"`({currentRoll})` \n```{rollsValue} ({rolls}```");
                try
                {
                    string rolls = "";
                    foreach (string str in DiceRolls)
                    {
                        if (str == ")" || str == "(")
                            rolls += str + " ";
                        else
                            rolls += "[" + str + "] ";
                    }
                    if (rolls == "")
                    {
                        rolls = "No dice rolled.";
                    }
                    else
                    {
                        rolls = rolls.Remove(rolls.Length - 1);
                    }
                    await _client.SendMessage(e.Channel, $"```{rollsValue} ({rolls})```");
                }
                catch (Discord.HttpException ex)
                {
                    Console.WriteLine($"Could not post in channel roll was in for some reason. {e.User.Name}, {e.Channel.Name}, {e.Server.Name}");
                }
            }
        }

        /// <summary>
        /// Command variant of GetRoll - Rolls all dice mentioned and posts the result.
        /// </summary>
        /// <param name="_client">Client to post through.</param>
        /// <param name="e">Message events for the command</param>
        /// <param name="message">The message to try to compute.</param>
        async static void GetRoll(DiscordClient _client, CommandEventArgs e, string message)
        {
            while (true)
            {
                string currentRoll = "";
                double rollsValue = 0;
                List<string> DiceRolls = new List<string>();
                try
                {
                    //int index1 = message.IndexOf("[[");
                    //if (index1 == -1)
                    //    break;
                    //int index2 = message.IndexOf("]]");
                    //if (index2 == -1)
                    //    break;
                    //currentRoll = message.Substring(index1 + 2, index2 - index1 - 2).ToLower();
                    currentRoll = message;
                    //message = message.Remove(0, index2 + 2);

                    try
                    {
                        rollsValue = Roll(currentRoll, DiceRolls);
                    }
                    catch (Exception ex)
                    {
                        await _client.SendMessage(e.Channel, $"Failed to roll: {ex.Message}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await _client.SendMessage(e.Channel, "Failed to roll");
                    return;
                }
                ///await _client.SendMessage(e.Channel, Format.Italics($"Rolling for {e.User.Name}:") + $"`({currentRoll})` \n```{rollsValue} ({rolls}```");
                try
                {
                    string rolls = "";
                    foreach (string str in DiceRolls)
                    {
                        if (str == ")" || str == "(")
                            rolls += str + " ";
                        else
                            rolls += "[" + str + "] ";
                    }
                    if (rolls == "")
                    {
                        rolls = "No dice rolled.";
                    }
                    else
                    {
                        rolls = rolls.Remove(rolls.Length - 1);
                    }
                    await _client.SendMessage(e.Channel, $"```{rollsValue} ({rolls})```");
                    break;
                }
                catch (Discord.HttpException ex)
                {
                    Console.WriteLine($"Could not post in channel roll was in for some reason. {e.User.Name}, {e.Channel.Name}, {e.Server.Name}");
                }
            }
        }
        
        void InitializeDeck()
        {
            _defaultDeck.Clear();
            string[] values = { "Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King" };
            string[] suits = { "Hearts", "Clubs", "Spades", "Diamonds" };
            foreach (string s1 in values)
            {
                foreach (string s2 in suits)
                {
                    _defaultDeck.Add($"{s1} of {s2}");
                }
            }
            _defaultDeck.Add("Joker");
            _defaultDeck.Add("Joker");
        }

        void InitializeDeck(int Jokers, Channel channel)
        {
            EnsureDeckExists(channel);
            _cards[channel].Clear();
            string[] values = {"Ace","Two","Three","Four","Five","Six","Seven","Eight","Nine","Ten","Jack","Queen","King" };
            string[] suits = { "Hearts", "Clubs", "Spades", "Diamonds" };
            foreach(string s1 in values)
            {
                foreach(string s2 in suits)
                {
                    _cards[channel].Add($"{s1} of {s2}");
                }
            }
            for (int i = 0; i < Jokers; i++)
            {
                _cards[channel].Add("Joker");
            }
        }

        string DrawCard(Channel channel)
        {
            EnsureDeckExists(channel);
            if (_cards[channel].Any())
            {
                Random rand = new Random();
                int draw = rand.Next(1, _cards[channel].Count - 1);
                string card = _cards[channel][draw];
                _cards[channel].Remove(card);
                return card;
            }
            return "No cards left in the deck.";
        }

        void EnsureDeckExists(Channel channel)
        {
            if(_cards.ContainsKey(channel))
            {
                return;
            }
            else
            {
                _cards.Add(channel, _defaultDeck);
                InitializeDeck(2, channel);
            }
        }
    }
}
