using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace MereCatalog {
	/// <summary>
	/// A SQL Server specific MereCataloger.
	/// Adds the ability to return a ResultSet from a stored procedure.
	/// </summary>
	public class MereCatalogerSQLServer : MereCatalogerSQL {
		//public string ConnectionStringName { get; private set; }
		public static readonly string CONNECTION_STRING_NAME = "MereCatalogSQLServer_ConnectionString";
		public string ConnectionString { get; private set; }

		public MereCatalogerSQLServer(string connectionString)
		{
			ConnectionString = connectionString;
		}

		protected override IDbCommand CommandNew() { return new SqlCommand(); }

		protected override IDbConnection ConnectionNew() { return new SqlConnection(ConnectionString); }

		public override IDbDataParameter ParameterNew(string name, object value) { return new SqlParameter(name, value); }

		public ResultSet<T[]> LoadFromSP<T>(string sp, Type[] types, bool recursiveLoad, params object[] parameters) where T : class {

			Catalogable p = Catalogable.For(typeof(T));
			IDbCommand cmd = findallcmd(p, CommandType.StoredProcedure, ParameterList(parameters));
			cmd.CommandText = sp;
			return Load<T>(types, cmd, recursiveLoad);
		}
	}
}
