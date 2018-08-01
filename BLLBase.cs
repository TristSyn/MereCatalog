using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MereCatalog {

	/// <summary>
	/// A base class for a very simple Business Logic Layer
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class BLLBase<C, idType> where C : class {

		protected static MereCataloger MereCataloger { get { return MereCataloger.Instance; } }

		public static C ByID(idType id, bool initialLoad = true, bool recursiveLoad = false) {
			return MereCataloger.FindByID<C>(initialLoad, recursiveLoad, id);
        }

        public static void Save(C target) {
			MereCataloger.Save(target);
        }

        public static void Delete(C target) {
			MereCataloger.Delete(target);
        }
    }

	/// <summary>
	/// A base class for a very simple SQL Server Business Logic Layer
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class BLLSQLServer<C, idType> : BLLBase<C, idType> where C : class {

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
