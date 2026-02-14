using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class BaseDto
{
    public MaskedGuid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class BaseCreateRequest
{
}

public class BaseUpdateRequest
{
    public MaskedGuid? Id { get; set; }
}
