using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Collections;

namespace MereCatalog {
	/// <summary>
	/// A base class for a SQL based MereCataloger.  May not be completely SQL variant agnostic.
	/// </summary>
	public abstract class MereCatalogerSQL : MereCataloger {

		#region CRUD

		public override T[] Find<T>(bool initialLoad, bool recursiveLoad, params object[] parameters) {
			Catalogable p = Catalogable.For(typeof(T));
			IDbDataParameter[] pl = ParameterList(parameters);
			IDbCommand cmd = findallcmd(p, CommandType.Text, pl);
			ResultSet<T[]> result;
			if (initialLoad) {
				Type[] types = Fullify(cmd, p, pl);
				result = Load<T>(types, cmd, recursiveLoad);
			} else
				result = Load<T>(new Type[] { typeof(T) }, cmd, recursiveLoad);
			return result != null ? result.Result : null;
		}

		public override void Save(object target, bool isNew) {
			Catalogable schema = Catalogable.For(target);
			if (isNew) {
				object id = ExecuteScalar(insertcmd(schema, target));
				schema.IDProperty.SetValue(target, Convert.ChangeType(id, schema.IDProperty.PropertyType), null);
			} else
				ExecuteNonQuery(savecmd(schema, target));
		}

		public override void Delete(object target) {
			ExecuteNonQuery(deletecmd(target));
		}

		#endregion CRUD



		#region abstract methods

		protected abstract IDbCommand CommandNew();
		protected abstract IDbConnection ConnectionNew();

		public abstract IDbDataParameter ParameterNew(string name, object value);

		#endregion abstract methods

		#region executes

		protected void ExecuteNonQuery(IDbCommand cmd) {
			Execute(cmd, () => cmd.ExecuteNonQuery());
		}

		protected object ExecuteScalar(IDbCommand cmd) {
			return Execute(cmd, () => cmd.ExecuteScalar());
		}

		protected object Execute(IDbCommand cmd, Func<object> t) {
			object result = null;
			using (IDbConnection connection = ConnectionNew()) {
				cmd.Connection = connection;
				connection.Open();
				result = t();
				connection.Close();
			}
			return result;
		}

		#endregion executes

		protected Type[] Fullify(IDbCommand cmd, Catalogable p, params IDbDataParameter[] parameters) {
			IDbCommand where = whereClause(p, parameters);
			List<Type> types = new List<Type>() { p.Type };
			foreach (PropertyInfo property in p.Associated) {
				PropertyInfoEx tEx = PropertyInfoEx.ForType(property.PropertyType);
				Catalogable pt = Catalogable.For(tEx.ElementType);
				string KeyID = p.HasPropertyAttribute(property) ? p.PropertyAttribute(property).KeyID : tEx.ElementType.Name + "ID";
				if (!tEx.IsListOrArray && pt.IDProperty == null)
					continue;
				if (pt.Cached) {
					if (pt.Cache == null && !types.Contains(tEx.ElementType)) {
						cmd.CommandText += string.Format(";\r\nSELECT {0} FROM [{1}]", string.Join(", ", pt.Columns.Select(c => c.Name)), pt.TableName);
						types.Add(tEx.ElementType);
					}
				} else {
					string qry = string.Format(";\r\nSELECT {0} FROM [{1}] WHERE {2} IN (SELECT {3} FROM [{4}] {5})"
						, string.Join(", ", pt.Columns.Select(c => c.Name))
						, pt.TableName
						, tEx.IsListOrArray ? p.IDProperty.Name : pt.IDProperty.Name //array/list issue if FK isn't same as ID Name
						, tEx.IsListOrArray ? p.IDProperty.Name : KeyID
						, p.TableName
						, where.CommandText);

					cmd.CommandText += qry;
					types.Add(tEx.ElementType);
				}
			}
			return types.ToArray();
		}

		/// <summary>
		/// Runs a DBCommand and adds the various results to a ResultSet object. 
		/// It processes the ResultSet queue after the command is complete to wire up all the "associated" properties between results.
		/// Potentially more DBCommands could be generated and executed during this processing of the queue for any missing results.
		/// </summary>
		/// <typeparam name="T">The Type of the first (and primary) result for this call to Load</typeparam>
		/// <param name="types">The full list of types that will be returned by the DBCommand</param>
		/// <param name="cmd">The DBCommand to run</param>
		/// <param name="recursiveLoad">Dictates whether to manually attempt to load missing results expected during wiring up the "associated properties</param>
		/// <returns></returns>
		protected ResultSet<T[]> Load<T>(Type[] types, IDbCommand cmd, bool recursiveLoad) where T : class {
			if (typeof(T) != types[0])
				throw new Exception("First Type does not match Generic Type");
			ResultSet<T[]> rs = null;
			//try 
			{
				using (IDbConnection connection = ConnectionNew()) {

					cmd.Connection = connection;
					connection.Open();

					DbDataReader reader = (DbDataReader)cmd.ExecuteReader();
					int i = 0;
					while (reader.HasRows && i < types.Length) {
						try {
							Catalogable pt = Catalogable.For(types[i]);
							ArrayList results = new ArrayList();
							while (reader.Read()) {
								object obj = Activator.CreateInstance(pt.Type);
								InstantiateProperties(pt, obj, s => reader[s]);
								results.Add(obj);
							}
							if (pt.Cached && pt.Cache == null)
								pt.Cache = results.ToArray(); //check if it's cacheable and add if not already cached
							if (rs == null)
								rs = new ResultSet<T[]>((T[])results.ToArray(pt.Type));
							else
								rs.Add(results.ToArray(pt.Type));
							reader.NextResult();
						} catch (Exception ex) { } //no error handling for now
						i++;
					}
					connection.Close();
				}
				if(rs != null) //ie results found
					rs.ProcessQueue(this, recursiveLoad);
			} //catch (Exception ex) { }

			return rs;
		}

		protected IDbDataParameter[] ParameterList(object[] parameters) {
			if (parameters.Length % 2 > 0)
				throw new Exception("Invalid parameters");
			List<IDbDataParameter> ps = new List<IDbDataParameter>();
			for (int i = 0; i < parameters.Length; i += 2)
				ps.Add(ParameterNew(parameters[i].ToString(), parameters[i + 1] ?? DBNull.Value));
			return ps.ToArray();
		}

		/// <summary>
		/// A parser of "null" values, looking to return DBNull.Value if needed.
		/// Not exhaustive but demonstrative.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="item"></param>
		/// <returns></returns>
		protected object GetParameterValue(PropertyInfo property, object item) {
			object value = property.GetValue(item, null);
			Type t = property.GetType();

			switch (property.PropertyType.Name) {
				case "Int32":
					if ((int)value == int.MinValue)
						value = DBNull.Value;
					break;
				case "Int64":
					if ((long)value == long.MinValue)
						value = DBNull.Value;
					break;
				case "Decimal":
					if ((decimal)value == decimal.MinValue)
						value = DBNull.Value;
					break;
				case "DateTime":
					if ((DateTime)value == DateTime.MinValue)
						value = DBNull.Value;
					break;
			}

			return value ?? DBNull.Value;
		}

		#region CRUD Commands

		protected virtual IDbCommand deletecmd(object item) {
			Catalogable schema = Catalogable.For(item);
			IDbCommand cmd = CommandNew();
			string qry = string.Format("DELETE FROM {0} WHERE {1}=@{2}", schema.TableName, schema.IDProperty.Name, schema.IDProperty.Name);
			cmd.Parameters.Add(ParameterNew(schema.IDProperty.Name, schema.ID(item)));
			cmd.CommandText = qry;
			return cmd;
		}

		protected virtual IDbCommand whereClause(Catalogable schema, params IDbDataParameter[] parameters) {
			IDbCommand cmd = CommandNew();
			if (parameters == null || parameters.Length == 0)
				return cmd;
			string paramqry = " WHERE ";
			for (int i = 0; i < parameters.Length; i++) {
				if (schema.Columns.Any(p => p.Name == parameters[i].ParameterName)) {
					paramqry += string.Format("{0} = @{1} {2}", parameters[i].ParameterName, "p" + i.ToString(), i < parameters.Length - 1 ? " AND " : "");
					cmd.Parameters.Add(ParameterNew("p" + i.ToString(), parameters[i].Value));
				} else
					throw new Exception("Parameter not found : " + parameters[i].ParameterName);
			}
			cmd.CommandText = paramqry;
			return cmd;
		}

		protected virtual IDbCommand findallcmd(Catalogable schema, CommandType cmdType, params IDbDataParameter[] parameters) {
			IDbCommand cmd = CommandNew();
			cmd.CommandType = cmdType;
			switch (cmdType) {
				case CommandType.Text:
					if (schema.Cached) {
						cmd = CommandNew();
						cmd.CommandType = cmdType;
						cmd.CommandText = string.Format("SELECT {0} FROM [{1}]", string.Join(", ", schema.Columns.Select(c => c.Name)), schema.TableName);
					} else {
						cmd = whereClause(schema, parameters);
						cmd.CommandType = cmdType;
						cmd.CommandText = string.Format("SELECT {0} FROM [{1}] {2}", string.Join(", ", schema.Columns.Select(c => c.Name)), schema.TableName, cmd.CommandText);
					}
					break;
				case CommandType.StoredProcedure:
					if (parameters != null && parameters.Length > 0) {
						for (int i = 0; i < parameters.Length; i++)
							cmd.Parameters.Add(ParameterNew(parameters[i].ParameterName, parameters[i].Value));
					}
					break;
			}
			return cmd;
		}

		protected virtual IDbCommand insertcmd(Catalogable schema, object item) {
			IDbCommand cmd = CommandNew();
			string fieldnames = string.Empty, paramnames = string.Empty;
			foreach (var col in schema.Columns.Where(col => col.Name != schema.IDProperty.Name)) {
				fieldnames += string.Format("{0}, ", col.Name);
				paramnames += string.Format("@{0}, ", col.Name);
				cmd.Parameters.Add(ParameterNew(col.Name, GetParameterValue(col, item)));
			}
			cmd.CommandText = string.Format("INSERT INTO [{0}]({1}) OUTPUT Inserted.[{2}] VALUES ({3}); ", schema.TableName, fieldnames.TrimEnd(',', ' '), schema.IDProperty.Name, paramnames.TrimEnd(',', ' '));
			return cmd;
		}

		protected virtual IDbCommand savecmd(Catalogable schema, object item) {
			IDbCommand cmd = CommandNew();
			string fields = "";
			foreach (var col in schema.Columns.Where(col => col.Name != schema.IDProperty.Name)) {
				fields += string.Format("{0}=@{1}, ", col.Name, col.Name);
				cmd.Parameters.Add(ParameterNew(col.Name, GetParameterValue(col, item)));
			}
			cmd.Parameters.Add(ParameterNew(schema.IDProperty.Name, schema.ID(item)));
			cmd.CommandText = string.Format("UPDATE [{0}] SET {1} WHERE {2}=@{3}", schema.TableName, fields.TrimEnd(',', ' '), schema.IDProperty.Name, schema.IDProperty.Name);
			return cmd;
		}

		#endregion CRUD Commands
	}
}