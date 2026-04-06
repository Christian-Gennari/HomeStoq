using System.Text.Json;
using HomeStoq.App.Models;
using HomeStoq.App.Repositories;
using HomeStoq.App.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HomeStoq.App.Endpoints;

public static class ShoppingListEndpoints
{
    public static IEndpointRouteBuilder MapShoppingListEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/shopping-list/current - Get current Draft/Active list
        app.MapGet("/api/shopping-list/current", async (
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("GET /api/shopping-list/current requested.");
            
            var buyList = await repository.GetDraftOrActiveBuyListAsync();
            
            if (buyList == null)
            {
                return Results.Ok(new { hasList = false });
            }

            return Results.Ok(new
            {
                hasList = true,
                id = buyList.Id,
                status = buyList.Status.ToString().ToLower(),
                greeting = buyList.GeneratedContext ?? "Your shopping list",
                followUpQuestion = buyList.UserContext,
                createdAt = buyList.CreatedAt,
                totalItems = buyList.TotalItems,
                checkedItems = buyList.CheckedItems,
                items = buyList.Items.Select(i => new
                {
                    id = i.Id,
                    itemName = i.ItemName,
                    quantity = i.Quantity,
                    isChecked = i.IsChecked,
                    isDismissed = i.IsDismissed,
                    note = i.Note,
                    source = i.Source,
                    aiOriginalReasoning = i.AIOriginalReasoning
                }).ToList()
            });
        });

        // POST /api/shopping-list/create - Create a blank list session (no AI, no pre-populated items)
        app.MapPost("/api/shopping-list/create", async (
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/create requested.");

            // Cancel/clear any existing draft
            var existingList = await repository.GetDraftOrActiveBuyListAsync();
            if (existingList != null && existingList.Status == BuyListStatus.Draft)
            {
                await repository.ClearBuyListItemsAsync(existingList.Id);
                return Results.Ok(new { id = existingList.Id });
            }

            var buyList = await repository.CreateBuyListAsync("", null);
            return Results.Ok(new { id = buyList.Id });
        });

        // POST /api/shopping-list/generate - Generate new AI suggestions
        app.MapPost("/api/shopping-list/generate", async (
            InventoryRepository repository,
            GeminiService gemini,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/generate requested.");

            // Get existing draft/active list and cancel it if present
            var existingList = await repository.GetDraftOrActiveBuyListAsync();
            if (existingList != null && existingList.Status == BuyListStatus.Draft)
            {
                // Clear items from draft and reuse, or create new
                await repository.ClearBuyListItemsAsync(existingList.Id);
            }

            // Get data for AI
            var history = await repository.GetHistoryAsync(30);
            var inventory = await repository.GetInventoryAsync();

            var historyJson = JsonSerializer.Serialize(history);
            var inventoryJson = JsonSerializer.Serialize(inventory);

            logger.LogInformation("Generating shopping buddy suggestions via Gemini...");
            var aiResponse = await gemini.GenerateShoppingBuddyListAsync(historyJson, inventoryJson);

            if (aiResponse == null)
            {
                logger.LogWarning("Gemini failed to generate shopping buddy suggestions.");
                return Results.Problem("AI failed to generate suggestions.");
            }

            // Create new BuyList or reuse existing draft
            BuyList buyList;
            if (existingList != null && existingList.Status == BuyListStatus.Draft)
            {
                buyList = existingList;
            }
            else
            {
                buyList = await repository.CreateBuyListAsync(aiResponse.Greeting, aiResponse.Greeting);
            }

            // Add AI suggestions as items (all checked by default)
            foreach (var suggestion in aiResponse.Suggestions)
            {
                await repository.AddItemToBuyListAsync(
                    buyList.Id,
                    suggestion.ItemName,
                    suggestion.Quantity,
                    "ai-suggestion",
                    suggestion.Reasoning,
                    isChecked: true
                );
            }

            // Reload to get fresh data
            var freshBuyList = await repository.GetBuyListByIdAsync(buyList.Id);
            if (freshBuyList == null)
            {
                return Results.Problem("Failed to reload buy list.");
            }
            buyList = freshBuyList;

            return Results.Ok(new
            {
                id = buyList.Id,
                status = buyList.Status.ToString().ToLower(),
                greeting = aiResponse.Greeting,
                followUpQuestion = aiResponse.FollowUpQuestion,
                createdAt = buyList.CreatedAt,
                totalItems = buyList.TotalItems,
                checkedItems = buyList.CheckedItems,
                items = buyList.Items.Select(i => new
                {
                    id = i.Id,
                    itemName = i.ItemName,
                    quantity = i.Quantity,
                    isChecked = i.IsChecked,
                    isDismissed = i.IsDismissed,
                    note = i.Note,
                    source = i.Source,
                    aiOriginalReasoning = i.AIOriginalReasoning
                }).ToList()
            });
        });

        // POST /api/shopping-list/{id}/follow-up - Handle user context reply
        app.MapPost("/api/shopping-list/{id}/follow-up", async (
            long id,
            [FromBody] FollowUpRequest request,
            InventoryRepository repository,
            GeminiService gemini,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/{Id}/follow-up requested.", id);

            var buyList = await repository.GetBuyListByIdAsync(id);
            if (buyList == null)
            {
                return Results.NotFound("Shopping list not found.");
            }

            // Save user context
            await repository.UpdateBuyListUserContextAsync(id, request.UserReply);

            // Get data for AI
            var history = await repository.GetHistoryAsync(30);
            var inventory = await repository.GetInventoryAsync();
            var previousSuggestions = JsonSerializer.Serialize(buyList.Items.Select(i => new
            {
                i.ItemName,
                i.Quantity,
                i.AIOriginalReasoning
            }));

            var historyJson = JsonSerializer.Serialize(history);
            var inventoryJson = JsonSerializer.Serialize(inventory);

            logger.LogInformation("Generating follow-up shopping suggestions via Gemini...");
            var aiResponse = await gemini.GenerateShoppingBuddyListWithContextAsync(
                historyJson, 
                inventoryJson, 
                request.UserReply,
                previousSuggestions
            );

            if (aiResponse == null)
            {
                logger.LogWarning("Gemini failed to generate follow-up suggestions.");
                return Results.Problem("AI failed to generate follow-up suggestions.");
            }

            // Clear existing items and add new suggestions
            await repository.ClearBuyListItemsAsync(id);

            foreach (var suggestion in aiResponse.Suggestions)
            {
                await repository.AddItemToBuyListAsync(
                    id,
                    suggestion.ItemName,
                    suggestion.Quantity,
                    "ai-suggestion",
                    suggestion.Reasoning,
                    isChecked: true
                );
            }

            // Reload to get fresh data
            buyList = await repository.GetBuyListByIdAsync(id);

            return Results.Ok(new
            {
                id = buyList!.Id,
                status = buyList.Status.ToString().ToLower(),
                greeting = aiResponse.Greeting,
                followUpQuestion = aiResponse.FollowUpQuestion,
                updatedAt = buyList.UpdatedAt,
                totalItems = buyList.TotalItems,
                checkedItems = buyList.CheckedItems,
                items = buyList.Items.Select(i => new
                {
                    id = i.Id,
                    itemName = i.ItemName,
                    quantity = i.Quantity,
                    isChecked = i.IsChecked,
                    isDismissed = i.IsDismissed,
                    note = i.Note,
                    source = i.Source,
                    aiOriginalReasoning = i.AIOriginalReasoning
                }).ToList()
            });
        });

        // PUT /api/shopping-list/{id}/items/{itemId} - Update item
        app.MapPut("/api/shopping-list/{id}/items/{itemId}", async (
            long id,
            long itemId,
            [FromBody] UpdateItemRequest request,
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("PUT /api/shopping-list/{Id}/items/{ItemId} requested.", id, itemId);

            await repository.UpdateBuyListItemAsync(
                itemId,
                request.IsChecked,
                request.Quantity,
                request.IsDismissed,
                request.Note
            );

            return Results.Ok(new { success = true });
        });

        // POST /api/shopping-list/{id}/items - Add custom item
        app.MapPost("/api/shopping-list/{id}/items", async (
            long id,
            [FromBody] AddItemRequest request,
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/{Id}/items requested.", id);

            var item = await repository.AddItemToBuyListAsync(
                id,
                request.ItemName,
                request.Quantity,
                "custom",
                null,
                isChecked: true
            );

            return Results.Ok(new
            {
                id = item.Id,
                itemName = item.ItemName,
                quantity = item.Quantity,
                isChecked = item.IsChecked,
                isDismissed = item.IsDismissed,
                source = item.Source
            });
        });

        // POST /api/shopping-list/{id}/commit - Draft -> Active
        app.MapPost("/api/shopping-list/{id}/commit", async (
            long id,
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/{Id}/commit requested.", id);

            await repository.CommitBuyListAsync(id);
            
            return Results.Ok(new { status = "active" });
        });

        // POST /api/shopping-list/{id}/complete - Active -> Completed
        app.MapPost("/api/shopping-list/{id}/complete", async (
            long id,
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/{Id}/complete requested.", id);

            await repository.CompleteBuyListAsync(id);
            
            return Results.Ok(new { status = "completed" });
        });

        // GET /api/shopping-list/history - Past lists
        app.MapGet("/api/shopping-list/history", async (
            InventoryRepository repository,
            ILogger<GeminiService> logger,
            int limit = 20) =>
        {
            logger.LogInformation("GET /api/shopping-list/history requested.");

            var history = await repository.GetBuyListHistoryAsync(limit);

            return Results.Ok(history.Select(l => new
            {
                id = l.Id,
                name = l.SavedName ?? $"{l.CreatedAt:yyyy-MM-dd} {l.GeneratedContext}",
                createdAt = l.CreatedAt,
                status = l.Status.ToString().ToLower(),
                itemCount = l.TotalItems,
                checkedItems = l.CheckedItems,
                items = l.Items.Where(i => !i.IsDismissed).Select(i => new
                {
                    itemName = i.ItemName,
                    quantity = i.Quantity
                }).ToList()
            }).ToList());
        });

        // POST /api/shopping-list/{id}/chat - Natural language conversation
        app.MapPost("/api/shopping-list/{id}/chat", async (
            long id,
            [FromBody] ChatRequest request,
            InventoryRepository repository,
            GeminiService gemini,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/{Id}/chat requested.", id);

            var buyList = await repository.GetBuyListByIdAsync(id);
            if (buyList == null)
            {
                return Results.NotFound("Shopping list not found.");
            }

            // Load conversation history with actions (avoid circular references)
            var messageDtos = buyList.Messages
                .OrderBy(m => m.Timestamp)
                .Select(m => new ChatMessageDto 
                { 
                    Role = m.Role, 
                    Content = m.Content,
                    ActionsJson = m.ActionsJson
                })
                .ToList();

            // Get inventory for context
            var inventory = await repository.GetInventoryAsync();
            var inventoryJson = JsonSerializer.Serialize(inventory);

            // Get current list items as DTOs
            var itemDtos = buyList.Items
                .Where(i => !i.IsDismissed)
                .Select(i => new BuyListItemDto 
                { 
                    Id = i.Id, 
                    ItemName = i.ItemName, 
                    Quantity = i.Quantity, 
                    Note = i.Note,
                    IsChecked = i.IsChecked,
                    IsDismissed = i.IsDismissed,
                    Source = i.Source,
                    AddedAt = i.CreatedAt
                })
                .ToList();

            // Call AI with full context
            var chatResponse = await gemini.ChatWithShoppingListAsync(
                request.Message,
                messageDtos,
                itemDtos,
                inventoryJson,
                request.Language ?? "English"
            );

            if (chatResponse == null)
            {
                return Results.Problem("AI failed to process message.");
            }

            // Save user message
            var userMessage = new BuyListMessage
            {
                BuyListId = id,
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow
            };
            await repository.AddBuyListMessageAsync(userMessage);

            // Save AI response
            var aiMessage = new BuyListMessage
            {
                BuyListId = id,
                Role = "assistant",
                Content = chatResponse.Reply,
                Timestamp = DateTime.UtcNow,
                ActionsJson = JsonSerializer.Serialize(chatResponse.Actions)
            };
            await repository.AddBuyListMessageAsync(aiMessage);

            // Update conversation JSON
            buyList.ConversationJson = JsonSerializer.Serialize(
                buyList.Messages.Select(m => new { m.Role, m.Content, m.Timestamp })
            );
            await repository.UpdateBuyListAsync(buyList);

            return Results.Ok(new
            {
                reply = chatResponse.Reply,
                actions = chatResponse.Actions,
                suggestedReplies = chatResponse.SuggestedReplies,
                requiresConfirmation = chatResponse.RequiresConfirmation,
                currentItems = buyList.Items.Select(i => new
                {
                    id = i.Id,
                    itemName = i.ItemName,
                    quantity = i.Quantity,
                    isChecked = i.IsChecked,
                    isDismissed = i.IsDismissed,
                    note = i.Note,
                    category = i.Category
                }).ToList()
            });
        });

        // POST /api/shopping-list/{id}/confirm - Confirm pending actions
        app.MapPost("/api/shopping-list/{id}/confirm", async (
            long id,
            [FromBody] ConfirmRequest request,
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/{Id}/confirm requested.", id);

            var buyList = await repository.GetBuyListByIdAsync(id);
            if (buyList == null)
            {
                return Results.NotFound("Shopping list not found.");
            }

            if (request.Accept)
            {
                // Apply all pending actions
                foreach (var action in request.Actions)
                {
                    switch (action.Type)
                    {
                        case "add":
                            await repository.AddItemToBuyListAsync(
                                id, action.ItemName!, action.Quantity, "ai-chat", action.Reasoning, true, action.Category
                            );
                            break;
                        case "remove":
                            var itemToRemove = buyList.Items.FirstOrDefault(i => i.ItemName == action.ItemName);
                            if (itemToRemove != null)
                            {
                                await repository.UpdateBuyListItemAsync(itemToRemove.Id, null, null, true, null);
                            }
                            break;
                        case "modify":
                            var itemToModify = buyList.Items.FirstOrDefault(i => i.ItemName == action.ItemName);
                            if (itemToModify != null)
                            {
                                await repository.UpdateBuyListItemAsync(itemToModify.Id, null, action.Quantity, null, null);
                            }
                            break;
                    }
                }
            }

            // Reload
            buyList = await repository.GetBuyListByIdAsync(id);

            return Results.Ok(new
            {
                accepted = request.Accept,
                currentItems = buyList!.Items.Select(i => new
                {
                    id = i.Id,
                    itemName = i.ItemName,
                    quantity = i.Quantity,
                    isChecked = i.IsChecked,
                    isDismissed = i.IsDismissed
                }).ToList()
            });
        });

        // POST /api/shopping-list/{id}/save - Save list with name
        app.MapPost("/api/shopping-list/{id}/save", async (
            long id,
            [FromBody] SaveRequest request,
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("POST /api/shopping-list/{Id}/save requested.", id);

            var buyList = await repository.GetBuyListByIdAsync(id);
            if (buyList == null)
            {
                return Results.NotFound("Shopping list not found.");
            }

            var activeItems = buyList.Items.Count(i => !i.IsDismissed);
            if (activeItems == 0)
            {
                return Results.BadRequest("Cannot save an empty list.");
            }

            // Generate auto name if requested
            string savedName;
            if (request.AutoName || string.IsNullOrWhiteSpace(request.CustomName))
            {
                var date = DateTime.Now.ToString("yyyy-MM-dd");
                savedName = $"{date} Shopping ({activeItems} items)";
            }
            else
            {
                savedName = request.CustomName!;
            }

            buyList.IsSaved = true;
            buyList.SavedName = savedName;
            buyList.IsActiveSession = true;
            buyList.Status = BuyListStatus.Saved;
            buyList.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateBuyListAsync(buyList);

            return Results.Ok(new
            {
                saved = true,
                name = savedName,
                id = buyList.Id
            });
        });

        // GET /api/shopping-list/saved - Get saved list for sidebar
        app.MapGet("/api/shopping-list/saved", async (
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("GET /api/shopping-list/saved requested.");

            var savedList = await repository.GetSavedBuyListAsync();
            
            if (savedList == null)
            {
                return Results.Ok(new { hasSaved = false });
            }

            return Results.Ok(new
            {
                hasSaved = true,
                id = savedList.Id,
                name = savedList.SavedName,
                createdAt = savedList.CreatedAt,
                itemCount = savedList.Items.Count(i => !i.IsDismissed),
                items = savedList.Items.Where(i => !i.IsDismissed).Select(i => new
                {
                    itemName = i.ItemName,
                    quantity = i.Quantity
                }).ToList()
            });
        });

        // GET /api/shopping-list/saved/all - Get all saved lists for browsing
        app.MapGet("/api/shopping-list/saved/all", async (
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("GET /api/shopping-list/saved/all requested.");

            var savedLists = await repository.GetAllSavedListsAsync();
            
            return Results.Ok(savedLists.Select(l => new
            {
                id = l.Id,
                name = l.SavedName,
                createdAt = l.CreatedAt,
                updatedAt = l.UpdatedAt,
                status = l.Status.ToString().ToLower(),
                itemCount = l.Items.Count(i => !i.IsDismissed),
                items = l.Items.Where(i => !i.IsDismissed).Select(i => new
                {
                    itemName = i.ItemName,
                    quantity = i.Quantity
                }).ToList()
            }).ToList());
        });

        // DELETE /api/shopping-list/{id} - Delete a saved list
        app.MapDelete("/api/shopping-list/{id}", async (
            long id,
            InventoryRepository repository,
            ILogger<GeminiService> logger) =>
        {
            logger.LogInformation("DELETE /api/shopping-list/{Id} requested.", id);

            var buyList = await repository.GetBuyListByIdAsync(id);
            if (buyList == null)
            {
                return Results.NotFound("Shopping list not found.");
            }

            await repository.DeleteBuyListAsync(id);
            
            return Results.Ok(new { deleted = true });
        });

        return app;
    }
}

public class FollowUpRequest
{
    public string UserReply { get; set; } = string.Empty;
}

public class UpdateItemRequest
{
    public bool? IsChecked { get; set; }
    public double? Quantity { get; set; }
    public bool? IsDismissed { get; set; }
    public string? Note { get; set; }
}

public class AddItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; } = 1;
}

// NEW: Chat request/response DTOs
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? Language { get; set; }
}

public class ConfirmRequest
{
    public bool Accept { get; set; }
    public List<ChatAction> Actions { get; set; } = new();
}

public class SaveRequest
{
    public bool AutoName { get; set; }
    public string? CustomName { get; set; }
}

public class ChatAction
{
    public string Type { get; set; } = string.Empty; // "add", "remove", "modify", "info"
    public string? ItemName { get; set; }
    public double Quantity { get; set; }
    public string? Category { get; set; }
    public string? Reasoning { get; set; }
}

// DTOs to avoid circular reference serialization issues
public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ActionsJson { get; set; }
}

public class BuyListItemDto
{
    public long Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Note { get; set; }
    public bool IsChecked { get; set; }
    public bool IsDismissed { get; set; }
    public string? Source { get; set; }
    public string? Category { get; set; }
    public DateTime? AddedAt { get; set; }
}