﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cassandra;
using Cassandra.Data.Linq;

//based on https://github.com/pchalamet/cassandra-sharp/tree/master/Samples
namespace TPLSample.NerdMoviesLinqSample
{

    [AllowFiltering]
    public class NerdMovie
    {
        [ClusteringKey(1)]
        public string Director;

        public string MainActor;

        [PartitionKey]
        public string Movie;

        public int Year;
    }

    public class NerdMoviesLinqSample
    {
        public static void Run()
        {
            Cluster cluster = Cluster.Builder().AddContactPoint("cassi.cloudapp.net").WithoutRowSetBuffering().Build();

            using (var session = cluster.Connect())
            {
                const string keyspaceName = "Excelsior";
                Console.WriteLine("============================================================");
                Console.WriteLine("Creating keyspace...");

                try
                {
                    session.ChangeKeyspace(keyspaceName);
                }
                catch (InvalidException)
                {
                    session.CreateKeyspaceIfNotExists(keyspaceName);
                    session.ChangeKeyspace(keyspaceName);
                }

                Console.WriteLine("============================================================");
                Console.WriteLine("Creating tables...");
                var context = new Context(session);
                context.AddTable<NerdMovie>();
                context.CreateTablesIfNotExist();
                Console.WriteLine("============================================================");

                context.GetTable<NerdMovie>().AddNew(new NerdMovie(){ Movie = "Serenity", Director = "Joss Whedon", MainActor = "Nathan Fillion", Year = 2005});
                var taskSaveMovies = Task.Factory.FromAsync(context.BeginSaveChangesBatch, context.EndSaveChangesBatch, TableType.Standard, ConsistencyLevel.Default, null);

                taskSaveMovies.Wait();

                var selectNerdMovies = context.GetTable<NerdMovie>(); //select everything from table


                var taskSelectStartMovies =
                    Task<IEnumerable<NerdMovie>>.Factory.FromAsync(selectNerdMovies.BeginExecute,
                                                                   selectNerdMovies.EndExecute, null)
                                                .ContinueWith(res => DisplayMovies(res.Result));



                taskSelectStartMovies.Wait();

                var selectAllFromWhere = from m in context.GetTable<NerdMovie>() where m.Director == "Joss Whedon" select m;

                var taskselectAllFromWhere =
                    Task<IEnumerable<NerdMovie>>.Factory.FromAsync(selectAllFromWhere.BeginExecute,
                                                                   selectAllFromWhere.EndExecute, null)
                                                .ContinueWith(res => DisplayMovies(res.Result));

                taskselectAllFromWhere.Wait();

                var taskselectAllFromWhereWithFuture =
                    Task<IEnumerable<NerdMovie>>.Factory.FromAsync(selectAllFromWhere.BeginExecute,
                                                                   selectAllFromWhere.EndExecute, null)
                                                .ContinueWith(a => a.Result.ToList());

                DisplayMovies(taskselectAllFromWhereWithFuture.Result);

                session.DeleteKeyspaceIfExists(keyspaceName);
            }

            cluster.Shutdown();
        }

        private static void DisplayMovies(IEnumerable<NerdMovie> result)
        {
            foreach (var resMovie in result)
            {
                Console.WriteLine("Movie={0} Director={1} MainActor={2}, Year={3}",
                                  resMovie.Movie, resMovie.Director, resMovie.MainActor, resMovie.Year);
            }
            Console.WriteLine();
            Console.WriteLine();
        }

    }
}