﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace EDDiscovery.DB
{
    public enum DistancsEnum
    {
        Unknown = 0,
        EDSC = 1,
        RedWizzard =2,
        EDDiscovery = 3,
        EDDiscoverySubmitted = 4,
        LessThen = 5,
    }

    public class DistanceClass
    {
        public int id;
        public string NameA;
        public string NameB;
        public double Dist;
        public string CommanderCreate;
        public DateTime CreateTime;
        public DistancsEnum Status;

        public DistanceClass()
        {
        }

        public DistanceClass(DataRow dr)
        {
            id = (int)(long)dr["id"];
            NameA = (string)dr["NameA"];
            NameB = (string)dr["NameB"];
            Dist = Convert.ToDouble(dr["Dist"]);

            CommanderCreate = (string)dr["commanderCreate"];
            CreateTime = (DateTime)dr["CreateTime"];
            CreateTime = CreateTime.ToUniversalTime();
            Status = (DistancsEnum)(long)dr["status"];


        }

        public static List<DistanceClass> ParseEDSC(string json, ref string date)
        {
            List<DistanceClass> listDistances;

            JObject edsc = null;
            if (json != null)
                edsc = (JObject)JObject.Parse(json);

            listDistances = new List<DistanceClass>();

            if (edsc == null)
                return listDistances;

            JObject edscdata = (JObject)edsc["d"];
            if (edscdata == null) // If from file.
                edscdata = edsc;  

            JArray systems = (JArray)edscdata["distances"];

            if (systems != null)
            {
                foreach (JObject jo in systems)
                {
                    List<DistanceClass> dists = Parse(jo, date);

                    foreach (DistanceClass dist in dists)
                    {
                        listDistances.Add(dist);
                    }
                }
            }

            date = edscdata["date"].Value<string>();

            return listDistances;
        }


        public static List<DistanceClass> Parse(JObject jo, string date)
        {
            List<DistanceClass> dists = new List<DistanceClass>();

            string NameA = jo["name"].Value<string>();

            JArray ja = (JArray)jo["refs"];


            foreach (JObject jdist in ja)
            {
                DistanceClass dist = new DistanceClass();

                dist.NameA = NameA;
                dist.NameB = jdist["name"].Value<string>();
                dist.Dist = jdist["dist"].Value<float>();

                dist.CommanderCreate = jdist["commanderupdate"].Value<string>();
                dist.CreateTime = jdist["updatedate"].Value<DateTime>();
                dist.Status = DistancsEnum.EDSC;

                if (date.StartsWith("2010-"))  // första gången läg in fflera om cr>1;
                {
                    dist.CommanderCreate = jdist["commandercreate"].Value<string>();
                    dist.CreateTime = jdist["createdate"].Value<DateTime>();
                    dist.Status = DistancsEnum.EDSC;

                    int cr = jdist["cr"].Value<int>();

                    dists.Add(dist);
                    if (cr > 1)
                    {
                        dist = new DistanceClass();

                        dist.NameA = NameA;
                        dist.NameB = jdist["name"].Value<string>();
                        dist.Dist = jdist["dist"].Value<double>();

                        dist.CommanderCreate = jdist["commanderupdate"].Value<string>();
                        dist.CreateTime = jdist["updatedate"].Value<DateTime>();
                        dist.Status = DistancsEnum.EDSC;

                        if (cr > 2)
                            dist.NameA = NameA;

                        if (cr > 5)
                            cr = 5;

                        for (int ii = 1; ii < cr; ii++)
                            dists.Add(dist);
                    }
                }
                else
                    dists.Add(dist);
            }
            return dists;
        }


        public static bool Delete(DistancsEnum distsource)
        {
            using (SQLiteConnection cn = new SQLiteConnection(SQLiteDBClass.ConnectionString))
            {
                cn.Open();

                using (SQLiteCommand cmd = new SQLiteCommand())
                {
                    cmd.Connection = cn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandTimeout = 30;
                    cmd.CommandText = "Delete from Distances where Status=@Status";
                    cmd.Parameters.AddWithValue("@Status", (int)distsource);


                    SQLiteDBClass.SqlNonQueryText(cn, cmd);

                }


                cn.Close();
        
            }
            return true;
        }

        public static bool Store(List<DistanceClass> dists)
        {
            if (dists == null)
                return true;

            try
            {
                Stopwatch sw = new Stopwatch();

                sw.Start();

                using (SQLiteConnection cn = new SQLiteConnection(SQLiteDBClass.ConnectionString))
                {
                    cn.Open();
                    SQLiteTransaction transaction = cn.BeginTransaction();
                    foreach (DistanceClass dist in dists)
                    {
                        dist.Store(cn);
                    }

                    transaction.Commit();
                    cn.Close();
                }
                sw.Stop();
                System.Diagnostics.Trace.WriteLine("SQLite Add  "+ dists.Count.ToString()+ " distances: " + sw.Elapsed.TotalSeconds.ToString("0.000s"));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Exception : " + ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                return false;
            }

        }

        public bool Store()
        {
            using (SQLiteConnection cn = new SQLiteConnection(SQLiteDBClass.ConnectionString))
            {
                bool ret;
                 ret = Store(cn);

                 if (ret == true)
                 {
                     using (SQLiteCommand cmd2 = new SQLiteCommand())
                     {
                         cmd2.Connection = cn;
                         cmd2.CommandType = CommandType.Text;
                         cmd2.CommandTimeout = 30;
                         cmd2.CommandText = "Select Max(id) as id from Distances";

                         id = (int)(long)SQLiteDBClass.SqlScalar(cn, cmd2);
                     }

                     return true;
                 }
                 return ret;

            }
        }

        private bool Store(SQLiteConnection cn)
        {
            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.Connection = cn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 30;
                cmd.CommandText = "Insert into Distances (NameA, NameB, Dist, CommanderCreate, CreateTime, Status) values (@NameA, @NameB, @Dist, @CommanderCreate, @CreateTime, @Status)";
                cmd.Parameters.AddWithValue("@NameA", NameA);
                cmd.Parameters.AddWithValue("@NameB", NameB);
                cmd.Parameters.AddWithValue("@Dist", Dist);
                cmd.Parameters.AddWithValue("@CommanderCreate", CommanderCreate);
                cmd.Parameters.AddWithValue("@CreateTime", CreateTime);
                cmd.Parameters.AddWithValue("@Status", Status);


                SQLiteDBClass.SqlNonQueryText(cn, cmd);

            }

            return true;

        }


        public bool Update()
        {
            using (SQLiteConnection cn = new SQLiteConnection(SQLiteDBClass.ConnectionString))
            {
                return Update(cn);
            }
        }

        private bool Update(SQLiteConnection cn)
        {
            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.Connection = cn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 30;
                cmd.CommandText = "Update Distances  set NameA=@NameA, NameB=@NameB, Dist=@Dist, commandercreate=@commandercreate, CreateTime=@CreateTime, status=@status  where ID=@id";
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@NameA", NameA);
                cmd.Parameters.AddWithValue("@NameB", NameB);
                cmd.Parameters.AddWithValue("@Dist", Dist);
                cmd.Parameters.AddWithValue("@CommanderCreate", CommanderCreate);
                cmd.Parameters.AddWithValue("@CreateTime", CreateTime);
                cmd.Parameters.AddWithValue("@Status", Status);


                SQLiteDBClass.SqlNonQueryText(cn, cmd);
                return true;
            }
        }


        public bool Delete()
        {
            using (SQLiteConnection cn = new SQLiteConnection(SQLiteDBClass.ConnectionString))
            {
                return Delete(cn);
            }
        }

        private bool Delete(SQLiteConnection cn)
        {
            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.Connection = cn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 30;
                cmd.CommandText = "Delete From  Distances where ID=@id";
                cmd.Parameters.AddWithValue("@ID", id);


                SQLiteDBClass.SqlNonQueryText(cn, cmd);
                return true;
            }
        }




        static public double Distance(SystemClass s1, SystemClass s2)
        {
            List<DistanceClass> dists = new List<DistanceClass>();

            if (s1 == null || s2 == null)
                return -1;

            string name1, name2;
            
            name1 = s1.SearchName;
            name2 = s2.SearchName;

            string key = name1 + ":" + name2;

            if (SQLiteDBClass.dictDistances.ContainsKey(key))
                return SQLiteDBClass.dictDistances[key].Dist;
            else
                return -1;

            /*
            var obj3 = from p in SQLiteDBClass.globalDistances where (p.NameA.ToLower() == name2 && p.NameB.ToLower() == name1) || (p.NameA.ToLower() == name1 && p.NameB.ToLower() == name2) orderby p.CreateTime descending select p;


            foreach (DistanceClass dist in obj3)
            {
                dists.Add(dist);
            }

            if (dists.Count < 1)
                return -1;

            return dists.First().Dist;  
             * */
        }

        static public DataSet SqlQueryText(SQLiteConnection cn, SQLiteCommand cmd)
        {

            //LogLine("SqlQueryText: " + cmd.CommandText);

            try
            {
                DataSet ds = new DataSet();
                SQLiteDataAdapter da = default(SQLiteDataAdapter);
                cmd.CommandType = CommandType.Text;
                cmd.Connection = cn;
                da = new SQLiteDataAdapter(cmd);
                cn.Open();
                da.Fill(ds);
                cn.Close();
                return ds;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SqlQuery Exception: " + ex.Message);
                throw;
            }

        }


        public static double DistanceDB(SystemClass s1, SystemClass s2)
        {
            double distly = -1;
            List<DistanceClass> dists = new List<DistanceClass>();
            try
            {
                using (SQLiteConnection cn = new SQLiteConnection(SQLiteDBClass.ConnectionString))
                {
                    using (SQLiteCommand cmd = new SQLiteCommand())
                    {
                        DataSet ds = null;
                        DataSet ds2 = null;
                        cmd.Connection = cn;
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandTimeout = 30;
                        cmd.CommandText = "SELECT * FROM Distances WHERE NameA = @NameA COLLATE NOCASE  and NameB = @NameB COLLATE NOCASE ";

                        cmd.Parameters.AddWithValue("@NameA", s1.name);
                        cmd.Parameters.AddWithValue("@NameB", s2.name);

                        ds = SqlQueryText(cn, cmd);

                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@NameA", s2.name);
                        cmd.Parameters.AddWithValue("@NameB", s1.name);
                        ds2 = SqlQueryText(cn, cmd);


                        if (ds.Tables.Count > 0)
                        {
                            if (ds.Tables[0].Rows.Count > 0)
                            {
                                foreach (DataRow dr in ds.Tables[0].Rows)
                                {
                                    DistanceClass dist = new DistanceClass(dr);
                                    dists.Add(dist);
                                }
                         
                            }
                        }

                        if (ds2.Tables.Count > 0)
                        {
                            if (ds2.Tables[0].Rows.Count > 0)
                            {
                                foreach (DataRow dr in ds2.Tables[0].Rows)
                                {
                                    DistanceClass dist = new DistanceClass(dr);
                                    dists.Add(dist);
                                }

                            }
                        }


                        if (dists.Count == 0)
                            return -1;


                        return dists[0].Dist;
       

                    }
                }



            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Exception : " + ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);
                return -1;
            }

        }
    }
}
