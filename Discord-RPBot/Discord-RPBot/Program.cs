using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Commands.Permissions.Userlist;
using Discord.Modules;

namespace Discord_RPBot
{
    class Program
    {
        private static DiscordClient _client;
        
        private static void Main(string[] args)
        {
            Start();
        }

        private static void Start()
        {
            SettingsManager.Load();
            _client = new DiscordClient();
            _client.LogMessage += (s, e) =>
            {
                Console.WriteLine($"[{e.Severity}] {e.Source} => {e.Message}");
            };

            //Set up permissions
            _client.AddService(new BlacklistService());
            _client.AddService(new WhitelistService());
            _client.AddService(new PermissionLevelService((u, c) =>
            {
                if (u.Id == long.Parse(SettingsManager.OwnerID))
                    return (int)PermissionLevel.BotOwner;
                if (!u.IsPrivate)
                {
                    if (u == c.Server.Owner)
                        return (int)PermissionLevel.ServerOwner;

                    var serverPerms = u.GetServerPermissions();
                    if (serverPerms.ManageRoles)
                        return (int)PermissionLevel.ServerAdmin;
                    if (serverPerms.ManageMessages && serverPerms.KickMembers && serverPerms.BanMembers)
                        return (int)PermissionLevel.ServerMod;

                    var channelPerms = u.GetPermissions(c);
                    if (channelPerms.ManagePermissions)
                        return (int)PermissionLevel.ChannelAdmin;
                    if (channelPerms.ManageMessages)
                        return (int)PermissionLevel.ChannelMod;
                }
                return (int)PermissionLevel.User;
            }));

            //Set up commands
            var commands = _client.AddService(new CommandService(new CommandServiceConfig
            {
                CommandChar = '!',
                HelpMode = HelpMode.Private
            }));
            commands.RanCommand += (s, e) => Console.WriteLine($"[Command] {(e.Server == null ? "[Private]" : e.Server.ToString())  + "/" + e.Channel} => {e.Message}");
            commands.CommandError += (s, e) =>
            {
                string msg = e.Exception?.GetBaseException().Message;
                if (msg == null)
                {
                    {
                        switch (e.ErrorType)
                        {
                            case CommandErrorType.Exception:
                                //msg = "Unknown error.";
                                break;
                            case CommandErrorType.BadPermissions:
                                msg = "You do not have permission to run this command.";
                                break;
                            case CommandErrorType.BadArgCount:
                                //msg = "You provided the incorrect number of arguments for this command.";
                                break;
                            case CommandErrorType.InvalidInput:
                                //msg = "Unable to parse your command, please check your input.";
                                break;
                            case CommandErrorType.UnknownCommand:
                                //msg = "Unknown command.";
                                break;
                        }
                    }
                }
                if (msg != null)
                {
                    _client.SendMessage(e.Channel, $"Failed to complete command: {msg}");
                    Console.WriteLine($"[Error] Failed to complete command: {e.Command.Text} for {e.User.Name}");
                }
                Console.WriteLine("Command failure");
            };
            //Set up modules
            var modules = _client.AddService(new ModuleService());
            modules.Install(new Modules.SimpleCommands(), "Simple Commands", FilterType.Unrestricted);
            modules.Install(new Modules.Chance(), "Dice Rolling", FilterType.Unrestricted);



            //Boot up
            _client.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await _client.Connect(SettingsManager.Email, SettingsManager.Password);
                        if (!_client.AllServers.Any())
                            await _client.AcceptInvite(_client.GetInvite("0nwaapOqh2LPqDL9").Result);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Login failed" + ex.ToString());
                    }

                }
            });
        }
    }
}
