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
still honors the configured method order, including CI's `ConnectionString`
override for admin-owned migrations and development migration fallback. The
runtime restriction does not remove or change the underlying credentials.

### Design-time migration authentication

Unset or `Legacy` `Migrations:AuthenticationMode` preserves existing local/CI
authentication and password fallback. Modes are case-insensitive; empty, padded
or unknown values fail.

For CLI authentication, set `Migrations__AuthenticationMode=AzureCli`,
`Migrations__Postgres__Host` (DNS hostname/IP, no port),
`Migrations__Postgres__Database`, `Migrations__Postgres__Username` and
`AZURE_TENANT_ID` (tenant GUID). It uses only the `azure/login` or local `az login`
session, with no runtime identity, Key Vault or password fallback. The factory
reads JSON/environment variables, not `.env`; local login still needs database
permissions and network access.

Connections use `VerifyFull` TLS and acquire PostgreSQL tokens as physical
connections authenticate, with a 30-second timeout and async cancellation.
Context disposal releases the data source. Expected CLI errors omit sensitive
diagnostics. EF's short-lived admin clones snapshot a fresh token; the main
migration connection retains the token provider.

The shared workflow checks `api/.migration-auth-contract` (exactly
`azure-cli-postgresql-v1` plus one LF) before login/build/EF. No caller opts in yet:
activation requires a provisioned database, dedicated migration identity,
catalog ownership/bootstrap and network checks, then a separate development
opt-in. Runtime authentication and release gates are unchanged.

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
