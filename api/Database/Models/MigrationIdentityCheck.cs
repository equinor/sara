namespace api.Database.Models;

// Temporary table for verifying that GitHub Actions can run EF migrations as the app registration.
public class MigrationIdentityCheck
{
    public Guid Id { get; set; }
}
