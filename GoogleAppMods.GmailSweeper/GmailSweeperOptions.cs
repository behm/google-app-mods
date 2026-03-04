using System.ComponentModel.DataAnnotations;

namespace GoogleAppMods.GmailSweeper;

public class GmailSweeperOptions
{
    public const string SectionName = "GmailSweeper";

    public string Schedule { get; set; } = "0 12 * * *";
    public List<string> Queries { get; set; } = [];

    [Range(1, 1000, ErrorMessage = "BatchSize must be between 1 and 1000 (Gmail batchModify limit).")]
    public int BatchSize { get; set; } = 100;
}
