using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace MereCatalog
{
	/// <summary>
	/// Used to store reflected information for the classes to be persisted to storage
	/// This is used so that it is cached as opposed to being reflected each time it's required. Reflecting has a measurable performance hit.
	/// </summary>
	public class Catalogable {
		private static readonly Type[] AllowedNonPrimitives = new Type[] { typeof(string), typeof(DateTime), typeof(decimal) };
		protected Catalogable(Type targetType) { Type = targetType; }

		protected Catalogable(object target) { Type = target.GetType(); }

		private static Dictionary<Type, Catalogable> Schemas = new Dictionary<Type, Catalogable>();
		public static Catalogable For(object item) { return For(item.GetType()); }
		public static Catalogable For(Type type) {
			if (Schemas.ContainsKey(type))
				return Schemas[type];
			lock (Schemas) {
				Schemas.Add(type, new Catalogable(type));
			}
			return Schemas[type];
		}

		public Type Type;

        //Cache it?
        public bool HasPropertyAttribute(PropertyInfo pi) { return Attribute.IsDefined(pi, typeof(DBPropertyAttribute)); }
        public DBPropertyAttribute PropertyAttribute(PropertyInfo pi) { return ((DBPropertyAttribute)Attribute.GetCustomAttribute(pi, typeof(DBPropertyAttribute))); }


		private IEnumerable<PropertyInfo> all;
        private IEnumerable<PropertyInfo> All {
            get {
                if (all == null)
                    all = Type.GetProperties();
                return all;
            }
        }

		/// <summary>
		/// Returns all properties that are primitive or otherwise allowed (string, datetime, decimal) and writeable (i.e. public set)
		/// </summary>
        private IEnumerable<PropertyInfo> columns;
		public IEnumerable<PropertyInfo> Columns {
			get {
				if (columns == null)
                    columns = 
                        All.Where(p =>
								(p.PropertyType.IsPrimitive || AllowedNonPrimitives.Contains(p.PropertyType) || IsAllowedNullable(p.PropertyType))
								&& p.CanWrite
                                //&& (!HasPropertyAttribute(p) || !PropertyAttribute(p).Exclude)
								&& !(HasPropertyAttribute(p) && PropertyAttribute(p).Exclude)
                                
                            );
                return columns;
			}
        }

		private bool IsAllowedNullable(Type t) {
			Type nt = Nullable.GetUnderlyingType(t);
			return nt != null && (nt.IsPrimitive || AllowedNonPrimitives.Contains(nt));
		}

        protected PropertyInfo ColumnByName(string name) { return Columns.FirstOrDefault(c => c.Name == name); }
		protected PropertyInfo PropertyByName(string name) { return All.FirstOrDefault(f => f.Name == name); }

        public object ColumnValue(object target, string name) {
            return ColumnByName(name)?.GetValue(target, null); 
        }

		/// <summary>
		/// Returns all "associated" properties, so ones that should be other Catalogable classes
		/// </summary>
        private IEnumerable<PropertyInfo> associated;
        public IEnumerable<PropertyInfo> Associated {
            get
            {
				if (associated == null)
					associated =
						All.Where(p =>
							!p.PropertyType.IsPrimitive
							&& !IsAllowedNullable(p.PropertyType) //nullable primitives
							//&& !(p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(p.PropertyType).IsPrimitive) 
							&& !AllowedNonPrimitives.Contains(p.PropertyType)
							&& !(HasPropertyAttribute(p) && PropertyAttribute(p).Exclude)
							//&& (!HasPropertyAttribute(p) || !PropertyAttribute(p).Exclude)
                        );
                return associated;
            }
        }
		
		private PropertyInfo idProperty;
		public PropertyInfo IDProperty {
			get {
				if (idProperty == null) {
					idProperty = 
						All.FirstOrDefault(p => HasPropertyAttribute(p) && PropertyAttribute(p).IDField)
						?? PropertyByName(Type.Name + "ID")
						?? PropertyByName("ID");
				}
				return idProperty;
			}
		}

		public string Reference {
			get { return TableAttribute.Reference ?? Type.Name + "ID"; }
		}

        public object ID(object target) { return IDProperty.GetValue(target, null); }
        public Type IDType { get { return IDProperty.PropertyType; } }

		public string TableName { get { return TableAttribute.TableName ?? Type.Name; } }
        public bool Cached { get { return TableAttribute.Cached; } }
		public object[] Cache { get; set; }

		private DBTableAttribute _TableAttribute;
		private DBTableAttribute TableAttribute {
			get {
				if (_TableAttribute == null) {
					_TableAttribute = (DBTableAttribute)Attribute.GetCustomAttribute(Type, typeof(DBTableAttribute))
						?? new DBTableAttribute() { TableName = Type.Name };
				}
				return _TableAttribute;
			}
		}

		public override string ToString() => TableName;
	}
}
