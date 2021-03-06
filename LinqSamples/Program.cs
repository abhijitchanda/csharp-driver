﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Cassandra.Data.Linq;
using Cassandra;

namespace LinqSamples
{
    class Program
    {
        [AllowFiltering]
        [Table("nerdiStuff")]
        public class NerdMovie
        {
            [ClusteringKey(1)]
            [Column("diri")]
            public string Director { get; set; }

            [Column("mainGuy")]
            public string MainActor;

            [PartitionKey(1)]
            [Column("movieTile")]
            public string Movie;

            [PartitionKey(5)]
            [Column("movieMaker")]
            public string Maker;

            [Column("When-Made")]
            public int Year { get; set; }
        }

        public class ExtMovie
        {
            public string TheDirector;
            public int Size;
            public string TheMaker;
        }

        static void Main(string[] args)
        {
            Cluster cluster = Cluster.Builder().WithConnectionString("Contact Points=cassi.cloudapp.net;Port=9042").Build();

            using (var session = cluster.Connect())
            {

                string keyspaceName = "Excelsior"+Guid.NewGuid().ToString("N");

                try
                {
                    session.ChangeKeyspace(keyspaceName);
                }
                catch (InvalidException)
                {
                    session.CreateKeyspaceIfNotExists(keyspaceName);
                    session.ChangeKeyspace(keyspaceName);
                }

                var table = session.GetTable<NerdMovie>();
                table.CreateIfNotExists();


                {
                    var batch = session.CreateBatch();

                    var movies = new List<NerdMovie>()
                    {
                        new NerdMovie(){ Movie = "Serenity", Maker="20CentFox",  Director = "Joss Whedon", MainActor = "Nathan Fillion", Year = 2005},
                        new NerdMovie(){ Movie = "Pulp Fiction", Maker = "Pixar", Director = "Quentin Tarantino", MainActor = "John Travolta", Year = 1994},
                    };

                    batch.Append(from m in movies select table.Insert(m));

                    batch.Execute();
                }

                var testmovie = new NerdMovie { Year = 2005, Director = "Quentin Tarantino", Movie = "Pulp Fiction", Maker = "Pixar" };
                table.Where(m => m.Movie == testmovie.Movie && m.Maker==testmovie.Maker && m.Director == testmovie.Director).Select(m => new NerdMovie { Year = testmovie.Year }).Update().Execute();


                var anonMovie = new { Director = "Quentin Tarantino", Year = 2005 };
                table.Where(m => m.Movie == "Pulp Fiction" && m.Maker == "Pixar" && m.Director == anonMovie.Director).Select(m => new NerdMovie { Year = anonMovie.Year, MainActor = "George Clooney" }).Update().Execute();

                var all2 = table.Where((m) => CqlToken.Create(m.Movie, m.Maker) > CqlToken.Create("Pulp Fiction", "Pixar")).Execute().ToList();
                var all = (from m in table where CqlToken.Create(m.Movie, m.Maker) > CqlToken.Create("Pulp Fiction", "Pixar") select m).Execute().ToList();

                var nmT = (from m in table where m.Director == "Quentin Tarantino" select new ExtMovie { TheDirector = m.MainActor, Size=all.Count, TheMaker = m.Director }).Execute().ToList();
                var nm1 = (from m in table where m.Director == "Quentin Tarantino" select new { MA = m.MainActor, Z = 10, Y = m.Year }).Execute().ToList();

                var nmX = (from m in table where m.Director == "Quentin Tarantino" select new { m.MainActor, Z = 10, m.Year }).Execute().ToList();

                (from m in table where m.Movie.Equals("Pulp Fiction") && m.Maker.Equals("Pixar") && m.Director == "Quentin Tarantino" select new NerdMovie { Year = 1994 }).Update().Execute();

                table.Where((m) => m.Movie == "Pulp Fiction" && m.Maker == "Pixar" && m.Director == "Quentin Tarantino").Select((m) => new NerdMovie { Year = 1994 }).Update().Execute();

                var nm2 = table.Where((m) => m.Director == "Quentin Tarantino").Select((m) => new { MA = m.MainActor, Y = m.Year }).Execute().ToList();

                (from m in table where m.Movie == "Pulp Fiction" && m.Maker == "Pixar" && m.Director == "Quentin Tarantino" select m).Delete().Execute();

                var nm3 = (from m in table where m.Director == "Quentin Tarantino" select new { MA = m.MainActor, Y = m.Year }).Execute().ToList();


                session.DeleteKeyspaceIfExists(keyspaceName);
            }

            cluster.Shutdown();
        }
    }
}