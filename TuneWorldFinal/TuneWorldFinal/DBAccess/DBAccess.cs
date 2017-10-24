using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace TuneWorldFinal.DBAccess
{
    public static class DBAccess
    {
        static SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);

        public static bool InsertUpdateDeleteIntoDB(string cmdstr)
        {
            try
            {
                SqlCommand cmd = new SqlCommand(cmdstr, con);
                con.Open();
                cmd.ExecuteNonQuery();
                con.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}