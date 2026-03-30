using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Tasks.v1;
using Google.Apis.Tasks.v1.Data;
using HomeStoq.Server.Repositories;

namespace HomeStoq.Server.Services;

public class VoiceSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VoiceSyncWorker> _logger;
    private TasksService? _tasksService;
    private string? _listId;

    public VoiceSyncWorker(IServiceProvider serviceProvider, ILogger<VoiceSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VoiceSyncWorker starting.");

        try
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            if (credential.IsCreateScopedRequired)
            {
                credential = credential.CreateScoped(TasksService.Scope.Tasks);
            }

            _tasksService = new TasksService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "HomeStoq"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Tasks API. Voice sync will be disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during voice sync loop.");
            }

            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async System.Threading.Tasks.Task SyncTasksAsync(CancellationToken stoppingToken)
    {
        if (_tasksService == null) return;

        if (string.IsNullOrEmpty(_listId))
        {
            var lists = await _tasksService.Tasklists.List().ExecuteAsync(stoppingToken);
            _listId = lists.Items?.FirstOrDefault(l => l.Title.Equals("HomeStoq", StringComparison.OrdinalIgnoreCase))?.Id;
            
            if (string.IsNullOrEmpty(_listId))
            {
                _logger.LogWarning("Google Tasks list 'HomeStoq' not found.");
                return;
            }
        }

        var tasks = await _tasksService.Tasks.List(_listId).ExecuteAsync(stoppingToken);
        if (tasks.Items == null || tasks.Items.Count == 0) return;

        using var scope = _serviceProvider.CreateScope();
        var gemini = scope.ServiceProvider.GetRequiredService<GeminiService>();
        var repository = scope.ServiceProvider.GetRequiredService<InventoryRepository>();

        foreach (var task in tasks.Items)
        {
            if (string.IsNullOrWhiteSpace(task.Title)) continue;

            _logger.LogInformation("Processing voice task: {Title}", task.Title);
            var parsed = await gemini.ParseVoiceCommandAsync(task.Title);

            if (parsed != null)
            {
                var quantityChange = parsed.Action.Equals("Remove", StringComparison.OrdinalIgnoreCase) 
                    ? -parsed.Quantity 
                    : parsed.Quantity;

                await repository.UpdateInventoryItemAsync(parsed.ItemName, quantityChange, source: "Voice");
                await _tasksService.Tasks.Delete(_listId, task.Id).ExecuteAsync(stoppingToken);
                _logger.LogInformation("Successfully processed and deleted task: {Title}", task.Title);
            }
            else
            {
                _logger.LogWarning("Gemini failed to parse voice task: {Title}", task.Title);
            }
        }
    }
}
