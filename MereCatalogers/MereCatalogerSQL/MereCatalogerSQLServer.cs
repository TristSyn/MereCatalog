using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace MereCatalog
{
	/// <summary>
	/// A SQL Server specific MereCataloger.
	/// Adds the ability to return a ResultSet from a stored procedure.
	/// </summary>
	public class MereCatalogerSQLServer : MereCatalogerSQL {
		public string ConnectionStringName = "MereCatalogSQLServer_ConnectionString";

		public MereCatalogerSQLServer() { }
        public MereCatalogerSQLServer(string connectionStringName) : this() {
            ConnectionStringName = connectionStringName;
        }

        protected override IDbCommand CommandNew() { return new SqlCommand(); }

        protected override IDbConnection ConnectionNew() { return new SqlConnection(ConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString); }

        public override IDbDataParameter ParameterNew(string name, object value) { return new SqlParameter(name, value); }
        
		public ResultSet<T[]> LoadFromSP<T>(string sp, Type[] types, params object[] parameters) where T : class {

			Catalogable p = Catalogable.For(typeof(T));
            IDbCommand cmd = findallcmd(p, CommandType.StoredProcedure, ParameterList(parameters));
			cmd.CommandText = sp;
			return Load<T>(types, cmd, false);
		}
    }
}
