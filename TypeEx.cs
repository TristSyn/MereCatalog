using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MereCatalog {
	/// <summary>
	/// Contains a collection of extra information about the "associate" types.
	/// Helps identifying if it's a list or not.
	/// </summary>
	public class TypeEx {
		static Dictionary<Type, TypeEx> types = new Dictionary<Type, TypeEx>();

		public Type Type { get; private set; }
		public bool IsList { get; private set; }
		public bool IsArray { get; private set; }
		public bool IsListOrArray { get { return IsList || IsArray; } }
		public Type ElementType { get; private set; }

		public TypeEx(Type t) {
			Type = t;
			IsList = t.GetInterface("IList") != null;
			IsArray = t.IsArray;
			if (IsListOrArray)
				ElementType = IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
			else
				ElementType = t;
		}

		public static TypeEx ForType(Type t) {
			if (!types.ContainsKey(t))
				types.Add(t, new TypeEx(t));
			return types[t];
		}

		public static TypeEx For(object o) =>  ForType(o.GetType());
	}

	public static class PropertyInfoExtensions {
		public static TypeEx TypeEx(this PropertyInfo pi) { return MereCatalog.TypeEx.ForType(pi.PropertyType); }
	}
}
