using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Collections;

namespace MereCatalog
{
	/// <summary>
	/// A base class for a SQL based MereCataloger.  May not be completely SQL variant agnostic.
	/// </summary>
	public abstract class MereCatalogerSQL : MereCataloger {

		#region CRUD
		public override T[] Find<T>(bool eagerLoad, params object[] parameters)
		{
			Catalogable p = Catalogable.For(typeof(T));
			IDbDataParameter[] pl = ParameterList(parameters);
			IDbCommand cmd = findallcmd(p, CommandType.Text, pl);
			var cmdd = whereClause(p, pl);

			QuerySet querySet = new QuerySet(p, cmd);
			ResultSet<T[]> result;

			//iterate through associateds, adding queries as we go
			if (eagerLoad) {
				BuildOut(querySet, p, cmdd.CommandText, string.Empty);
				var types = CompleteQuerySetCmd(querySet, cmd);
				result = Load<T>(types, cmd, false);
			} else
				result = Load<T>(new List<Type> { typeof(T) }, cmd, false);
			return result?.Result;
		}

		List<Type> CompleteQuerySetCmd(QuerySet querySet, IDbCommand cmd)
		{
			List<Type> types = new List<Type> { querySet.Type.Type };
			foreach (var cq in querySet.CachedQueries) {
				if (cq.Type.Cache == null) {
					cmd.CommandText += cq.CommandText;
					types.Add(cq.Type.Type);
				}
			}
			foreach (var q in querySet.AssociateQueries) {
				cmd.CommandText += q.CommandText;
				types.Add(q.Type.Type);
			}

			return types;
		}

		private void BuildOut(QuerySet querySet, Catalogable p, string whereCmdText, string keyIgnoreRecursion)
		{
			foreach (PropertyInfo property in p.Associated)
			{
				TypeEx tEx = property.TypeEx();
				Catalogable pt = Catalogable.For(tEx.ElementType);

				if (!tEx.IsListOrArray && pt.IDProperty == null)
					continue;

				if (pt.Cached) {
					if (!querySet.CachedQueries.Any(cq => cq.Type == pt)) {
						string cmdText = string.Format("SELECT {0} FROM [{1}];\r\n", string.Join(", ", pt.Columns.Select(c => "[" + c.Name + "]")), pt.TableName);
						querySet.CachedQueries.Add(new Query(pt, cmdText));
					}

					string whereText = string.Format("SELECT {0} FROM [{1}];\r\n", pt.IDProperty.Name, pt.TableName);

					BuildOut(querySet, pt, whereText, string.Empty);
				} else {
					/* either	single property		select ... from assoctable where id in (select assoctableid from parenttable where ...)
					 * or		array property		select ... from assoctable where parenttableid in (select id from parenttable where ...)
					 */

					string assocTableKeyID = tEx.IsListOrArray ? p.Reference : pt.IDProperty.Name; //array/list issue if FK isn't same as ID Name
					string parentTableKeyID = tEx.IsListOrArray ? p.IDProperty.Name : (p.HasPropertyAttribute(property) ? p.PropertyAttribute(property).KeyID : pt.Reference);

					if (string.IsNullOrEmpty(keyIgnoreRecursion) || keyIgnoreRecursion != parentTableKeyID) {

						string newWhereCmdText = string.Format(" WHERE {0} IN (SELECT {1} FROM [{2}] {3})"
							, assocTableKeyID
							, parentTableKeyID
							, p.TableName
							, whereCmdText);

						string cmdText = string.Format("SELECT {0} FROM [{1}] {2} {3};\r\n"
							, string.Join(", ", pt.Columns.Select(c => "[" + c.Name + "]"))
							, pt.TableName
							, assocTableKeyID
							, newWhereCmdText);

						querySet.AssociateQueries.Add(new Query(pt, cmdText));
						BuildOut(querySet, pt, newWhereCmdText, tEx.IsListOrArray ? assocTableKeyID : string.Empty);
					}
				}
			}
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

		/// <summary>
		/// Runs a DBCommand and adds the various results to a ResultSet object. 
		/// It processes the ResultSet queue after the command is complete to wire up all the "associated" properties between results.
		/// Potentially more DBCommands could be generated and executed during this processing of the queue for any missing results.
		/// </summary>
		/// <typeparam name="T">The Type of the first (and primary) result for this call to Load</typeparam>
		/// <param name="types">The full list of types that will be returned by the DBCommand</param>
		/// <param name="cmd">The DBCommand to run</param>
		/// <param name="eagerLoad">Dictates whether to manually attempt to load missing results expected during wiring up the "associated properties</param>
		/// <returns></returns>
		protected ResultSet<T[]> Load<T>(List<Type> types, IDbCommand cmd, bool eagerLoad) where T : class {
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
					int typeCount = types.Count();
					while (reader.HasRows && i < typeCount) {
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
				rs?.ProcessQueue(this, eagerLoad);
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
					cmd = schema.Cached ? CommandNew() : whereClause(schema, parameters);
					cmd.CommandType = cmdType;
					cmd.CommandText = string.Format("SELECT {0} FROM [{1}] {2};\r\n", string.Join(", ", schema.Columns.Select(c => "["+c.Name+"]")), schema.TableName, cmd.CommandText); //cmd.CommandText will be empty for schema.Cached
					break;
					// SPs may not be available in all SQLs - should be moved to SQLServer.
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

	internal class Query {
		public Query(Catalogable type, string cmdText) { Type = type; CommandText = cmdText; }
		public Catalogable Type { get; set; } ///maybe string or maybe Catalogable instead of Type

		public string CommandText { get; set; }

		public override string ToString() => "Query: " + Type;
	}

	internal class QuerySet {
		public Catalogable Type { get; set; }
		public QuerySet(Catalogable p, IDbCommand cmd) {
			Type = p;
			DbCommand = cmd;
		}
		public List<Query> CachedQueries { get; set; } = new List<Query>();
		public List<Query> AssociateQueries { get; set; } = new List<Query>();

		public IDbCommand DbCommand { get; set; }

		public string cmds => string.Join("\r\n", AssociateQueries.Select(q => q.CommandText));

	}
}