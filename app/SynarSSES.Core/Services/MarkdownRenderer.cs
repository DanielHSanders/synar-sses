using Markdig;

namespace SynarSSES.Core.Services;

// Thin wrapper around Markdig with safe defaults: GFM-ish features enabled,
// raw HTML disabled so user-entered Markdown can't inject scripts. Used by
// the Outline page to render section bodies and entry notes.
public sealed class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseTaskLists()
        .DisableHtml()
        .Build();

    public string ToHtml(string markdown)
        => string.IsNullOrWhiteSpace(markdown) ? "" : Markdown.ToHtml(markdown, _pipeline);
}
