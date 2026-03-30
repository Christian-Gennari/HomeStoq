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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_tasksService == null)
                {
                    await InitializeTasksServiceAsync(stoppingToken);
                }

                if (_tasksService != null)
                {
                    await SyncTasksAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during voice sync loop.");
            }

            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async System.Threading.Tasks.Task InitializeTasksServiceAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Initializing Google Tasks API...");
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
            _logger.LogInformation("Google Tasks API initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to initialize Google Tasks API: {Message}. Will retry in next iteration.", ex.Message);
            _tasksService = null;
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
                    _logger.LogWarning("Google Tasks list '{ListName}' not found. Voice sync waiting...", _listName);
                    return;
                }
                
                _logger.LogInformation("Using Google Tasks list: {ListName} (ID: {ListId})", _listName, _listId);
            }
        }

        Tasks tasks;
        try
        {
            tasks = await _tasksService.Tasks.List(_listId).ExecuteAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch tasks from Google Tasks API.");
            if (ex.Message.Contains("unauthenticated", StringComparison.OrdinalIgnoreCase))
            {
                _tasksService = null; // Force re-initialization
            }
            return;
        }

        if (tasks.Items == null || tasks.Items.Count == 0) return;

        using var scope = _serviceProvider.CreateScope();
        var gemini = scope.ServiceProvider.GetRequiredService<GeminiService>();
        var repository = scope.ServiceProvider.GetRequiredService<InventoryRepository>();

        foreach (var task in tasks.Items)
        {
            if (string.IsNullOrWhiteSpace(task.Title)) continue;

            _logger.LogInformation("Processing voice task: {Title}", task.Title);
            
            try
            {
                var parsed = await gemini.ParseVoiceCommandAsync(task.Title);

                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.ItemName))
                {
                    var isRemove = string.Equals(parsed.Action, "Remove", StringComparison.OrdinalIgnoreCase);
                    var quantityChange = isRemove ? -parsed.Quantity : parsed.Quantity;

                    _logger.LogInformation("Applying voice action: {Action} {Quantity} {Item}", isRemove ? "Remove" : "Add", parsed.Quantity, parsed.ItemName);
                    await repository.UpdateInventoryItemAsync(parsed.ItemName, quantityChange, source: "Voice");
                    
                    await _tasksService.Tasks.Delete(_listId, task.Id).ExecuteAsync(stoppingToken);
                    _logger.LogInformation("Successfully processed and deleted task: {Title}", task.Title);
                }
                else
                {
                    _logger.LogWarning("Gemini could not parse task as an inventory command: {Title}. Skipping for now.", task.Title);
                    // We don't delete it yet to allow for potential retry if it was a transient AI failure,
                    // but in a production app we might want to move it to a 'failed' list or delete after X retries.
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process task: {Title}", task.Title);
            }
        }
    }
}
