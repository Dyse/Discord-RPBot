using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_RPBot
{
        public enum PermissionLevel
    {
        User = 0,
        ChannelMod,
        ChannelAdmin,
        ServerMod,
        ServerAdmin,
        ServerOwner,
        BotOwner
    }
}
