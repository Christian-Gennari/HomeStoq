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

    /// <summary>
    /// Checks if text content contains a PDF file reference.
    /// Uses word boundary detection to avoid false positives like "myfile.pdf.txt"
    /// or text that happens to contain ".pdf" as a substring.
    /// </summary>
    private static bool IsPdfReference(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check for explicit MIME type reference
        if (text.Contains("application/pdf", StringComparison.OrdinalIgnoreCase))
            return true;

        // Use regex to find .pdf at word boundaries (end of filename)
        // This matches: "file.pdf", "file.PDF", "path/to/file.pdf"
        // This avoids: "myfile.pdf.txt", "nota.pdfextension"
        var pdfPattern = @"\.pdf\b";
        return System.Text.RegularExpressions.Regex.IsMatch(
            text, 
            pdfPattern, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
