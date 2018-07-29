using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Text;


namespace MereCatalog.Implementations.TristoSQL
{

    public class MereCatalogerSQLite : PersistorSQLBase {
		private static readonly string CONNECTIONSTRING = ConfigurationManager.ConnectionStrings["MereCatalogSQLLite_ConnectionString"].ConnectionString;
        private string FileLocation;

		public MereCatalogerSQLite(string fileLocation)
        {
            FileLocation = fileLocation;    
        }

        protected override IDbCommand CommandNew()
        {
            return new SQLiteCommand();
        }

        protected override IDbConnection ConnectionNew()
        {
            return new SQLiteConnection(CONNECTIONSTRING.Replace("{FileLocation}", FileLocation));
        }

        protected override IDbDataParameter ParameterNew(string name, object value)
        {
            return new SQLiteParameter(name, value);
        }
    }
}
