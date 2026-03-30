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
    private readonly string _listName;
    private TasksService? _tasksService;
    private string? _listId;

    public VoiceSyncWorker(IServiceProvider serviceProvider, ILogger<VoiceSyncWorker> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        // Read from environment variable or fallback to "@default" (Google's default list identifier)
        _listName = configuration["GOOGLE_TASKS_LIST_NAME"] ?? "@default";
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
            if (_listName == "@default")
            {
                _listId = "@default";
                _logger.LogInformation("Using default Google Tasks list.");
            }
            else
            {
                var lists = await _tasksService.Tasklists.List().ExecuteAsync(stoppingToken);
                _listId = lists.Items?.FirstOrDefault(l => l.Title.Equals(_listName, StringComparison.OrdinalIgnoreCase))?.Id;
                
                if (string.IsNullOrEmpty(_listId))
                {
                    _logger.LogWarning("Google Tasks list '{ListName}' not found. Voice sync disabled.", _listName);
                    return;
                }
                
                _logger.LogInformation("Using Google Tasks list: {ListName}", _listName);
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
