namespace Tennis.Api.Data.Entities;

public class ImportBatch
{
    public Guid Id { get; set; }
    public string Mode { get; set; } = "";
    public string Provenance { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int FilesProcessed { get; set; }
    public int RowsRead { get; set; }
    public int MatchesInserted { get; set; }
    public int PlayersInserted { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int InvalidRowsSkipped { get; set; }
    public int MissingStatisticCells { get; set; }
    public string? Error { get; set; }
    public List<ImportFileRecord> Files { get; set; } = new();
}
