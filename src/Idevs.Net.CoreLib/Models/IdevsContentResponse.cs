using Serenity.Services;

namespace Idevs.Models;

public class IdevsContentResponse : ServiceResponse
{
    public string? Content { get; set; }
    public string? ContentType { get; set; }
    public string? DownloadName { get; set; }
}
