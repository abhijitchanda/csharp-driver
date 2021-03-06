﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cassandra.Data.Linq
{
    public class Batch : Query
    {
        readonly Session _session;

        internal Batch(Session session)
        {
            _session = session;
        }

        private readonly StringBuilder _batchScript = new StringBuilder();
        private string _batchType = "";

        public void Append(CqlCommand cqlCommand)
        {
            if (cqlCommand.GetTable().GetTableType() == TableType.Counter)
                _batchType = "COUNTER ";
            _batchScript.AppendLine(cqlCommand.GetCql());
        }

        public void Append(IEnumerable<CqlCommand> cqlCommands)
        {
            foreach (var cmd in cqlCommands)
                Append(cmd);
        }

        public void Execute()
        {
            EndExecute(BeginExecute(null, null));
        }

        private struct CqlQueryTag
        {
            public Session Session;
        }

        public IAsyncResult BeginExecute(AsyncCallback callback, object state)
        {
            if (_batchScript.Length != 0)
            {
                var ctx = _session;
                var cqlQuery = "BEGIN " + _batchType + "BATCH\r\n" + _batchScript.ToString() + "\r\nAPPLY " + _batchType + "BATCH";
                return ctx.BeginExecute(new SimpleStatement(cqlQuery).EnableTracing(IsTracing).SetConsistencyLevel(ConsistencyLevel),
                                    new CqlQueryTag() { Session = ctx }, callback, state);
            }
            throw new ArgumentOutOfRangeException();
        }

        public void EndExecute(IAsyncResult ar)
        {
            InternalEndExecute(ar);
        }

        public override CassandraRoutingKey RoutingKey
        {
            get { return null; }
        }

        protected override IAsyncResult BeginSessionExecute(Session session, object tag, AsyncCallback callback, object state)
        {
            if (!ReferenceEquals(_session, session))
                throw new ArgumentOutOfRangeException("session");
            return BeginExecute(callback, state);
        }

        public QueryTrace QueryTrace { get; private set; }

        private CqlRowSet InternalEndExecute(IAsyncResult ar)
        {
            var tag = (CqlQueryTag)Session.GetTag(ar);
            var ctx = tag.Session;
            var outp = ctx.EndExecute(ar);
            QueryTrace = outp.QueryTrace;
            return outp;
        }

        protected override CqlRowSet EndSessionExecute(Session session, IAsyncResult ar)
        {
            if (!ReferenceEquals(_session, session))
                throw new ArgumentOutOfRangeException("session");
            return InternalEndExecute(ar);
        }

    }
}
