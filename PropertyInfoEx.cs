using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MereCatalog {
	/// <summary>
	/// Contains a collection of extra information about the "associate" types.
	/// Helps identifying if it's a list or not.
	/// </summary>
    sealed class PropertyInfoEx {
        static Dictionary<Type, PropertyInfoEx> types = new Dictionary<Type, PropertyInfoEx>();
        public Type Type { get; private set; }
        public bool IsList { get; private set; }
        public bool IsArray { get; private set; }
        public bool IsListOrArray { get { return IsList || IsArray; } }
        public Type ElementType { get; private set; }

		private PropertyInfoEx(Type t) {
            Type = t;
            IsList = t.GetInterface("IList") != null;
            IsArray = t.IsArray;
            if (IsListOrArray)
                ElementType = IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
            else
                ElementType = t;
        }

		public static PropertyInfoEx ForType(Type t) {
            if (!types.ContainsKey(t))
				types.Add(t, new PropertyInfoEx(t));
            return types[t];
        }

		public static PropertyInfoEx For(object o) {
            Type t = o.GetType();
            if (!types.ContainsKey(t))
				types.Add(t, new PropertyInfoEx(t));
            return types[t];
        }
    }
}
