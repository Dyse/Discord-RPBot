using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace Discord_RPBot
{
    class SettingsManager
    {
        public static string Email;
        public static string Password;
        public static string OwnerID;
        public static void Load()
        {
            Email = ConfigurationManager.AppSettings["BotUsername"];
            Password = ConfigurationManager.AppSettings["BotPassword"];
            OwnerID = ConfigurationManager.AppSettings["BotOwnerID"];
        }
    }
}
