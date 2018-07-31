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
    }

	[AttributeUsage(AttributeTargets.Property)]
	public class DBPropertyAttribute : Attribute
	{
		public bool IDField;
		public bool Exclude;
        public string KeyID;
	}
}