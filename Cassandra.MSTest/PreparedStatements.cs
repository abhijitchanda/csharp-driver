﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if MYTEST
using MyTest;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using System.Text;

namespace Cassandra.MSTest
{
    
    public partial class PreparedStatements
    {
        string Keyspace = "tester";
        Cluster Cluster;
        Session Session;

        public PreparedStatements()
        {
        }

        [TestInitialize]
        public void SetFixture()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            var clusterb = Cluster.Builder().AddContactPoint("cassi.cloudapp.net");
            clusterb.WithDefaultKeyspace(Keyspace);

            if (Cassandra.MSTest.Properties.Settings.Default.Compression)
            {
                clusterb.WithCompression(CompressionType.Snappy);
                Console.WriteLine("Compression: Snappy Compression");
            }
            else Console.WriteLine("Compression: No Compression");

            Cluster = clusterb.Build();
            Diagnostics.CassandraTraceSwitch.Level = System.Diagnostics.TraceLevel.Verbose;
            Diagnostics.CassandraStackTraceIncluded = true;
            Diagnostics.CassandraPerformanceCountersEnabled = true;
            Session = Cluster.ConnectAndCreateDefaultKeyspaceIfNotExists(ReplicationStrategies.CreateSimpleStrategyReplicationProperty(2), true);
        }

        [TestCleanup]
        public void Dispose()
        {
            Session.DeleteKeyspace(Keyspace);
            Session.Dispose();
            Cluster.Shutdown();
        }


        public void insertingSingleValuePrepared(Type tp)
        {
            string cassandraDataTypeName = QueryTools.convertTypeNameToCassandraEquivalent(tp);
            string tableName = "table" + Guid.NewGuid().ToString("N");

            QueryTools.ExecuteSyncNonQuery(Session, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         value {1}
         );", tableName, cassandraDataTypeName));

            List<object[]> toInsert = new List<object[]>(1);
            var val = Randomm.RandomVal(tp);
            if (tp == typeof(string))
                val = "'" + val.ToString().Replace("'", "''") + "'";

            var row1 = new object[2] { Guid.NewGuid(), val };

            toInsert.Add(row1);

            var prep = QueryTools.PrepareQuery(this.Session, string.Format("INSERT INTO {0}(tweet_id, value) VALUES ({1}, ?);", tableName, toInsert[0][0].ToString()));
            QueryTools.ExecutePreparedQuery(this.Session, prep, new object[1] { toInsert[0][1] });

            QueryTools.ExecuteSyncQuery(Session, string.Format("SELECT * FROM {0};", tableName), toInsert);
            QueryTools.ExecuteSyncNonQuery(Session, string.Format("DROP TABLE {0};", tableName));
        }

        public void massivePreparedStatementTest()
        {
            string tableName = "table" + Guid.NewGuid().ToString("N");

            try
            {
                QueryTools.ExecuteSyncNonQuery(Session, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         numb1 double,
         numb2 int
         );", tableName));
            }
            catch (AlreadyExistsException)
            {
            }
            int numberOfPrepares = 100;

            List<object[]> values = new List<object[]>(numberOfPrepares);
            List<PreparedStatement> prepares = new List<PreparedStatement>();

            Parallel.For(0, numberOfPrepares, i =>
            {

                var prep = QueryTools.PrepareQuery(Session, string.Format("INSERT INTO {0}(tweet_id, numb1, numb2) VALUES ({1}, ?, ?);", tableName, Guid.NewGuid()));

                lock (prepares)
                    prepares.Add(prep);

            });

            Parallel.ForEach(prepares, prep =>
            {
                QueryTools.ExecutePreparedQuery(this.Session, prep, new object[] { (double)Randomm.RandomVal(typeof(double)), (int)Randomm.RandomVal(typeof(int)) });
            });

            QueryTools.ExecuteSyncQuery(Session, string.Format("SELECT * FROM {0};", tableName));
        }

    }
}
