namespace Tennis.Api.Data.Entities;

public class ImportFileRecord
{
    public Guid Id { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public int? SourceYear { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long ByteSize { get; set; }
    public string Provenance { get; set; } = "";
    public string HeaderJson { get; set; } = "[]";
    public string UnmappedColumns { get; set; } = "[]";
    public string Status { get; set; } = "";
    public int RowsRead { get; set; }
    public int MatchesInserted { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int InvalidRowsSkipped { get; set; }
    public int MissingStatisticCells { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public List<Match> Matches { get; set; } = new();
}
