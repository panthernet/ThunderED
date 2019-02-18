using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using ThunderED.Helpers;

namespace ThunderED.Providers
{
    internal abstract class DBProviderBase<TConnection, TCommand>
        where TConnection: class, IDbConnection, new()
        where TCommand: class, IDbCommand, new()
    {
        protected string Schema;
        protected string SchemaDot;

        protected virtual string CreateConnectString(bool skipDatabase = false)
        {
            return null;
        }

        protected async Task SessionWrapper(string query, Func<TCommand, Task> method)
        {
            using (var session = new TConnection())
            {
                session.ConnectionString = CreateConnectString();
                using (var command = new TCommand())
                {
                    command.CommandText = query;
                    command.Connection = session;
                    session.Open();
                    await method(command);
                }
            }
        }

        protected void SessionWrapper(string query, Action<TCommand> method)
        {
            using (var session = new TConnection())
            {
                session.ConnectionString = CreateConnectString();
                using (var command = new TCommand())
                {
                    command.CommandText = query;
                    command.Connection = session;
                    session.Open();
                    method(command);
                }
            }
        }

        protected async Task<TValue> SessionWrapper<TValue>(string query, Func<TCommand, Task<TValue>> method)
        {
            using (var session = new TConnection())
            {
                session.ConnectionString = CreateConnectString();
                using (var command = new TCommand())
                {
                    command.CommandText = query;
                    command.Connection = session;
                    session.Open();
                    return await method(command);
                }
            }
        }

        protected TValue SessionWrapper<TValue>(string query, Func<TCommand, TValue> method)
        {
            using (var session = new TConnection())
            {
                session.ConnectionString = CreateConnectString();
                using (var command = new TCommand())
                {
                    command.CommandText = query;
                    command.Connection = session;
                    session.Open();
                    return method(command);
                }
            }
        }

        protected T CreateParam<T>(string name, object value)
            where T: IDbDataParameter, new()
        {
            return new T { ParameterName = name, Value = value};
        }

        public async Task<bool> RunScript(string file)
        {
            if (!File.Exists(file)) return false;

            return await SessionWrapper(File.ReadAllText(file), async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(RunScript), ex, LogCat.Database);
                    return false;
                }

                return true;
            });
        }
      
        public async Task RunCommand(string query2, bool silent = false)
        {
            await SessionWrapper(query2, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    if (!silent)
                        await LogHelper.LogEx($"[{nameof(RunCommand)}]: {query2}", ex, LogCat.Database);
                }
            });
        }

        public async Task<bool> RunScriptText(string text)
        {
            return await SessionWrapper(text, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(RunScriptText), ex, LogCat.Database);
                    return false;
                }

                return true;
            });
        }
      

        public async Task RunSystemCommand(string query2, bool silent = false)
        {
            try
            {
                using (var session = new TConnection())
                {
                    session.ConnectionString = CreateConnectString(true);
                    using (var command = new TCommand())
                    {
                        command.CommandText = query2;
                        command.Connection = session;
                        session.Open();
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                    await LogHelper.LogEx($"[{nameof(RunSystemCommand)}]: {query2}", ex, LogCat.Database);
            }
        }

    }
}
