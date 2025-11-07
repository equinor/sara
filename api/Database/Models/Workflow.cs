using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public class AnonymizerResult
{
    public required bool IsPersonInImage { get; set; }
}

public class CLOEResult
{
    public required float OilLevel { get; set; }
}

public class FencillaResult
{
    public required bool IsBreak { get; set; }
    public required float Confidence { get; set; }
}

public enum WorkflowStatus
{
    NotStarted,
    Started,
    ExitSuccess,
    ExitFailure,
}


public class Workflow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required BlobStorageLocation SourceBlobStorageLocation { get; set; } // Usuallty the output from the Anonymizer

    [Required]
    public required BlobStorageLocation DestinationBlobStorageLocation { get; set; }

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public WorkflowStatus Status { get; set; } = WorkflowStatus.NotStarted;

}

public class Anonymization : Workflow
{
    public AnonymizerResult? Result { get; set; }
}

public class CLOEAnalysis : Workflow
{
    public CLOEResult? Result { get; set; }
}

public class FencillaAnalysis : Workflow
{
    public FencillaResult? Result { get; set; }
}
