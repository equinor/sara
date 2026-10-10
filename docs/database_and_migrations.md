## Database model and EF Core

Our database model is defined in the folder
[`/api/Database`](/api/Database) and we use
[Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) as an
object-relational mapper (O/RM). When making changes to the model, we also need
to create a new
[migration](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
and apply it to our databases.

For PostgreSQL at runtime, Staging and Production (case-insensitive) require
`AppRegIdentity`: `ConnectionString` is excluded after all configuration providers
have been applied, regardless of its position in `Database:AllowedAuthMethods`.
Missing identity configuration or token acquisition failure stops startup without
password fallback. Development/local connection strings (including pgAdmin), Test
and in-memory databases are unchanged. The migration Job's design-time factory
requires `AppRegIdentity` when `Database__RequireAppRegIdentity=true` is set;
it cannot fall back to a connection string.

### Applying migrations in the cluster

Each environment's Argo CD application runs the `sara-migrations` Job as a
PreSync hook before rolling out the API. The Job uses the same version tag as
the API, the environment's `robotics-analytics-sa` Workload Identity and its
`sara-<env>` PostgreSQL role. A failed migration prevents the rollout. Syncing
the same version again is safe: EF Core applies only pending migrations.

When changing migration ownership or permissions, use
`eq_robot_utility_scripts/postgres-db/sara-migration-ownership-inventory.sql`
to review the live database. The role needs database access, `CREATE` on
`public`, and ownership of migration-managed tables (including
`__EFMigrationsHistory`) and sequences.

### Installing EF Core

```bash
dotnet tool install --global dotnet-ef
```

### Adding a new migration

**NB: Make sure you have have fetched the newest code from main and that no-one else
is making migrations at the same time as you!**

1. Set the environment variable `ASPNETCORE_ENVIRONMENT` to `Development`:

   ```bash
    export ASPNETCORE_ENVIRONMENT=Development
   ```

2. Run the following command from `/api`:
   ```bash
     dotnet ef migrations add AddTableNamePropertyName
   ```
   `add` will make changes to existing files and add 2 new files in
   `/api/Migrations`, which all need to be checked in to git.

### Notes

- The `your-migration-name-here` is basically a database commit message.
- `Database__ConnectionString` will be fetched from the keyvault when running the `add` command.
- `add` will _not_ update or alter the connected database in any way, but will add a
  description of the changes that will be applied later
- If you for some reason are unhappy with your migration, you can delete it with:
  ```bash
  dotnet ef migrations remove
  ```
  Once removed you can make new changes to the model
  and then create a new migration with `add`.

### Deploying migrations

Pull requests that change `/api/Migrations` trigger a notification and migration
validation against a temporary database. After merging, the Development deploy
workflow builds matching API and migration images and updates their tags in
robotics-infrastructure. Argo CD applies pending migrations during the PreSync
hook. The Staging release and Production promotion workflows likewise publish
matching images and update both tags; Argo CD runs each environment's hook
before deploying the new API version. Check the Argo CD sync result and hook
logs to confirm migrations succeeded.

The migration runner reports each applied migration's ID, duration and declared
schema operations to its console and as OpenTelemetry logs under the
`sara-migrations` service. Custom SQL is reported as present without inferring
its effects on data. An up-to-date database produces an info log; failures log
the migration ID and exception and fail the Job. In Grafana's Backends log panel,
select `sara-backend` and `sara-migrations` together to view deployment logs in
one timeline. Check a time range covering the PreSync Job when looking for its
short-lived logs.
