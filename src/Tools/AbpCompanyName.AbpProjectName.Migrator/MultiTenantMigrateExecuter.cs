using System;
using System.Collections.Generic;
using System.Configuration;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Extensions;
using Abp.MultiTenancy;
using AbpCompanyName.AbpProjectName.MultiTenancy;

namespace AbpCompanyName.AbpProjectName.Migrator
{
    public class MultiTenantMigrateExecuter : ITransientDependency
    {
        public Log Log { get; private set; }

        private readonly IAbpZeroDbMigrator _migrator;
        private readonly IRepository<Tenant> _tenantRepository;

        public MultiTenantMigrateExecuter(
            IAbpZeroDbMigrator migrator, 
            IRepository<Tenant> tenantRepository,
            Log log)
        {
            Log = log;

            _migrator = migrator;
            _tenantRepository = tenantRepository;
        }

        public void Run(bool skipConnVerification)
        {
            var connStr = ConfigurationManager.ConnectionStrings["Default"];
            if (connStr == null || connStr.ConnectionString.IsNullOrWhiteSpace())
            {
                Log.Write("Configuration file should contain a connection string named 'Default'");
                return;
            }

            Log.Write("Host database: " + connStr.ConnectionString);
            if (!skipConnVerification)
            {
                Log.Write("Continue to migration for this host database and all tenants..? (Y/N): ");
                var command = Console.ReadLine();
                if (!command.IsIn("Y", "y"))
                {
                    Log.Write("Migration canceled.");
                    return;
                }
            }

            Log.Write("HOST database migration started...");

            try
            {
                _migrator.CreateOrMigrateForHost();
            }
            catch (Exception ex)
            {
                Log.Write("An error occured during migration of host database:");
                Log.Write(ex.ToString());
                Log.Write("Canceled migrations.");
                return;
            }

            Log.Write("HOST database migration completed.");
            Log.Write("--------------------------------------------------------");

            var migratedDatabases = new HashSet<string>();
            var tenants = _tenantRepository.GetAllList(t => t.ConnectionString != null && t.ConnectionString != "");
            for (int i = 0; i < tenants.Count; i++)
            {
                var tenant = tenants[i];
                Log.Write(string.Format("Tenant database migration started... ({0} / {1})", (i + 1), tenants.Count));
                Log.Write("Name              : " + tenant.Name);
                Log.Write("TenancyName       : " + tenant.TenancyName);
                Log.Write("Tenant Id         : " + tenant.Id);
                Log.Write("Connection string : " + tenant.ConnectionString);

                if (!migratedDatabases.Contains(tenant.ConnectionString))
                {
                    try
                    {
                        _migrator.CreateOrMigrateForTenant(tenant);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("An error occured during migration of tenant database:");
                        Log.Write(ex.ToString());
                        Log.Write("Skipped this tenant and will continue for others...");
                    }

                    migratedDatabases.Add(tenant.ConnectionString);
                }
                else
                {
                    Log.Write("This database has already migrated before (you have more than one tenant in same database). Skipping it....");
                }

                Log.Write(string.Format("Tenant database migration completed. ({0} / {1})", (i + 1), tenants.Count));
                Log.Write("--------------------------------------------------------");
            }

            Log.Write("All databases have been migrated.");
        }
    }
}