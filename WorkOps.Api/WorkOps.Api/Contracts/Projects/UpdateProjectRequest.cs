using System.ComponentModel.DataAnnotations;

namespace WorkOps.Api.Contracts.Projects;

public class UpdateProjectRequest : IValidatableObject
{
    public string Name { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var name = (Name ?? "").Trim();
        if (name.Length == 0)
            yield return new ValidationResult("Name is required.");
        else if (name.Length > 200)
            yield return new ValidationResult("Name must be at most 200 characters.");
    }
}
