using Microsoft.Extensions.AI;

namespace HomeStoq.App.Services;

/// <summary>
/// Helper class to inspect AI chat messages and detect vision/image content.
/// Used by HybridAIClient to route requests appropriately.
/// </summary>
public static class AIContentInspector
{
    /// <summary>
    /// Determines if the chat messages contain vision content (images or PDFs).
    /// </summary>
    public static bool ContainsVisionContent(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is DataContent data)
                {
                    var mediaType = data.MediaType?.ToLowerInvariant() ?? "";
                    if (IsImageOrPdf(mediaType))
                        return true;
                }
                
                // Also check for PDF file references in text content
                if (content is TextContent text && IsPdfReference(text.Text))
                    return true;
            }
        }
        return false;
    }

    private static bool IsImageOrPdf(string mediaType) =>
        mediaType.StartsWith("image/") ||
        mediaType == "application/pdf" ||
        mediaType == "application/x-pdf";

    private static bool IsPdfReference(string? text) =>
        text?.Contains(".pdf", StringComparison.OrdinalIgnoreCase) == true ||
        text?.Contains("application/pdf", StringComparison.OrdinalIgnoreCase) == true;
}
