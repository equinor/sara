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
and in-memory databases are unchanged. EF Core's separate design-time factory
still honors the configured method order unless CI sets
`Database__RequireAppRegIdentity=true`. That setting requires `AppRegIdentity`
regardless of the configured array and fails without a connection-string
fallback. Until the migration cutover, CI continues using the admin connection
string. The runtime restriction does not remove or change the underlying credentials.

### GitHub Actions migration identity cutover

The migration jobs use `armada/.github/workflows/run_dotnet_migrations.yml@main`.
The shared Armada workflow already accepts `use_app_reg_identity`; deploy SARA's
callers and design-time factory next. The input defaults to `false`, preserving
the existing admin-connection-string migration path. No GitHub variable needs
to be created during preparation.

Before enabling identity migrations in an environment, transfer the reviewed
SARA migration-managed tables (including `__EFMigrationsHistory`) and sequences
to its `sara-<env>` PostgreSQL role, grant that role `CREATE` on `public`, and
verify its database access and runtime grants. Use
`eq_robot_utility_scripts/postgres-db/sara-migration-ownership-inventory.sql`
to review the live database before each cutover. The adjacent ownership guide
describes a *different*, dedicated migration identity; transfer to the
existing `sara-<env>` role for this workflow. The GitHub OIDC credentials
on the existing `sara-dev`, `sara-staging`, and `sara-prod` app registrations
must match the respective GitHub environments.

After ownership and privileges are verified, set the following **repository**
GitHub Actions variables to the literal string `true`, one environment at a
time. Unset or any other value keeps the old migration path. Both development
migration workflows use the same switch. Ensure the SARA ref checked out by
each release/promotion contains the updated design-time factory before setting
its switch. Once objects have changed owner, leaving the switch off can make
subsequent migrations fail because `postgresuser` no longer owns those objects.

| Environment | Repository variable | PostgreSQL role |
| --- | --- | --- |
| Development | `SARA_DEV_MIGRATIONS_USE_APP_REG_IDENTITY` | `sara-dev` |
| Staging | `SARA_STAGING_MIGRATIONS_USE_APP_REG_IDENTITY` | `sara-staging` |
| Production | `SARA_PROD_MIGRATIONS_USE_APP_REG_IDENTITY` | `sara-prod` |

In identity mode the shared workflow's Azure OIDC login supplies the CLI
credential used by the design-time factory to obtain a PostgreSQL token. It
sets `Database__RequireAppRegIdentity=true`, so even a later
`ConnectionString` entry in appsettings cannot provide a fallback. Validate a
development migration and same-version rerun before enabling staging, then
production. Do not enable an environment's switch before its ownership cutover.

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

### Applying the migrations to the dev database

Updates to the database structure (applying migrations) are done in Github Actions.

When a pull request contains changes in the `/api/Migrations` folder,
a workflow is triggered to notify that the pull request has database changes.

After the pull request is merged, apply the migrations to the Development database by
manually running the
["Run database migrations (Development)"](https://github.com/equinor/sara/actions/workflows/run_development_migrations.yml)
workflow from the Actions tab.

### Applying migrations to staging and production databases

This is done automatically as part of the promotion workflows
([promote_to_production](https://github.com/equinor/sara/blob/main/.github/workflows/promote_to_production.yaml)
and [promote_to_staging](https://github.com/equinor/sara/blob/main/.github/workflows/deploy_to_staging.yml).
