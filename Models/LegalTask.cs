namespace NajGravador.Models;

public enum LegalTaskStatus
{
    Pending,
    InProgress,
    AwaitingClient,
    UnderReview,
    Completed,
    Suspended
}

public sealed class LegalTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Description { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public string ProcessNumber { get; set; } = string.Empty;
    public DateTime InternalDeadline { get; set; } = DateTime.Today;
    public DateTime FinalDeadline { get; set; } = DateTime.Today;
    public string Responsible { get; set; } = string.Empty;
    public string Supervisor { get; set; } = string.Empty;
    public LegalTaskStatus Status { get; set; } = LegalTaskStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public static class LegalTaskStatusInfo
{
    public static string GetName(LegalTaskStatus status) => status switch
    {
        LegalTaskStatus.Pending => "Pendente",
        LegalTaskStatus.InProgress => "Em andamento",
        LegalTaskStatus.AwaitingClient => "Aguardando cliente",
        LegalTaskStatus.UnderReview => "Em revisão",
        LegalTaskStatus.Completed => "Concluída",
        LegalTaskStatus.Suspended => "Suspensa",
        _ => "Pendente"
    };

    public static string GetColor(LegalTaskStatus status) => status switch
    {
        LegalTaskStatus.Pending => "#E56910",
        LegalTaskStatus.InProgress => "#0C66E4",
        LegalTaskStatus.AwaitingClient => "#7F5F01",
        LegalTaskStatus.UnderReview => "#6E5DC6",
        LegalTaskStatus.Completed => "#1F845A",
        LegalTaskStatus.Suspended => "#626F86",
        _ => "#626F86"
    };

    public static string GetBackground(LegalTaskStatus status) => status switch
    {
        LegalTaskStatus.Pending => "#FFF3EB",
        LegalTaskStatus.InProgress => "#E9F2FF",
        LegalTaskStatus.AwaitingClient => "#FFF7D6",
        LegalTaskStatus.UnderReview => "#F3F0FF",
        LegalTaskStatus.Completed => "#E3FCEF",
        LegalTaskStatus.Suspended => "#F1F2F4",
        _ => "#F1F2F4"
    };
}
