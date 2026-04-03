using System.IO;
using System.Linq;
using HomeStoq.App.Repositories;
using HomeStoq.App.Services;
using HomeStoq.App.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeStoq.App.Endpoints;

public static class ReceiptEndpoints
{
    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        _ = app.MapPost(
                "/api/receipts/scan",
                async (
                    IFormFile receiptImage,
                    GeminiService gemini,
                    InventoryRepository repository,
                    ILogger<GeminiService> logger,
                    IConfiguration config
                ) =>
                {
                    logger.LogInformation(
                        "POST /api/receipts/scan: Received file {FileName} ({ContentType})",
                        receiptImage.FileName,
                        receiptImage.ContentType
                    );
                    using var stream = new MemoryStream();
                    await receiptImage.CopyToAsync(stream);

                    var inventory = await repository.GetInventoryAsync();
                    var itemNames = inventory.Select(i => i.ItemName).ToList();

                    var items = await gemini.ProcessReceiptImageAsync(
                        stream.ToArray(),
                        receiptImage.ContentType,
                        itemNames
                    );

                    if (items == null || !items.Any())
                    {
                        logger.LogWarning("Gemini failed to process the receipt image.");
                        return Results.Problem("Gemini failed to process the image.");
                    }

                    var storeName = StoreHelper.ResolveStoreName(
                        receiptImage.FileName,
                        config["App:Language"] ?? "English"
                    );
                    var totalAmount = items.Sum(i => i.Price ?? 0);
                    var receiptId = await repository.CreateReceiptAsync(storeName, totalAmount);

                    logger.LogInformation(
                        "Gemini identified {Count} items from receipt. Saved as Receipt #{Id}",
                        items.Count,
                        receiptId
                    );

                    foreach (var item in items)
                    {
                        await repository.UpdateInventoryItemAsync(
                            item.ItemName,
                            item.Quantity,
                            item.Price,
                            source: "Receipt",
                            category: item.Category,
                            receiptId: receiptId,
                            expandedName: item.ExpandedName
                        );
                    }

                    return Results.Ok(items);
                }
            )
            .DisableAntiforgery();

        app.MapGet(
            "/api/receipts",
            async (InventoryRepository repository) =>
            {
                var receipts = await repository.GetReceiptsAsync();
                return Results.Ok(receipts);
            }
        );

        app.MapGet(
            "/api/receipts/{id}/items",
            async (long id, InventoryRepository repository) =>
            {
                var items = await repository.GetReceiptItemsAsync(id);
                return Results.Ok(items);
            }
        );

        return app;
    }
}
