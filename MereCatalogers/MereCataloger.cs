using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace MereCatalog
{
	/// <summary>
	/// MereCatalog is a base class for representing a data store, e.g. a SQL database.
	/// </summary>
	public abstract class MereCataloger	{

		private static MereCataloger instance;
		public static MereCataloger Instance {
			get {
				if (instance == null)
					instance = new MereCatalogerSQLServer();
				return instance;
			}
			set { instance = value; }
		}

		#region CRUD

        public T[] Find<T>(params object[] parameters) where T : class { return Find<T>(true, false, parameters); }
        public T FindByID<T>(object id) where T : class { return FindByID<T>(true, false, id); }

        public abstract T[] Find<T>(bool initialLoad, bool recursiveLoad, params object[] parameters) where T : class;
        public virtual T FindByID<T>(bool initialLoad, bool recursiveLoad, object id) where T : class {
			Catalogable p = Catalogable.For(typeof(T));
            T[] results = Find<T>(initialLoad, recursiveLoad, p.IDProperty.Name, id);
			if (results == null)
				return null;
			return results.Length == 1 ? results[0] : results.FirstOrDefault(obj => p.ID(obj).Equals(id));
        }

		public void Save(object target) {
			Catalogable schema = Catalogable.For(target);
			object idval = Convert.ChangeType(schema.ID(target), schema.IDType);
			string idvalStr = idval.ToString();
			bool isNew = idvalStr == "" || idvalStr == "0";
			Save(target, isNew);
		}

		public abstract void Save(object target, bool isNew);

		public abstract void Delete(object target);


        #endregion CRUD

		protected static void InstantiateProperties(Catalogable t, object item, Func<string, object> valueExtractor) {
            foreach (PropertyInfo p in t.Columns) {
                object fieldValue = valueExtractor(p.Name);
                if (Convert.IsDBNull(fieldValue)) // data in database is null, so do not set the value of the property
                    continue;
				bool rightType = (p.PropertyType == fieldValue.GetType()) || (Nullable.GetUnderlyingType(p.PropertyType) == fieldValue.GetType());
				p.SetValue(item, rightType ? fieldValue : Convert.ChangeType(fieldValue, p.PropertyType), null);
            }
        }

        private Dictionary<Type, MethodInfo> FindByIDMethods = new Dictionary<Type,MethodInfo>();
        public MethodInfo _FindByIDMethod(Type t) {
            if(!FindByIDMethods.ContainsKey(t))
                FindByIDMethods.Add(t, this.GetType().GetMethod("FindByID", new Type[] { typeof(bool), typeof(bool), typeof(object) }).MakeGenericMethod(t));
            return FindByIDMethods[t];
        }

        private Dictionary<Type, MethodInfo> FindMethods = new Dictionary<Type, MethodInfo>();
        public MethodInfo _FindMethod(Type t) {
            if (!FindMethods.ContainsKey(t))
                FindMethods.Add(t, this.GetType().GetMethod("Find", new Type[] { typeof(bool), typeof(bool), typeof(object[]) }).MakeGenericMethod(t));
            return FindMethods[t];
        }
	}
}
