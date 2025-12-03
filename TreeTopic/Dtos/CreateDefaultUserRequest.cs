using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class CreateDefaultUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
