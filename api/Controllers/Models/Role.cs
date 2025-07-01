namespace api.Controllers.Models;

public class Role
{
    private const string ReadOnlyRole = "Role.ReadOnly";
    private const string UserRole = "Role.User";
    private const string AdminRole = "Role.Admin";
    public const string PlantDataRead = "PlantData.Read";
    private const string WorkflowStatusWriteRole = "WorkflowStatus.Write";

    /// <summary>
    /// The user must be an admin
    /// </summary>
    public const string Admin = AdminRole;

    /// <summary>
    /// The user must at least have the user role
    /// </summary>
    public const string User = $"{UserRole}, {Admin}";

    /// <summary>
    /// Any role is accepted.
    /// <para>
    /// <see cref="ReadOnlyRole"/> is the lowest access level role
    /// </para>
    /// </summary>
    public const string Any = $"{ReadOnlyRole}, {UserRole}, {Admin}, {PlantDataRead}";

    /// <summary>
    /// Role required to update workflow statuses
    /// </summary>
    public const string WorkflowStatusWrite = WorkflowStatusWriteRole;
}
