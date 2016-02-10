using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Configuration;

namespace Discord_RPBot.Data_Access
{
    class SQLConnection
    {
        public static DbConnection connection = GetOpenConnection();
        public static DbConnection GetOpenConnection()
        {
            string RPDB = ConfigurationManager.ConnectionStrings["RPDB"].ConnectionString;
            var connection = new SqlConnection(RPDB);
            connection.Open();
            return connection;
        }
    }
}
