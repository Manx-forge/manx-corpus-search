using System;
using System.Collections.Generic;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

/// <summary>The speech corpus: search, a recording's transcript, the Contribute page's lookup</summary>
[ApiController]
[Route("api/[controller]")]
public class SpeechController(SpeechService speech) : ControllerBase
{
    /// <param name="english">search the English translations rather than the Manx</param>
    /// <param name="origin">"human" or "asr": only lines of that origin</param>
    /// <param name="minConfidence">hide AI lines below this confidence (0-100)</param>
    /// <param name="platform">only recordings published there (youtube, manx_radio, ...)</param>
    [HttpGet("Search/{query}")]
    public ActionResult<SpeechService.SearchResult> Search(string query, bool english = false, string? origin = null,
        int? minConfidence = null, string? platform = null, int? minYear = null, int? maxYear = null,
        [FromQuery] SearchOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > CorpusSearchQuery.MAX_LENGTH)
        {
            return BadRequest($"The query must have 1 to {CorpusSearchQuery.MAX_LENGTH} characters");
        }
        var searchOptions = (options ?? SearchOptions.Default) with
        {
            SearchType = english ? SearchType.English : SearchType.Manx,
        };
        try
        {
            return speech.Search(query, searchOptions,
                new SpeechService.Filter(origin, minConfidence, platform, minYear, maxYear));
        }
        catch (ArgumentException e)
        {
            // a query the grammar cannot build (as the text search)
            return BadRequest(e.Message);
        }
    }

    [HttpGet("Work/{ident}")]
    public ActionResult<SpeechService.Work> Work(string ident) =>
        speech.GetWork(ident) is { } work ? work : NotFound();

    /// <param name="q">a URL, or words of a title</param>
    [HttpGet("Lookup")]
    public List<SpeechService.LookupResult> Lookup(string q) => speech.Lookup(q);

    [HttpGet("Statistics")]
    public SpeechService.Statistics Statistics() => speech.GetStatistics();
}
