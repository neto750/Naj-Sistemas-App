using System.Text.Json;
using NajGravador.Models;

namespace NajGravador.Services;

public sealed class LegalTaskRepository
{
    private const string FileName = "legal_tasks.json";
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

    public async Task<List<LegalTask>> GetAllAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(FilePath))
            {
                var examples = CreateExamples();
                await WriteUnsafeAsync(examples);
                return examples;
            }

            var tasks = await ReadUnsafeAsync();
            if (ApplyExampleMetadataMigration(tasks))
                await WriteUnsafeAsync(tasks);
            return tasks;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(LegalTask legalTask)
    {
        await _fileLock.WaitAsync();
        try
        {
            var tasks = await ReadUnsafeAsync();
            var index = tasks.FindIndex(item => item.Id == legalTask.Id);
            legalTask.UpdatedAt = DateTime.Now;
            if (index >= 0)
                tasks[index] = legalTask;
            else
                tasks.Add(legalTask);
            await WriteUnsafeAsync(tasks);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteAsync(string taskId)
    {
        await _fileLock.WaitAsync();
        try
        {
            var tasks = await ReadUnsafeAsync();
            tasks.RemoveAll(item => item.Id == taskId);
            await WriteUnsafeAsync(tasks);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<LegalTask>> ReadUnsafeAsync()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(FilePath);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<LegalTask>>(json, _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private Task WriteUnsafeAsync(List<LegalTask> tasks) =>
        File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(tasks, _jsonOptions));

    private static List<LegalTask> CreateExamples()
    {
        var today = DateTime.Today;
        return
        [
            New("Analisar documentos recebidos e definir os fundamentos da petição inicial.", "Gerovaldo Junior", "Seguradora Boreal S.A.", "1028457-31.2026.8.26.0100", today.AddDays(1), today.AddDays(4), "Georgina da Silva", "Helena Costa", LegalTaskStatus.InProgress),
            New("Redigir petição inicial da ação de indenização e encaminhar para revisão.", "Marina Oliveira", "Banco Mercantil do Sul", "5019372-48.2026.4.03.6100", today.AddDays(2), today.AddDays(6), "Rafael Martins", "Marcelo Azevedo", LegalTaskStatus.UnderReview),
            New("Elaborar e protocolar contestação com documentos comprobatórios.", "Comercial Aurora Ltda.", "Felipe Moreira", "0007841-62.2025.5.02.0038", today.AddDays(-1), today.AddDays(1), "Camila Nogueira", "Helena Costa", LegalTaskStatus.InProgress),
            New("Preparar roteiro, documentos e orientações para audiência de conciliação.", "Carlos Henrique Souza", "Imobiliária Parque Ltda.", "1015639-07.2025.8.26.0002", today.AddDays(3), today.AddDays(8), "Georgina da Silva", "Helena Costa", LegalTaskStatus.Pending),
            New("Elaborar recurso de apelação contra a sentença e revisar os pedidos.", "Beatriz Almeida", "Município de Brasília", "0704921-84.2024.8.07.0001", today.AddDays(4), today.AddDays(9), "Lucas Fernandes", "Marcelo Azevedo", LegalTaskStatus.InProgress),
            New("Conferir publicações no Diário de Justiça e registrar novos prazos processuais.", "Grupo Horizonte S.A.", "Maria das Dores", "5032186-15.2025.4.04.7100", today, today.AddDays(2), "Camila Nogueira", "Helena Costa", LegalTaskStatus.Completed),
            New("Solicitar documentos pendentes ao cliente para instrução do processo.", "Patrícia Mendes", "Instituto Nacional do Seguro Social", "1009274-56.2026.8.13.0024", today.AddDays(2), today.AddDays(7), "Rafael Martins", "Marcelo Azevedo", LegalTaskStatus.AwaitingClient),
            New("Revisar minuta contratual e apontar cláusulas de risco para negociação.", "Construtora Vale Verde", "", "", today.AddDays(5), today.AddDays(10), "Ana Paula Ribeiro", "Helena Costa", LegalTaskStatus.UnderReview),
            New("Realizar pesquisa jurisprudencial sobre responsabilidade civil e preparar relatório.", "Eduardo Santos", "Transportadora Via Sul", "0014286-93.2025.8.19.0209", today.AddDays(1), today.AddDays(5), "Lucas Fernandes", "Marcelo Azevedo", LegalTaskStatus.Pending),
            New("Atualizar o cliente sobre o andamento processual e registrar as orientações prestadas.", "Clínica Bem Viver Ltda.", "Operadora Saúde Integral", "1003648-29.2024.8.21.0001", today.AddDays(6), today.AddDays(12), "Ana Paula Ribeiro", "Helena Costa", LegalTaskStatus.Suspended)
        ];
    }

    private static LegalTask New(
        string description,
        string client,
        string counterparty,
        string processNumber,
        DateTime internalDeadline,
        DateTime finalDeadline,
        string responsible,
        string supervisor,
        LegalTaskStatus status) => new()
        {
            Description = description,
            Client = client,
            Counterparty = counterparty,
            ProcessNumber = processNumber,
            InternalDeadline = internalDeadline,
            FinalDeadline = finalDeadline,
            Responsible = responsible,
            Supervisor = supervisor,
            Status = status
        };

    private static bool ApplyExampleMetadataMigration(IEnumerable<LegalTask> tasks)
    {
        var examples = new Dictionary<string, (string Counterparty, string Supervisor)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gerovaldo Junior"] = ("Seguradora Boreal S.A.", "Helena Costa"),
            ["Marina Oliveira"] = ("Banco Mercantil do Sul", "Marcelo Azevedo"),
            ["Comercial Aurora Ltda."] = ("Felipe Moreira", "Helena Costa"),
            ["Carlos Henrique Souza"] = ("Imobiliária Parque Ltda.", "Helena Costa"),
            ["Beatriz Almeida"] = ("Município de Brasília", "Marcelo Azevedo"),
            ["Grupo Horizonte S.A."] = ("Maria das Dores", "Helena Costa"),
            ["Patrícia Mendes"] = ("Instituto Nacional do Seguro Social", "Marcelo Azevedo"),
            ["Construtora Vale Verde"] = ("Fornecedora Atlas", "Helena Costa"),
            ["Eduardo Santos"] = ("Transportadora Via Sul", "Marcelo Azevedo"),
            ["Clínica Bem Viver Ltda."] = ("Operadora Saúde Integral", "Helena Costa")
        };
        var changed = false;
        foreach (var task in tasks)
        {
            if (!examples.TryGetValue(task.Client, out var metadata)) continue;
            if (!string.IsNullOrWhiteSpace(task.ProcessNumber) &&
                !string.Equals(task.ProcessNumber, "Sem processo judicial", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(task.Counterparty))
            {
                task.Counterparty = metadata.Counterparty;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(task.Supervisor))
            {
                task.Supervisor = metadata.Supervisor;
                changed = true;
            }
            if (string.Equals(task.ProcessNumber, "Sem processo judicial", StringComparison.OrdinalIgnoreCase))
            {
                task.ProcessNumber = string.Empty;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(task.ProcessNumber) && !string.IsNullOrWhiteSpace(task.Counterparty))
            {
                task.Counterparty = string.Empty;
                changed = true;
            }
        }
        return changed;
    }
}
