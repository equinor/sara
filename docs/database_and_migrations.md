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
and in-memory databases are unchanged. EF Core migrations use the separate
design-time authentication below; runtime credentials and method arrays are
unchanged.

### Design-time migration authentication

Migrations default to `AzureCli`. The legacy authentication chain, Key Vault
lookup and automatic password fallback are removed. Mode names are
case-insensitive; `Legacy`, empty, padded or unknown values fail.

Set `Migrations__Postgres__Host` (DNS hostname/IP, no port),
`Migrations__Postgres__Database`, `Migrations__Postgres__Username` and
`AZURE_TENANT_ID` (tenant GUID). It uses only the `azure/login` or local `az login`
session, with no runtime identity, Key Vault or password fallback. The factory
reads JSON/environment variables, not `.env`; local login still needs database
permissions and network access.

For local PostgreSQL or disposable CI databases only, explicitly set
`Migrations__AuthenticationMode=LocalConnectionString` and
`Database__postgresConnectionString`. This requires `ASPNETCORE_ENVIRONMENT`
to be `Local`, `Development`, `IntegrationTest` or `Test`; it never reads Key Vault.
Deployed CI migrations always force `AzureCli`, including Development.

Connections use `VerifyFull` TLS and acquire PostgreSQL tokens as physical
connections authenticate, with a 30-second timeout and async cancellation.
Context disposal releases the data source. Expected CLI errors omit sensitive
diagnostics. EF's short-lived admin clones snapshot a fresh token; the main
migration connection retains the token provider.

The shared workflow checks `api/.migration-auth-contract` (exactly
`azure-cli-postgresql-v1` plus one LF) before login/build/EF; unsupported release
tags fail at this gate. **This is a breaking cutover**, not an opt-in prerequisite:
coordinate the shared workflow and supported app releases, provision the
dedicated migration identity, database roles/catalog ownership and required
environment variables, and confirm network access before merging/running.
Runtime authentication and release gates are unchanged.

### Installing EF Core

```bash
dotnet tool install --global dotnet-ef
```

### Adding a new migration

**NB: Make sure you have have fetched the newest code from main and that no-one else
is making migrations at the same time as you!**

1. Configure one of the migration authentication paths above. For Azure
   PostgreSQL, sign in using `az login --tenant <tenant-id>` and set the dedicated
   database/tenant variables. Set `ASPNETCORE_ENVIRONMENT` to `Development`:

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
