﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using Dapper;
using FunctionApp.Authentication;
using FunctionApp.DataAccess;
using FunctionApp.Models;
using FunctionApp.Models.GetTaskInstanceJSON;
using FunctionApp.Models.Options;
using FunctionApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FunctionApp.TestHarness
{
    public class Program
    {        

        static void Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables()
                .Build();

            Startup.ConfigureServices(services, config);
            services.AddTransient<App>();

            services.AddLogging(builder => builder.AddConsole().AddFilter(level => level >= LogLevel.Information));
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            var app = serviceProvider.GetService<App>();
            app.Run();
        }
        
    }

    public class App
    {
        private readonly ILogger<App> _logger;
        private readonly TaskTypeMappingProvider _taskTypeMappingProvider;
        private readonly SourceAndTargetSystemJsonSchemasProvider _schemasProvider;
        private readonly IOptions<ApplicationOptions> _options;
        private readonly IAzureAuthenticationProvider _authProvider;
        private Logging.Logging _funcAppLogger = new Logging.Logging();
        private readonly TaskMetaDataDatabase _taskMetaDataDatabase;
        private readonly DataFactoryPipelineProvider _dataFactoryPipelineProvider;
        private readonly DataFactoryClientFactory _dataFactoryClientFactory;
        private readonly ISecurityAccessProvider _sap;

        public App(ILogger<App> logger,
            TaskTypeMappingProvider taskTypeMappingProvider,
            SourceAndTargetSystemJsonSchemasProvider schemasProvider,
            IOptions<ApplicationOptions> options, 
            IAzureAuthenticationProvider authProvider, 
            TaskMetaDataDatabase taskMetaDataDatabase, 
            DataFactoryPipelineProvider dataFactoryPipelineProvider,
            DataFactoryClientFactory dataFactoryClientFactory,
            ISecurityAccessProvider sap)
        {
            _logger = logger;
            _taskTypeMappingProvider = taskTypeMappingProvider;
            _schemasProvider = schemasProvider;
            _options = options;
            _authProvider = authProvider;
            _taskMetaDataDatabase = taskMetaDataDatabase;
            _dataFactoryPipelineProvider = dataFactoryPipelineProvider;
            _dataFactoryClientFactory = dataFactoryClientFactory;
            _sap = sap;
    }

        public void Run()
        {
            var executionId = Guid.NewGuid();            
            ActivityLogItem activityLogItem = new ActivityLogItem
            {
                LogSource = "AF",
                ExecutionUid = executionId
            };
            _funcAppLogger.InitializeLog(_logger, activityLogItem);
            //Test_TaskExecutionSchemaFile(_funcAppLogger);
            GenerateUnitTestResults();
            //InsertTestTasksIntoDb();
            //Test_GetSourceTargetMapping(_funcAppLogger);
            //Test_GetSQLCreateStatementFromSchema(_funcAppLogger);
            //DebugPrepareFrameworkTasks();       

        }



        public IEnumerable<GetTaskInstanceJsonResult> GetTests()
        {
            string filePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + "..\\..\\..\\..\\UnitTests\\tests.json";
            string tests;
            using (var r = new StreamReader(filePath))
            {
                tests = r.ReadToEnd();
            }
            
            var ts = JsonConvert.DeserializeObject<List<GetTaskInstanceJsonResult>>(tests);
            return ts;
        }


        public void InsertTestTasksIntoDb()
        {
            // Test_GetSQLCreateStatementFromSchema(LogHelper);

            var testTaskInstances = GetTests();

            var tmdb = new TaskMetaDataDatabase(_options,_authProvider);
            var con = tmdb.GetSqlConnection();

            var @sql = @"

            delete from [dbo].[TaskGroup] where taskgroupid <=0;            
            
            SET IDENTITY_INSERT [dbo].[TaskGroup] ON
            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-1,'Test Tasks',1, 0,10,null,1)

            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-2,'Test Tasks2',1, 0,10,null,1)

            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-3,'Test Tasks3',1, 0,10,null,1)

            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-4,'Test Tasks4',1, 0,10,null,1)

            SET IDENTITY_INSERT [dbo].[TaskGroup] OFF
   
            ";

            var result = con.Query(sql);

            sql = @"
                    
                    delete from [dbo].[TaskMaster] where taskmasterid <=0;

                    delete from [dbo].[TaskInstance] where taskmasterid <=0;";

            result = con.Query(sql);

            foreach (var testTaskInstance in testTaskInstances)
            {
                JObject processedTaskObject = null;
                try
                {
                    var T = new AdfJsonBaseTask(testTaskInstance, _funcAppLogger);


                    for (int i = 1; i < 5; i++)
                    {
                        var parameters = new
                        {
                            TaskMasterId = (T.TaskMasterId * -1) - (i*100),
                            TaskMasterName = T.AdfPipeline + T.TaskMasterId.ToString(),
                            TaskTypeId = T.TaskTypeId,
                            TaskGroupId = -1*i,
                            ScheduleMasterId = 4,
                            SourceSystemId = T.SourceSystemId,
                            TargetSystemId = T.TargetSystemId,
                            DegreeOfCopyParallelism = T.DegreeOfCopyParallelism,
                            AllowMultipleActiveInstances = 0,
                            TaskDatafactoryIR = "Azure",
                            TaskMasterJSON = T.TaskMasterJson,
                            ActiveYN = 1,
                            DependencyChainTag = "",
                            DataFactoryId = T.DataFactoryId
                        };
                        sql = @"
                                        
                    SET IDENTITY_INSERT [dbo].[TaskMaster] ON;
                    insert into [dbo].[TaskMaster]
                    (
                        [TaskMasterId]                          ,
                        [TaskMasterName]                        ,
                        [TaskTypeId]                            ,
                        [TaskGroupId]                           ,
                        [ScheduleMasterId]                      ,
                        [SourceSystemId]                        ,
                        [TargetSystemId]                        ,
                        [DegreeOfCopyParallelism]               ,
                        [AllowMultipleActiveInstances]          ,
                        [TaskDatafactoryIR]                     ,
                        [TaskMasterJSON]                        ,
                        [ActiveYN]                              ,
                        [DependencyChainTag]                    ,
                        [DataFactoryId]                         
                    )
                    select 
                        @TaskMasterId                          ,
                        @TaskMasterName                        ,
                        @TaskTypeId                            ,
                        @TaskGroupId                           ,
                        @ScheduleMasterId                      ,
                        @SourceSystemId                        ,
                        @TargetSystemId                        ,
                        @DegreeOfCopyParallelism               ,
                        @AllowMultipleActiveInstances          ,
                        @TaskDatafactoryIR                     ,
                        @TaskMasterJSON                        ,
                        @ActiveYN                              ,
                        @DependencyChainTag                    ,
                        @DataFactoryId;  
                    SET IDENTITY_INSERT [dbo].[TaskMaster] OFF;";

                        result = con.Query(sql, parameters);

                    }



                    //T.CreateJsonObjectForAdf(executionId);
                    //processedTaskObject = T.ProcessRoot(_taskTypeMappingProvider, _schemasProvider);
                }
                catch (Exception e)
                {
                    _funcAppLogger.LogErrors(e);
                }
                string FileFullPath = "../../../UnitTestResults/Todo/";
                // Determine whether the directory exists.
                if (!Directory.Exists(FileFullPath))
                {
                    // Try to create the directory.
                    var di = Directory.CreateDirectory(FileFullPath);
                }

                if (processedTaskObject != null)
                {
                    JObject obj = new JObject();
                    obj["TaskObject"] = processedTaskObject;

                    FileFullPath = $"{FileFullPath}{testTaskInstance.TaskType}_{testTaskInstance.AdfPipeline}_{testTaskInstance.TaskMasterId}.json";
                    System.IO.File.WriteAllText(FileFullPath, JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                }
            }

        }

        public void GenerateTestTaskScript()
        {
            // Test_GetSQLCreateStatementFromSchema(LogHelper);

            var testTaskInstances = GetTests();

            var tmdb = new TaskMetaDataDatabase(_options, _authProvider);
            var con = tmdb.GetSqlConnection();

            var @sql = @"

            delete from [dbo].[TaskGroup] where taskgroupid <=0;            
            
            SET IDENTITY_INSERT [dbo].[TaskGroup] ON
            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-1,'Test Tasks',1, 0,10,null,1)

            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-2,'Test Tasks2',1, 0,10,null,1)

            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-3,'Test Tasks3',1, 0,10,null,1)

            INSERT INTO [dbo].[TaskGroup] ([TaskGroupId],[TaskGroupName],[SubjectAreaId], [TaskGroupPriority],[TaskGroupConcurrency],[TaskGroupJSON],[ActiveYN])
            Values (-4,'Test Tasks4',1, 0,10,null,1)

            SET IDENTITY_INSERT [dbo].[TaskGroup] OFF
   
            ";

            var result = con.Query(sql);

            sql = @"
                    
                    delete from [dbo].[TaskMaster] where taskmasterid <=0;

                    delete from [dbo].[TaskInstance] where taskmasterid <=0;";

            result = con.Query(sql);

            foreach (var testTaskInstance in testTaskInstances)
            {
                JObject processedTaskObject = null;
                try
                {
                    var T = new AdfJsonBaseTask(testTaskInstance, _funcAppLogger);


                    for (int i = 1; i < 5; i++)
                    {
                        var parameters = new
                        {
                            TaskMasterId = (T.TaskMasterId * -1) - (i * 100),
                            TaskMasterName = T.AdfPipeline + T.TaskMasterId.ToString(),
                            TaskTypeId = T.TaskTypeId,
                            TaskGroupId = -1 * i,
                            ScheduleMasterId = 4,
                            SourceSystemId = T.SourceSystemId,
                            TargetSystemId = T.TargetSystemId,
                            DegreeOfCopyParallelism = T.DegreeOfCopyParallelism,
                            AllowMultipleActiveInstances = 0,
                            TaskDatafactoryIR = "Azure",
                            TaskMasterJSON = T.TaskMasterJson,
                            ActiveYN = 1,
                            DependencyChainTag = "",
                            DataFactoryId = T.DataFactoryId
                        };
                        sql = @"
                                        
                    SET IDENTITY_INSERT [dbo].[TaskMaster] ON;
                    insert into [dbo].[TaskMaster]
                    (
                        [TaskMasterId]                          ,
                        [TaskMasterName]                        ,
                        [TaskTypeId]                            ,
                        [TaskGroupId]                           ,
                        [ScheduleMasterId]                      ,
                        [SourceSystemId]                        ,
                        [TargetSystemId]                        ,
                        [DegreeOfCopyParallelism]               ,
                        [AllowMultipleActiveInstances]          ,
                        [TaskDatafactoryIR]                     ,
                        [TaskMasterJSON]                        ,
                        [ActiveYN]                              ,
                        [DependencyChainTag]                    ,
                        [DataFactoryId]                         
                    )
                    select 
                        @TaskMasterId                          ,
                        @TaskMasterName                        ,
                        @TaskTypeId                            ,
                        @TaskGroupId                           ,
                        @ScheduleMasterId                      ,
                        @SourceSystemId                        ,
                        @TargetSystemId                        ,
                        @DegreeOfCopyParallelism               ,
                        @AllowMultipleActiveInstances          ,
                        @TaskDatafactoryIR                     ,
                        @TaskMasterJSON                        ,
                        @ActiveYN                              ,
                        @DependencyChainTag                    ,
                        @DataFactoryId;  
                    SET IDENTITY_INSERT [dbo].[TaskMaster] OFF;";

                        result = con.Query(sql, parameters);

                    }



                    //T.CreateJsonObjectForAdf(executionId);
                    //processedTaskObject = T.ProcessRoot(_taskTypeMappingProvider, _schemasProvider);
                }
                catch (Exception e)
                {
                    _funcAppLogger.LogErrors(e);
                }
                string FileFullPath = "../../../UnitTestResults/Todo/";
                // Determine whether the directory exists.
                if (!Directory.Exists(FileFullPath))
                {
                    // Try to create the directory.
                    var di = Directory.CreateDirectory(FileFullPath);
                }

                if (processedTaskObject != null)
                {
                    JObject obj = new JObject();
                    obj["TaskObject"] = processedTaskObject;

                    FileFullPath = $"{FileFullPath}{testTaskInstance.TaskType}_{testTaskInstance.AdfPipeline}_{testTaskInstance.TaskMasterId}.json";
                    System.IO.File.WriteAllText(FileFullPath, JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                }
            }

        }



        public void DebugPrepareFrameworkTasks()
        {
            FunctionApp.Functions.AdfPrepareFrameworkTasksTimerTrigger c = new FunctionApp.Functions.AdfPrepareFrameworkTasksTimerTrigger(_options, _taskMetaDataDatabase, _dataFactoryPipelineProvider, _taskTypeMappingProvider);
            c.PrepareFrameworkTasksCore(_funcAppLogger);
        }


        public void DebugRunFrameworkTasks()
        {
            FunctionApp.Functions.AdfRunFrameworkTasksHttpTrigger c = new FunctionApp.Functions.AdfRunFrameworkTasksHttpTrigger(_sap,_taskMetaDataDatabase, _options, _authProvider, _dataFactoryClientFactory);
            c.RunFrameworkTasksCore(1, _funcAppLogger);
        }

        

        /// <summary>
        /// 
        /// </summary>
        public void GenerateUnitTestResults()
        {            

            var testTaskInstances = GetTests();

            //Set up table to Store Invalid Task Instance Objects
            DataTable InvalidTIs = new DataTable();
            InvalidTIs.Columns.Add("ExecutionUid", typeof(Guid));
            InvalidTIs.Columns.Add("TaskInstanceId", typeof(long));
            InvalidTIs.Columns.Add("LastExecutionComment", typeof(string));

            foreach (var testTaskInstance in testTaskInstances)
            {
                JObject processedTaskObject = null;
                try
                {
                    var T = new AdfJsonBaseTask(testTaskInstance, _funcAppLogger);
                    T.CreateJsonObjectForAdf((Guid)_funcAppLogger.DefaultActivityLogItem.ExecutionUid);
                    processedTaskObject = T.ProcessRoot(_taskTypeMappingProvider, _schemasProvider);
                }
                catch (Exception e)
                {
                    _funcAppLogger.LogErrors(e);
                }
                string FileFullPath = "../../../UnitTestResults/Todo/";
                // Determine whether the directory exists.
                if (!Directory.Exists(FileFullPath))
                {
                    // Try to create the directory.
                    var di = Directory.CreateDirectory(FileFullPath);
                }

                if (processedTaskObject != null)
                {
                    JObject obj = new JObject();
                    obj["TaskObject"] = processedTaskObject;

                    FileFullPath = $"{FileFullPath}{testTaskInstance.TaskType}_{testTaskInstance.AdfPipeline}_{testTaskInstance.TaskMasterId}.json";
                    File.WriteAllText(FileFullPath, JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                }
            }

        }

        public void Test_GetSQLCreateStatementFromSchema(Logging.Logging _logging)
        {
            string _filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory) + "..\\..\\..\\..\\UnitTests\\GetSQLCreateStatementFromSchema.json";
            string tests = "";
            using (var r = new StreamReader(_filePath))
            {
                tests = r.ReadToEnd();
            }

            //var bytes = Convert.FromBase64String("eyJUYXNrSW5zdGFuY2VJZCI6ODY1LCJUYXNrTWFzdGVySWQiOjEwMiwiVGFza1N0YXR1cyI6IlVudHJpZWQiLCJUYXNrVHlwZSI6IlNRTCBEYXRhYmFzZSB0byBBenVyZSBTdG9yYWdlIiwiRW5hYmxlZCI6MSwiRXhlY3V0aW9uVWlkIjoiZDk1NGNmZWUtNDRjMS00MTlmLTgzZDUtNTFmNzVlYWY4MDljIiwiTnVtYmVyT2ZSZXRyaWVzIjowLCJEZWdyZWVPZkNvcHlQYXJhbGxlbGlzbSI6MSwiS2V5VmF1bHRCYXNlVXJsIjoiaHR0cHM6Ly9hZHNnb2Zhc3RrZXl2YXVsdC52YXVsdC5henVyZS5uZXQvIiwiU2NoZWR1bGVNYXN0ZXJJZCI6MiwiVGFza0dyb3VwQ29uY3VycmVuY3kiOjEwLCJUYXNrR3JvdXBQcmlvcml0eSI6MCwiU291cmNlIjp7IlR5cGUiOiJTUUwgU2VydmVyIiwiRGF0YWJhc2UiOnsiU3lzdGVtTmFtZSI6ImFkc2dvZmFzdC1vbnByZSIsIk5hbWUiOiJBZHZlbnR1cmVXb3JrczIwMTciLCJBdXRoZW50aWNhdGlvblR5cGUiOiJTUUxBdXRoIiwiVXNlcm5hbWUiOiJzcWxhZGZpciIsIlBhc3N3b3JkS2V5VmF1bHRTZWNyZXROYW1lIjoiYWRzZ29mYXN0LW9ucHJlLXNxbGFkZmlyLXBhc3N3b3JkIn0sIkV4dHJhY3Rpb24iOnsiVHlwZSI6IlRhYmxlIiwiRnVsbE9ySW5jcmVtZW50YWwiOiJGdWxsIiwiSW5jcmVtZW50YWxUeXBlIjpudWxsLCJUYWJsZVNjaGVtYSI6IlNhbGVzIiwiVGFibGVOYW1lIjoiU2FsZXNPcmRlckhlYWRlciJ9fSwiVGFyZ2V0Ijp7IlR5cGUiOiJBenVyZSBCbG9iIiwiU3RvcmFnZUFjY291bnROYW1lIjoiaHR0cHM6Ly9hZHNnb2Zhc3RkYXRhbGFrZWFjY2Vsc3QuYmxvYi5jb3JlLndpbmRvd3MubmV0IiwiU3RvcmFnZUFjY291bnRDb250YWluZXIiOiJkYXRhbGFrZXJhdyIsIlN0b3JhZ2VBY2NvdW50QWNjZXNzTWV0aG9kIjoiTVNJIiwiUmVsYXRpdmVQYXRoIjoiQWR2ZW50dXJlV29ya3MyMDE3L1NhbGVzL1NhbGVzT3JkZXJIZWFkZXIvMjAyMC83LzIzLzkvMC8iLCJEYXRhRmlsZU5hbWUiOiJTYWxlcy5TYWxlc09yZGVySGVhZGVyLnBhcnF1ZXQiLCJTY2hlbWFGaWxlTmFtZSI6IlNhbGVzLlNhbGVzT3JkZXJIZWFkZXIuanNvbiIsIkZpcnN0Um93QXNIZWFkZXIiOm51bGwsIlNoZWV0TmFtZSI6bnVsbCwiU2tpcExpbmVDb3VudCI6bnVsbCwiTWF4Q29uY29ycmVudENvbm5lY3Rpb25zIjpudWxsfSwiRGF0YUZhY3RvcnkiOnsiSWQiOjEsIk5hbWUiOiJhZHNnb2Zhc3RkYXRha2FrZWFjY2VsYWRmIiwiUmVzb3VyY2VHcm91cCI6IkFkc0dvRmFzdERhdGFMYWtlQWNjZWwiLCJTdWJzY3JpcHRpb25JZCI6IjAzNWExMzY0LWYwMGQtNDhlMi1iNTgyLTRmZTEyNTkwNWVlMyIsIkFERlBpcGVsaW5lIjoiT25QLVNRTC1BWi1TdG9yYWdlLVBhcnF1ZXQtT25QLVNILUlSIn19");
            //var s = System.Text.Encoding.UTF8.GetString(bytes);



            FunctionApp.Functions.GetSqlCreateStatementFromSchema c = new Functions.GetSqlCreateStatementFromSchema(_options, _authProvider);
            var r1 = c.GetSqlCreateStatementFromSchemaCore(JObject.Parse(tests), _logging).Result;
            //var r1 = c.GetSqlCreateStatementFromSchemaCore(JObject.Parse("{\"TaskObject\": \"eyJUYXNrSW5zdGFuY2VJZCI6ODY1LCJUYXNrTWFzdGVySWQiOjEwMiwiVGFza1N0YXR1cyI6IlVudHJpZWQiLCJUYXNrVHlwZSI6IlNRTCBEYXRhYmFzZSB0byBBenVyZSBTdG9yYWdlIiwiRW5hYmxlZCI6MSwiRXhlY3V0aW9uVWlkIjoiZDk1NGNmZWUtNDRjMS00MTlmLTgzZDUtNTFmNzVlYWY4MDljIiwiTnVtYmVyT2ZSZXRyaWVzIjowLCJEZWdyZWVPZkNvcHlQYXJhbGxlbGlzbSI6MSwiS2V5VmF1bHRCYXNlVXJsIjoiaHR0cHM6Ly9hZHNnb2Zhc3RrZXl2YXVsdC52YXVsdC5henVyZS5uZXQvIiwiU2NoZWR1bGVNYXN0ZXJJZCI6MiwiVGFza0dyb3VwQ29uY3VycmVuY3kiOjEwLCJUYXNrR3JvdXBQcmlvcml0eSI6MCwiU291cmNlIjp7IlR5cGUiOiJTUUwgU2VydmVyIiwiRGF0YWJhc2UiOnsiU3lzdGVtTmFtZSI6ImFkc2dvZmFzdC1vbnByZSIsIk5hbWUiOiJBZHZlbnR1cmVXb3JrczIwMTciLCJBdXRoZW50aWNhdGlvblR5cGUiOiJTUUxBdXRoIiwiVXNlcm5hbWUiOiJzcWxhZGZpciIsIlBhc3N3b3JkS2V5VmF1bHRTZWNyZXROYW1lIjoiYWRzZ29mYXN0LW9ucHJlLXNxbGFkZmlyLXBhc3N3b3JkIn0sIkV4dHJhY3Rpb24iOnsiVHlwZSI6IlRhYmxlIiwiRnVsbE9ySW5jcmVtZW50YWwiOiJGdWxsIiwiSW5jcmVtZW50YWxUeXBlIjpudWxsLCJUYWJsZVNjaGVtYSI6IlNhbGVzIiwiVGFibGVOYW1lIjoiU2FsZXNPcmRlckhlYWRlciJ9fSwiVGFyZ2V0Ijp7IlR5cGUiOiJBenVyZSBCbG9iIiwiU3RvcmFnZUFjY291bnROYW1lIjoiaHR0cHM6Ly9hZHNnb2Zhc3RkYXRhbGFrZWFjY2Vsc3QuYmxvYi5jb3JlLndpbmRvd3MubmV0IiwiU3RvcmFnZUFjY291bnRDb250YWluZXIiOiJkYXRhbGFrZXJhdyIsIlN0b3JhZ2VBY2NvdW50QWNjZXNzTWV0aG9kIjoiTVNJIiwiUmVsYXRpdmVQYXRoIjoiQWR2ZW50dXJlV29ya3MyMDE3L1NhbGVzL1NhbGVzT3JkZXJIZWFkZXIvMjAyMC83LzIzLzkvMC8iLCJEYXRhRmlsZU5hbWUiOiJTYWxlcy5TYWxlc09yZGVySGVhZGVyLnBhcnF1ZXQiLCJTY2hlbWFGaWxlTmFtZSI6IlNhbGVzLlNhbGVzT3JkZXJIZWFkZXIuanNvbiIsIkZpcnN0Um93QXNIZWFkZXIiOm51bGwsIlNoZWV0TmFtZSI6bnVsbCwiU2tpcExpbmVDb3VudCI6bnVsbCwiTWF4Q29uY29ycmVudENvbm5lY3Rpb25zIjpudWxsfSwiRGF0YUZhY3RvcnkiOnsiSWQiOjEsIk5hbWUiOiJhZHNnb2Zhc3RkYXRha2FrZWFjY2VsYWRmIiwiUmVzb3VyY2VHcm91cCI6IkFkc0dvRmFzdERhdGFMYWtlQWNjZWwiLCJTdWJzY3JpcHRpb25JZCI6IjAzNWExMzY0LWYwMGQtNDhlMi1iNTgyLTRmZTEyNTkwNWVlMyIsIkFERlBpcGVsaW5lIjoiT25QLVNRTC1BWi1TdG9yYWdlLVBhcnF1ZXQtT25QLVNILUlSIn19\",\"RunId\":d1c14fa6-abd9-4830-ab8f-56e27c1539ab}"), _logging).Result;

        }

        public void Test_TaskExecutionSchemaFile(Logging.Logging _logging)
        {
            string _filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory) + "..\\..\\..\\..\\UnitTests\\TaskExecutionSchemaFile.json";
            string tests = "";
            using (var r = new StreamReader(_filePath))
            {
                tests = r.ReadToEnd();
            }

            FunctionApp.Functions.TaskExecutionSchemaFile c = new Functions.TaskExecutionSchemaFile(_options, _authProvider);
            var r1 = c.TaskExecutionSchemaFileCore(JObject.Parse(tests), _logging).Result;

        }

        public void Test_GetSourceTargetMapping(Logging.Logging _logging)
        {
            string _filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory) + "..\\..\\..\\..\\UnitTests\\GetSourceTargetMapping.json";
            string tests = "";
            using (var r = new StreamReader(_filePath))
            {
                tests = r.ReadToEnd();
            }

            FunctionApp.Functions.AdfGetSourceTargetMapping c = new Functions.AdfGetSourceTargetMapping(_options, _authProvider);
            var r1 = c.GetSourceTargetMappingCore(JObject.Parse(tests), _logging).Result;

        }


    }
}
