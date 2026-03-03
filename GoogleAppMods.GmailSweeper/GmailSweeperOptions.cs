namespace GoogleAppMods.GmailSweeper;

public class GmailSweeperOptions
{
    public const string SectionName = "GmailSweeper";

    public string Schedule { get; set; } = "* * * * *"; //"0 12 * * *";
    public List<string> Queries { get; set; } = [];
    public int BatchSize { get; set; } = 100;
}
