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
    internal class SimpleCommands : IModule
    {
        private ModuleManager _manager;
        private DiscordClient _client;

        void IModule.Install(ModuleManager manager)
        {
            _manager = manager;
            _client = manager.Client;

            manager.CreateCommands("", group =>
            {
                group.CreateCommand("whoami")
                .Description("Returns the user's ID")
                .Do(async e =>
                {
                    await WhoIs(e, e.User);
                });

                group.CreateCommand("whereami")
                .Description("Returns the channel and server's ID")
                .Do(async e =>
                {
                    await _client.SendMessage(e.Channel, $"You are currently in: {e.Server.Id}/{e.Channel.Id}");
                });

                group.CreateCommand("whois")
                .Description("Returns the user's ID")
                .Parameter("The user's Name")
                .Do(async e =>
                {
                    List<User> user = e.Channel.Members.Where(m => m.Name == e.Args[0]).ToList();
                    if (user.Any())
                        await WhoIs(e, user.First());

                });

                group.CreateCommand("join")
                .Description("Requests the bot to join a channel.")
                .Parameter("Instant Invite")
                .Do(async e =>
                {
                    var invite = await _client.GetInvite(e.Args[0]);
                    if (invite == null)
                    {
                        await _client.SendMessage(e.Channel, $"Invite not found.");
                        return;
                    }
                    else if (invite.IsRevoked)
                    {
                        await _client.SendMessage(e.Channel, $"The invite has expired or the bot is banned.");
                        return;
                    }

                    await _client.AcceptInvite(invite);
                    await _client.SendMessage(e.Channel, $"Joined the server.");
                });

                group.CreateCommand("leave")
                .Description("Requests the bot to get the fuck out.")
                .MinPermissions((int)PermissionLevel.ServerMod)
                .Do(async e =>
                {
                    await _client.SendMessage(e.Channel, $"Leaving channel.");
                    await _client.LeaveServer(e.Server);
                });

                group.CreateCommand("Clean up")
                .Description("Requests the bot to purge all his messages since the last reboot in the channel.")
                .MinPermissions((int)PermissionLevel.ServerAdmin)
                .Do(async e =>
                {
                    List<Message> myMessages = e.Channel.Messages.Where(m => m.User.Id == _client.CurrentUser.Id).ToList();
                    await _client.DeleteMessages(myMessages);
                });

                group.CreateCommand("Undo")
                .Description("Requests the bot to remove his last message.")
                .Do(async e =>
                {
                    Message lastMessage = e.Channel.Messages.Where(m => m.User.Id == _client.CurrentUser.Id).OrderByDescending(m => m.Timestamp).First();
                    await _client.DeleteMessage(lastMessage);
                });
            });
        }

        private async Task WhoIs(CommandEventArgs e, User user)
        {
            if (user != null)
            {
                await _client.SendPrivateMessage(e.User, $"{user.Name}'s ID is {user.Id}");
            }
        }
    }
}
