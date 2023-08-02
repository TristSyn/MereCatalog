using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MereCatalog {

	/// <summary>
	/// A base class for a very simple Business Logic Layer
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class MereCatalogerBLL<C, idType> where C : class {

		protected static MereCataloger MereCataloger => MereCataloger.Instance;

		public static C ByID(idType id, bool eagerLoad = true) {
			return MereCataloger.FindByID<C>(eagerLoad, id);
        }

		public static IEnumerable<C> All(bool eagerLoad = true) {
			return MereCataloger.Find<C>(eagerLoad);
		}

        public static void Save(C target) {
			MereCataloger.Save(target);
        }

        public static void Delete(C target) {
			MereCataloger.Delete(target);
        }
    }
	public abstract class MereCatalogerBLLSQLServer<C, idType> : MereCatalogerBLL<C, idType> where C : class
	{
		private static MereCatalogerSQLServer mereCataloger;
		protected new static MereCatalogerSQLServer MereCataloger {
			get {
				if (mereCataloger == null)
				{
					if (!(MereCatalog.MereCataloger.Instance is MereCatalogerSQLServer))
						throw new Exception("Must be a SQL Server MereCataloger as this calls a Stored Procedure");
					mereCataloger = (MereCatalogerSQLServer)MereCatalog.MereCataloger.Instance;
				}
				return mereCataloger;
			}
		}
	}
}
