using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CorpusSearch.Controllers;

/// <summary>
/// Displays HTML documents, for archive.org and non-JS crawlers.
/// </summary>
[Route("[controller]")]
public class BrowseController(
    DocumentSearchService documentSearchService,
    WorkService workService,
    SpeechService speech,
    IConfiguration configuration)
    : Controller
{
    /// <param name="corpus">"speech": the speech corpus's recordings rather than the texts</param>
    /// <param name="collection">with corpus=speech: one utterance collection's recordings</param>
    public async Task<IActionResult> Index(string? corpus = null, string? collection = null)
    {
        ViewData["Documents"] = await workService.GetAll();
        if (corpus == "speech")
        {
            // long-form recordings, with each collection of short utterances as one entry (or one collection's)
            var collections = speech.UtteranceCollections();
            var grouped = collections.Select(x => x.Key).ToHashSet();
            ViewData["Speech"] = speech.Works.Where(x => collection != null
                ? speech.CollectionOf(x) == collection
                : !grouped.Contains(speech.CollectionOf(x))).ToList();
            ViewData["Collections"] = collection == null ? collections : null;
            ViewData["Collection"] = collections.FirstOrDefault(x => x.Key == collection);
        }
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request) + "/Browse";
        return View("~/Views/Browse/Index.cshtml");
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> Get(string documentId)
    {
        if (!workService.HasIdent(documentId))
        {
            return Redirect("/Browse");
        }
        // the interactive app page is the indexable version of each text; this
        // server-rendered duplicate exists for crawlers that don't execute JS
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request)
                                   + "/docs/" + Uri.EscapeDataString(documentId);
        // trim the end in-case the CSV had excess blank lines
        var lines = documentSearchService.GetAllLines(documentId).TrimEnd(x => String.IsNullOrEmpty(x.English + x.Manx + x.Notes));
        var document = await workService.ByIdent(documentId);
        ViewData["Title"] = document.Name;
        ViewData["GitHubLink"] = document.GetGitHubLink();
        ViewData["DownloadText"] = document.GetDownloadTextLink();
        ViewData["DownloadMetadata"] = document.GetDownloadMetadataLink();
        ViewData["OriginalLanguage"] = document.Original;
        ViewData["docId"] = documentId;
        ViewData["lines"] = lines;
        return View("~/Views/Browse/Browse.cshtml");
    }
}

public static class Extensions {
    public static IList<T> TrimEnd<T>(this IList<T> target, Func<T, bool> toRemoveIf)
    {
        // TODO: This shouldn't mutate the input
        for (var i = target.Count - 1; i >= 0; i--)
        {
            try
            {
                if (toRemoveIf(target[i]))
                {
                    target.RemoveAt(i);
                }
            }
            catch
            {
                throw new Exception("a");
            }
            

        }
        return target;
    }
}