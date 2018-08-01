using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace MereCatalog
{
	/// <summary>
	/// ResultSet is used to store the results of queries as they come in. 
	/// Each result is added to a queue and then processed for the purpose of wiring up all the 
	/// "associated" properties specified through Catalogable, e.g. child or parent data relationships
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ResultSet<T>
	{
		//Dictionary of type names, against a dictionary of ids against value
		public Dictionary<string, Dictionary<object, object>> results = new Dictionary<string, Dictionary<object, object>>();

		private Queue<object> queue = new Queue<object>();

        public T Result { get; private set; }
        public ResultSet(T value) : this() {
            Result = value;
            Add(value);
        }

        public ResultSet() { }

        public void Add(object value)
        {
			if (value == null)
				return;
			PropertyInfoEx tEx = PropertyInfoEx.For(value);
			Catalogable pt = Catalogable.For(tEx.ElementType);
            if (tEx.IsListOrArray) {
                foreach (object obj in (IList)value)
                    Add(tEx.ElementType.FullName, pt.ID(obj), obj);
            } else
                Add(tEx.Type.FullName, pt.ID(value), value);
        }

		private void Add(string typename, object key, object value) {
            if (!results.ContainsKey(typename))
                results.Add(typename, new Dictionary<object, object>());
            Dictionary<object, object> savedlist = results[typename];
			if (!savedlist.ContainsKey(key)) {
				savedlist.Add(key, value);
				queue.Enqueue(value);
			}
		}

		public void ProcessQueue(MereCataloger p, bool recursiveLoad) {
			//once the raw results have been processed, connect all the related objects
			while (queue.Count > 0)
				InstantiateAssociated(p, queue.Dequeue(), recursiveLoad);
		}

		/// <summary>
		/// Go through an item's "associated" properties and attempt to find in this ResultSet. Potentially load from the MereCataloger if missing
		/// </summary>
		/// <param name="p">The MereCataloger to attempt to load from if required</param>
		/// <param name="item">the item that is being parsed and "associated"</param>
		/// <param name="recursiveLoad">Dictates whether to manually attempt to load missing results expected during wiring up the "associated properties</param>
		private void InstantiateAssociated(MereCataloger p, object item, bool recursiveLoad) {
			Catalogable t = Catalogable.For(item);
            object itemID = t.ID(item);

            foreach (PropertyInfo property in t.Associated) {
				PropertyInfoEx tEx = PropertyInfoEx.ForType(property.PropertyType);
                if (property.GetValue(item, null) != null)
                    continue;
				Catalogable pt = Catalogable.For(tEx.ElementType);
                if (tEx.IsListOrArray) {
                    object[] result = null;
					string KeyID = t.HasPropertyAttribute(property) ? t.PropertyAttribute(property).KeyID : t.Type.Name + "ID";

                    //if in the results then use, else load in using Find method on the relatedBusinessObject
                    if (results.ContainsKey(tEx.ElementType.FullName))
                        result = results[tEx.ElementType.FullName]
                            .Select(obj => Convert.ChangeType(obj.Value, tEx.ElementType))
                            .Where(obj => pt.ColumnValue(obj, KeyID).Equals(itemID)).ToArray();
                    
                    //if none found but recursiveLoad is allowed, manually get the results
                    if ((result == null || ((IList)result).Count == 0) && recursiveLoad) {
						result = (object[])p._FindMethod(tEx.ElementType).Invoke(p, new object[] { false, false, new object[] { KeyID, itemID } });
						Add(result);
                    }

                    if (result != null) {
                        if (tEx.IsArray) {
                            Array array = Array.CreateInstance(tEx.ElementType, result.Count());
                            Array.Copy(result, array, result.Length);
                            property.SetValue(item, array, null);
                        } else {
                            IList list = (IList)Activator.CreateInstance(tEx.Type);
                            foreach (object o in (object[])result)
                                list.Add(o);
                            property.SetValue(item, list, null);
                        }
                    }
                } else { //singular objects
                    object result = null;
					if (pt.IDProperty == null) //possibly because it's not been "excluded"
						continue;
                    string KeyID = t.HasPropertyAttribute(property) ? t.PropertyAttribute(property).KeyID : tEx.Type.Name + "ID";
                    object id = t.ColumnValue(item, KeyID);
                    //if in the results then use, else load in using Find method on the relatedBusinessObject
                    if (results.ContainsKey(tEx.Type.FullName) && results[tEx.Type.FullName].ContainsKey(id))
                        result = results[tEx.Type.FullName][id];
                    else if (recursiveLoad) { //if none found but recursiveLoad is allowed, manually get the results
                        result = p._FindByIDMethod(tEx.Type).Invoke(p, new object[] { false, false, id });
						Add(result);
                    }
                    if (result != null)
                        property.SetValue(item, result, null);
                }
            }
        }
	}
}