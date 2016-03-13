
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Sql;
using System.Data.SqlClient;
using System.IO;

namespace DataBaseUtilities
{
	class DBDev
	{
		static Boolean gDoDeltas = true;
		static Boolean gDoSPs = true;
		static Boolean gDoWait = true;
		static string gConnectionString = null;
		static string gDBName = null;
		static string gProjectDir = null;
		static string gDeltasTableName = "_Delta";
		static string gDBDir = "DB";
		static string gDeltaDir = "Delta";
		static string gProcDir = "Proc";
		static SqlConnection gConnection = null;

		// TODO: Currently this utility is targeted for Microsoft SQL Server. Other servers 
		// should be included with a server=MSSQL as default.  (MySQL to start with).

		static void Main(string[] args)
		{
			Console.WriteLine("DBDev: Data Base Development utility.");
			if (ParseArgs(args))
			{
				try
				{
					VerifyDirs();
					using (gConnection = new SqlConnection(gConnectionString))
					{
						gConnection.Open();
						CreateDB();
						ProcessDeltas();
						UpdateSPs();
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Exception in Main: " + ex.Message);
				}
			}
			if (gDoWait)
			{
				Console.WriteLine("Press any key to exit.");
				Console.ReadKey(true);
			}
		}

		static void ProcessDeltas()
		{
			if (gDoDeltas)
			{
				DeltaTableInit();

				List<string> scriptsRun = DeltaTableGetAll();
				List<string> fileNames = Directory.GetFiles(Path.Combine(gProjectDir, gDBDir, gDeltaDir), "*.sql").OrderBy(e => e).ToList();
				foreach (string fName in fileNames)
				{
					if (!scriptsRun.Contains(fName))
					{
						if (RunScript(fName))
						{
							DeltaTableAdd(fName);
						}
					}
				}
			}
		}

		static void UpdateSPs()
		{
			if (gDoSPs)
			{
				List<string> fileNames = Directory.GetFiles(Path.Combine(gProjectDir, gDBDir, gProcDir), "*.sql").OrderBy(e => e).ToList();
				foreach (string fName in fileNames)
				{
					RunScript(fName);
				}
			}
		}

		/// <summary>
		/// This is a very usfull all-purpose method to run a script file as an SQL command.
		/// </summary>
		/// <param name="fName">Full path to the script file (e.g. c:\MyProject\DB\Proc\GetRec.sql)</param>
		/// <returns>success/failure</returns>
		static bool RunScript(string fName)
		{
			try
			{
				Console.WriteLine(string.Format("Running SQL script: {0}.", fName));

				string fileContents = File.ReadAllText(fName);

				// Implement GO syntax al'la SQL Management Studio; break the script into
				// multiple segments and run them individually.
				string[] segments = fileContents.Split(new string[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string segment in segments)
				{
					SqlCommand cmd = new SqlCommand(segment, gConnection);
					cmd.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception in RunScript: " + ex.Message);
				return false;
			}
			return true;
		}

		static void DeltaTableInit()
		{
			try
			{
				// Create the table if it doesn't exist.
				string sql = @"
					IF OBJECT_ID('{0}', 'U') IS NULL
					BEGIN 
						CREATE TABLE {0} ([Name] NVARCHAR(1024));
					END;";
                SqlCommand cmd = new SqlCommand(string.Format(sql, gDeltasTableName), gConnection);
				cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception in DeltaTableInit: " + ex.Message);
			}
		}

		static List<string> DeltaTableGetAll()
		{
			List<string> deltas = new List<string>();
			string sql = @"
				SELECT * FROM {0};";

			SqlCommand cmd = new SqlCommand(string.Format(sql, gDeltasTableName), gConnection);
			SqlDataReader reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				deltas.Add(reader[0].ToString());
			}
			reader.Close();
			return deltas;
		}

		static void DeltaTableAdd(string fName)
		{
			try
			{
				string sql = @"
					INSERT INTO [dbo].[{0}] 
						([Name])
					VALUES 
						('{1}')";
				SqlCommand cmd = new SqlCommand(string.Format(sql, gDeltasTableName, fName), gConnection);
				cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception in DeltaTableAdd: " + ex.Message);
			}
		}

		static Boolean ParseArgs(string[] args)
		{
			string errMsg = @"
USAGE: DBDev <connection-string> <dbName> <ProjectDir> 

Processes SQL script files in <ProjectDir><DBDir><DeltasDir> and <ProjectDir><DBDir><ProcsDir>:

OPTIONS:
[NoWait]                Don't wait for keystroke after run is finished.
[DeltaTable=_Delta]     Name to store the delta scripts that have been run already.
|DBDir=DB]              The sub-dir under the ProjectDir where the scripts persist.
[DeltaDir=Delta]        The sub-dir under the DBDir where the delta scripts are stored.
[ProcDir=Proc]          The sub-dir under the DBDir where the stored procedure scripts are stored.
[DeltasOnly|ProcsOnly]  Either process just the deltas or just the stored procedure scripts.
";

			if (args.Length < 3)
			{
				Console.Write(errMsg);
				return false;
			}
			else
			{

				// Dump the input params.
				foreach (string s in args)
				{
					Console.WriteLine(string.Format("ARG:{0}", s));
				}
				Console.WriteLine("");

				gConnectionString = args[0];
				gDBName = args[1];
				gProjectDir = args[2];
				gDoWait = !args.Contains("nowait");
				gDoDeltas = !args.Contains("ProcsOnly");
				gDoSPs = !args.Contains("DeltasOnly");
				OverrideValue(ref gDBDir, "DBDir", args);
				OverrideValue(ref gProcDir, "ProcDir", args);
				OverrideValue(ref gDeltaDir, "DeltaDir", args);
				OverrideValue(ref gDeltasTableName, "DeltaTable", args);
			}
			return true;
		}

		static void OverrideValue(ref string gRef, string key, string[] args)
		{
			foreach (string arg in args)
			{
				if (arg.StartsWith(key + "=", StringComparison.CurrentCultureIgnoreCase))
				{
					gRef = arg.Substring(arg.IndexOf("=") + 1);
				}
			}
		}

		static void CreateDB()
		{
			try
			{

				// Create it if it doesn't exist.
				string sql = @"
					IF db_id('{0}') IS NULL
					BEGIN
						USE master;
						CREATE DATABASE {0}; 
					END";

                SqlCommand cmd = new SqlCommand(string.Format(sql, gDBName), gConnection);
				cmd.ExecuteNonQuery();

				// All subsequent commands will apply to the selected DB.
				sql = "USE {0};";
				cmd.CommandText = string.Format(sql, gDBName);
				cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception in CreateDB:" + ex.Message);
			}
		}

		static void VerifyDirs()
		{
			string dir;
			if(!Directory.Exists(gProjectDir))
			{
				throw new Exception("Project directory [" + gProjectDir + "] does not exist.");
			}
			dir = Path.Combine(gProjectDir, gDBDir);
            if (!Directory.Exists(dir))
            {
				throw new Exception("Database root directory [" + dir + "] does not exist.");
			}
			dir = Path.Combine(gProjectDir, gDBDir, gDeltaDir);
            if (!Directory.Exists(dir))
            {
				throw new Exception("Deltas directory [" + dir + "] does not exist.");
			}
			dir = Path.Combine(gProjectDir, gDBDir, gProcDir);
			if (!Directory.Exists(Path.Combine(gProjectDir, gDBDir, gProcDir)))
            {
				throw new Exception("Procedure directory [" + dir + "] does not exist.");
			}
        }
	}
}

