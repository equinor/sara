## Database model and EF Core

Our database model is defined in the folder
[`/api/Database`](/api/Database) and we use
[Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) as an
object-relational mapper (O/RM). When making changes to the model, we also need
to create a new
[migration](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
and apply it to our databases.

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
[a workflow](https://github.com/equinor/sara/blob/main/.github/workflows/notifyMigrationChanges.yml)
is triggered to notify that the pull request has database changes.

After the pull request is approved, a user can then trigger the database changes by commenting
`/UpdateDatabase` on the pull request.

This will trigger
[another workflow](https://github.com/equinor/sara/blob/main/.github/workflows/updateDatabase.yml)
which updates the database by apploying the new migrations.

By doing migrations this way, we ensure that the commands themselves are scripted, and that the database
changes become part of the review process of a pull request.

### Applying migrations to staging and production databases

This is done automatically as part of the promotion workflows
([promote_to_production](https://github.com/equinor/sara/blob/main/.github/workflows/promote_to_production.yaml)
and [promote_to_staging](https://github.com/equinor/sara/blob/main/.github/workflows/deploy_to_staging.yml).
