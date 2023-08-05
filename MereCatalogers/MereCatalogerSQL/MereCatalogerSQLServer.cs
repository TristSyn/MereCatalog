using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

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

		protected override IDbCommand CommandNew(string cmdText = "") { return new SqlCommand(cmdText); }

		protected override IDbConnection ConnectionNew() { return new SqlConnection(ConnectionString); }

		public override IDbDataParameter ParameterNew(string name, object value) { return new SqlParameter(name, value); }

		public ResultSet<T[]> LoadFromSP<T>(string sp, List<Type> types, bool eagerLoad, params object[] parameters) where T : class {

			Catalogable p = Catalogable.For(typeof(T));
			IDbCommand cmd = CommandNew();
			cmd.CommandType = CommandType.StoredProcedure;
			if (parameters != null && parameters.Length > 0)
			{
				var parameterList = ParameterList(parameters);
				for (int i = 0; i < parameterList.Length; i++)
					cmd.Parameters.Add(ParameterNew(parameterList[i].ParameterName, parameterList[i].Value));
			}
			cmd.CommandText = sp;
			return Load<T>(types, cmd, eagerLoad);
		}
	}
}
