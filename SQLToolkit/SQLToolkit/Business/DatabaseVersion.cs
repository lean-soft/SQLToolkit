﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SQLToolkit.Business
{
    public static class DatabaseVersion
    {
        /// <summary>
        /// 初始化升级工具基础表
        /// </summary>
        /// <returns></returns>
        public static int Init(string database)
        {
            return Helper.SQLHelper.ExecuteNonQuery(string.Format(@"
                IF db_id('{0}') IS NULL 
                Create Database {1}
                GO
                Use {2}
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ST_DatabaseVersion')
                CREATE TABLE ST_DatabaseVersion(
                    ID int IDENTITY(1,1) PRIMARY KEY,
                    [Filename] [nvarchar](MAX)  NULL,
                    [ExecuteResult] [nvarchar](MAX)  NULL,
                    [ExecuteTime] [nvarchar](MAX)  NULL,
                    [Message] [nvarchar](MAX)  NULL
                )", database, database, database));
        }

      

        /// <summary>
        /// 客户端数据库升级
        /// </summary>
        /// <param name="path">数据库脚本路径</param>
        public static void Upgrade(string database,string path)
        {
            if(!ValidateSqlPath(path))
            {
                Helper.LogHelper.Log(string.Format("Error:The path you supply is not exist,path:{0}", path));
                return;
            }

            var allSQLFiles = Directory.GetFiles(path).OrderBy(i => i).ToArray();
            var runFiles = updateScripts(database,allSQLFiles);
            foreach (string file in runFiles)
            {
                var filename = Path.GetFileName(file);
                Helper.LogHelper.Log(string.Format("Ready to exec sql script:{0}", filename));

                try
                {
                    Helper.SQLHelper.ExecuteNonQuery(File.ReadAllText(file));
                    Business.DatabaseVersion.UpdateRecord(database,filename, "success","");
                    Helper.LogHelper.Log(string.Format("Successful run sql script:{0}", filename));


                }
                catch (Exception ex)
                {
                    Business.DatabaseVersion.UpdateRecord(database,filename, "fail", ex.Message);
                    Helper.LogHelper.Log(string.Format("Failed run sql script:{0}", filename));
                    Helper.LogHelper.Log(string.Format("Error:{0}", ex.ToString()));
                    break;

                }

                Helper.LogHelper.Log(string.Format("Finish to exec sql script:{0}", file));
            }

        }


        /// <summary>
        /// 更新脚本执行记录
        /// </summary>
        /// <param name="filename">脚本文件名称</param>
        /// <param name="result">执行结果</param>
        /// <returns></returns>
        public static int UpdateRecord(string database,string filename, string result,string message)
        {
            var sql = "";
            if (!ExecutedBefore(database,filename)){
                sql = string.Format(@"
                Use {0}
                INSERT INTO ST_DatabaseVersion (Filename, ExecuteResult, ExecuteTime,Message)
                VALUES ('{1}', '{2}', '{3}','{4}');", database,filename, result, DateTime.Now.ToString(), message);
            }
            else {
                
                sql = string.Format(@"
                Use {0}
                UPDATE st_databaseversion
                SET ExecuteResult = '{1}', 
                    ExecuteTime= '{2}',
                    Message='{3}'
                WHERE Filename = '{4}';", database,result, DateTime.Now.ToString(), message,filename);
            }
           
            return Helper.SQLHelper.ExecuteNonQuery(sql);
        }

        /// <summary>
        /// 返回需要执行的SQL升级脚本列表
        /// </summary>
        /// <param name="files">全部SQL升级脚本文件列表</param>
        /// <returns></returns>
        private static string[] updateScripts(string database,string[] files)
        {
            var lastVersion = LastVersion(database);
            if(lastVersion==null)
            {
                return files;
            }
            else
            { 
                var lastVersionIndex = Array.FindIndex(files, f => Path.GetFileName(f) == lastVersion.ToString());
                var startIndex = lastVersionIndex + 1;
                var length = files.Length - startIndex;
                return Helper.ArrayHelper.SubArray(files,startIndex , length);
            }
        }



        /// <summary>
        /// 获取客户端最新版本
        /// </summary>
        /// <returns>sql脚本文件名称</returns>
        private static object LastVersion(string database)
        {
            var sql = string.Format(@"
            Use {0}
            select top 1 filename from  st_databaseversion
            where executeResult='success' order by filename desc",database);
            return Helper.SQLHelper.ExecuteScalar(sql);
        }


        private static bool ExecutedBefore(string database,string filename)
        {
            var exists = Helper.SQLHelper.ExecuteScalar(string.Format("Use {0} select count(1) from ST_DatabaseVersion where filename='{0}'", database,filename));
            return Convert.ToBoolean(exists);
        }

        private static bool ValidateSqlPath(string path)
        {
            if (!Directory.Exists(path))
            {
                return false;
            }
            return true;
        }


    }
}
