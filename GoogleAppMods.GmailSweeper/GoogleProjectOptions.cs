namespace GoogleAppMods.GmailSweeper;

public class GoogleProjectOptions
{
    public const string SectionName = "GoogleProject";

    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string TokenStorePath { get; set; }
}
