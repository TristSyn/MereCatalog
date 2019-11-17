using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.Serialization;

namespace MereCatalog
{
	[AttributeUsage(AttributeTargets.Class)]
	public class DBTableAttribute : Attribute
    {
        public string TableName;
        public bool Cached;

		/// <summary>
		/// A string indicating how objects in this table are referenced by other objects. In SQL world, this indicates what other tables would as a foreign key to refer to this table.
		/// </summary>
		public string Reference;
    }

	[AttributeUsage(AttributeTargets.Property)]
	public class DBPropertyAttribute : Attribute
	{
		public bool IDField;
		public bool Exclude;
        public string KeyID;
	}
}