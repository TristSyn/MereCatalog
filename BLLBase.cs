using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MereCatalog {

	/// <summary>
	/// A base class for a very simple Business Logic Layer
	/// </summary>
	/// <typeparam name="T"></typeparam>
    public abstract class BLLBase<T> where T : class {

		protected static MereCataloger MereCataloger { get { return MereCataloger.Instance; } }

		public static T ByID(long id, bool initialLoad = true, bool recursiveLoad = false) {
			return MereCataloger.FindByID<T>(initialLoad, recursiveLoad, id);
        }

        public static void Save(T target) {
			MereCataloger.Save(target);
        }

        public static void Delete(T target) {
			MereCataloger.Delete(target);
        }
    }

	/// <summary>
	/// A base class for a very simple SQL Server Business Logic Layer
	/// </summary>
	/// <typeparam name="T"></typeparam>
    public abstract class BLLSQLServer<T> : BLLBase<T> where T : class {

		private static MereCatalogerSQLServer mereCataloger;
		protected new static MereCatalogerSQLServer MereCataloger {
            get {
				if (mereCataloger == null) {
					if (!(MereCatalog.MereCataloger.Instance is MereCatalogerSQLServer))
                        throw new Exception("Must be a SQL Server MereCataloger as this calls a Stored Procedure");
					mereCataloger = (MereCatalogerSQLServer)MereCatalog.MereCataloger.Instance;
                }
				return mereCataloger;
            }
        }
    }
}
