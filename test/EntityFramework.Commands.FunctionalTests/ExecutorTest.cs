﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET451

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Entity.Commands.TestUtilities;
using Microsoft.Data.Entity.SqlServer.FunctionalTests;
using Xunit;

namespace Microsoft.Data.Entity.Commands
{
    public class ExecutorTest
    {
        public class SimpleProjectTest : IClassFixture<SimpleProjectTest.SimpleProject>
        {
            private readonly SimpleProject _project;

            public SimpleProjectTest(SimpleProject project)
            {
                _project = project;
            }

            [Fact]
            public void GetContextType_works_cross_domain()
            {
                var contextTypeName = _project.Executor.GetContextType("SimpleContext");
                Assert.StartsWith("SimpleProject.SimpleContext, ", contextTypeName);
            }

            [Fact]
            public void AddMigration_works_cross_domain()
            {
                var artifacts = _project.Executor.AddMigration("EmptyMigration", "SimpleContext");
                Assert.Equal(3, artifacts.Count());
            }

            [Fact]
            public async Task ApplyMigration_works_cross_domain()
            {
                try
                {
                    _project.Executor.ApplyMigration("InitialCreate", "SimpleContext");
                    Assert.True(await _project.TestStore.ExistsAsync());
                }
                finally
                {
                    await _project.TestStore.DeleteDatabaseAsync();
                }
            }

            [Fact]
            public void ScriptMigration_works_cross_domain()
            {
                var sql = _project.Executor.ScriptMigration(null, "InitialCreate", false, "SimpleContext");
                Assert.NotEmpty(sql);
            }

            [Fact]
            public void GetContextTypes_works_cross_domain()
            {
                var contextTypes = _project.Executor.GetContextTypes();
                Assert.Equal(1, contextTypes.Count());
            }

            [Fact]
            public void GetMigrations_works_cross_domain()
            {
                var migrations = _project.Executor.GetMigrations("SimpleContext");
                Assert.Equal(1, migrations.Count());
            }

            public class SimpleProject : IDisposable
            {
                private readonly TempDirectory _directory = new TempDirectory();

                public SimpleProject()
                {
                    TestStore = SqlServerTestStore.CreateScratchAsync(createDatabase: false).Result;

                    var source = new BuildSource
                    {
                        TargetDir = TargetDir,
                        References =
                            {
                                BuildReference.ByName("System.Collections.Immutable", copyLocal: true),
                                BuildReference.ByName("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                                BuildReference.ByName("System.Data.Common", copyLocal: true),
                                BuildReference.ByName("System.Interactive.Async", copyLocal: true),
                                BuildReference.ByName("System.Runtime, Version=4.0.10.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                                BuildReference.ByName("EntityFramework.Core", copyLocal: true),
                                BuildReference.ByName("EntityFramework.Commands", copyLocal: true),
                                BuildReference.ByName("EntityFramework.Migrations", copyLocal: true),
                                BuildReference.ByName("EntityFramework.Relational", copyLocal: true),
                                BuildReference.ByName("EntityFramework.SqlServer", copyLocal: true),
                                BuildReference.ByName("Microsoft.Framework.ConfigurationModel", copyLocal: true),
                                BuildReference.ByName("Microsoft.Framework.DependencyInjection", copyLocal: true),
                                BuildReference.ByName("Microsoft.Framework.Logging", copyLocal: true),
                                BuildReference.ByName("Microsoft.Framework.Logging.Interfaces", copyLocal: true),
                                BuildReference.ByName("Microsoft.Framework.OptionsModel", copyLocal: true),
                                BuildReference.ByName("Remotion.Linq", copyLocal: true)
                            },
                        Source = @"
                        using System;
                        using Microsoft.Data.Entity;
                        using Microsoft.Data.Entity.Metadata;
                        using Microsoft.Data.Entity.Migrations;
                        using Microsoft.Data.Entity.Migrations.Builders;
                        using Microsoft.Data.Entity.Migrations.Infrastructure;
                        using Microsoft.Data.Entity.Migrations.Model;

                        namespace SimpleProject
                        {
                            internal class SimpleContext : DbContext
                            {
                                protected override void OnConfiguring(DbContextOptions options)
                                {
                                    options.UseSqlServer(
                                        @""" + TestStore.Connection.ConnectionString + @""");
                                }
                            }

                            namespace Migrations
                            {
                                [ContextType(typeof(SimpleContext))]
                                public class InitialCreate : Migration, IMigrationMetadata
                                {
                                    public string MigrationId => ""201410102227260_InitialCreate"";
                                    public string ProductVersion => ""7.0.0"";
                                    public IModel TargetModel => new SimpleContext().Model;

                                    public override void Up(MigrationBuilder migrationBuilder)
                                    {
                                    }

                                    public override void Down(MigrationBuilder migrationBuilder)
                                    {
                                    }
                                }
                            }
                        }
                    "
                    };
                    var build = source.Build();
                    Executor = new ExecutorWrapper(TargetDir, build.TargetName + ".dll", TargetDir, "SimpleProject");
                }

                public string TargetDir => _directory.Path;
                public SqlServerTestStore TestStore { get; }
                public ExecutorWrapper Executor { get; }

                public void Dispose()
                {
                    Executor.Dispose();
                    _directory.Dispose();
                }
            }
        }

        [Fact]
        public void GetMigrations_filters_by_context_name()
        {
            using (var directory = new TempDirectory())
            {
                var targetDir = directory.Path;
                var source = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                        {
                            BuildReference.ByName("System.Runtime, Version=4.0.10.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                            BuildReference.ByName("EntityFramework.Core", copyLocal: true),
                            BuildReference.ByName("EntityFramework.Commands", copyLocal: true),
                            BuildReference.ByName("EntityFramework.Migrations", copyLocal: true),
                            BuildReference.ByName("Microsoft.Framework.Logging", copyLocal: true),
                            BuildReference.ByName("Microsoft.Framework.Logging.Interfaces", copyLocal: true)
                        },
                    Source = @"
                        using System;
                        using Microsoft.Data.Entity;
                        using Microsoft.Data.Entity.Metadata;
                        using Microsoft.Data.Entity.Migrations;
                        using Microsoft.Data.Entity.Migrations.Builders;
                        using Microsoft.Data.Entity.Migrations.Infrastructure;
                        using Microsoft.Data.Entity.Migrations.Model;

                        namespace MyProject
                        {
                            internal class Context1 : DbContext
                            {
                            }

                            internal class Context2 : DbContext
                            {
                            }

                            namespace Migrations
                            {
                                namespace Context1Migrations
                                {
                                    [ContextType(typeof(Context1))]
                                    public class Context1Migration : Migration, IMigrationMetadata
                                    {
                                        public string MigrationId
                                        {
                                            get { return ""000000000000000_Context1Migration""; }
                                        }

                                        public string ProductVersion
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public IModel TargetModel
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }

                                        public override void Down(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }

                                namespace Context2Migrations
                                {
                                    [ContextType(typeof(Context2))]
                                    public class Context2Migration : Migration, IMigrationMetadata
                                    {
                                        public string MigrationId
                                        {
                                            get { return ""000000000000000_Context2Migration""; }
                                        }

                                        public string ProductVersion
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public IModel TargetModel
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }

                                        public override void Down(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }
                            }
                        }
                    "
                };
                var build = source.Build();
                using (var executor = new ExecutorWrapper(targetDir, build.TargetName + ".dll", targetDir, "MyProject"))
                {
                    var migrations = executor.GetMigrations("Context1");

                    Assert.Equal(1, migrations.Count());
                }
            }
        }

        [Fact]
        public void GetContextType_works_with_multiple_assemblies()
        {
            using (var directory = new TempDirectory())
            {
                var targetDir = directory.Path;
                var contextsSource = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                        {
                            BuildReference.ByName("System.Runtime, Version=4.0.10.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                            BuildReference.ByName("EntityFramework.Core", copyLocal: true),
                            BuildReference.ByName("EntityFramework.Commands", copyLocal: true)
                        },
                    Source = @"
                        using Microsoft.Data.Entity;

                        namespace MyProject
                        {
                            public class Context1 : DbContext
                            {
                            }

                            public class Context2 : DbContext
                            {
                            }
                        }
                    "
                };
                var contextsBuild = contextsSource.Build();
                var migrationsSource = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                        {
                            BuildReference.ByName("System.Runtime, Version=4.0.10.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                            BuildReference.ByName("EntityFramework.Core"),
                            BuildReference.ByName("EntityFramework.Migrations", copyLocal: true),
                            BuildReference.ByName("Microsoft.Framework.Logging", copyLocal: true),
                            BuildReference.ByName("Microsoft.Framework.Logging.Interfaces", copyLocal: true),
                            BuildReference.ByPath(contextsBuild.TargetPath)
                        },
                    Source = @"
                        using System;
                        using Microsoft.Data.Entity;
                        using Microsoft.Data.Entity.Metadata;
                        using Microsoft.Data.Entity.Migrations;
                        using Microsoft.Data.Entity.Migrations.Builders;
                        using Microsoft.Data.Entity.Migrations.Infrastructure;
                        using Microsoft.Data.Entity.Migrations.Model;

                        namespace MyProject
                        {
                            internal class Context3 : DbContext
                            {
                            }

                            namespace Migrations
                            {
                                namespace Context1Migrations
                                {
                                    [ContextType(typeof(Context1))]
                                    public class Context1Migration : Migration, IMigrationMetadata
                                    {
                                        public string MigrationId
                                        {
                                            get { return ""000000000000000_Context1Migration""; }
                                        }

                                        public string ProductVersion
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public IModel TargetModel
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }

                                        public override void Down(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }

                                namespace Context2Migrations
                                {
                                    [ContextType(typeof(Context2))]
                                    public class Context2Migration : Migration, IMigrationMetadata
                                    {
                                        public string MigrationId
                                        {
                                            get { return ""000000000000000_Context2Migration""; }
                                        }

                                        public string ProductVersion
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public IModel TargetModel
                                        {
                                            get { throw new NotImplementedException(); }
                                        }

                                        public override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
        
                                        public override void Down(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }
                            }
                        }
                    "
                };
                var migrationsBuild = migrationsSource.Build();
                using (var executor = new ExecutorWrapper(targetDir, migrationsBuild.TargetName + ".dll", targetDir, "MyProject"))
                {
                    var contextTypes = executor.GetContextTypes();

                    Assert.Equal(3, contextTypes.Count());
                }
            }
        }
    }
}

#endif
