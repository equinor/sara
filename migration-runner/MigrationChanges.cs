using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MigrationRunner;

public static class MigrationChanges
{
    public static string Describe(IReadOnlyList<MigrationOperation> operations)
    {
        var changes = new List<string>();
        foreach (var operation in operations)
        {
            changes.Add(
                operation switch
                {
                    CreateTableOperation table => $"created table {table.Name}",
                    DropTableOperation table => $"dropped table {table.Name}",
                    AddColumnOperation column => $"added column {column.Table}.{column.Name}",
                    DropColumnOperation column => $"dropped column {column.Table}.{column.Name}",
                    AlterColumnOperation column => $"altered column {column.Table}.{column.Name}",
                    RenameColumnOperation column =>
                        $"renamed column {column.Table}.{column.Name} to {column.NewName}",
                    RenameTableOperation table => $"renamed table {table.Name} to {table.NewName}",
                    CreateIndexOperation index => $"created index {index.Name} on {index.Table}",
                    DropIndexOperation index => $"dropped index {index.Name}",
                    SqlOperation => "custom SQL present",
                    _ => $"{operation.GetType().Name} present",
                }
            );
        }

        return changes.Count == 0 ? "no schema operations" : string.Join("; ", changes.Distinct());
    }
}
