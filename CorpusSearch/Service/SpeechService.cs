using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CorpusSearch.Dependencies;
using CorpusSearch.Dependencies.csly;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CorpusSearch.Service;

/// <summary>
/// The speech corpus (Manx-forge/manx-speech-corpus): its own index, apart from the text
/// corpus, so AI-generated lines never reach the word statistics or dictionary attestations
/// (D4). Every hit links to the recording at its source, at the matched word less a second.
/// </summary>
public partial class SpeechService
{
    public const string GitHubRepo = "Manx-forge/manx-speech-corpus";

    /// <summary>Confidence bands of ASR lines (D37): green at 90 and above, amber from 60</summary>
    public const int Green = 90, Amber = 60;

    private readonly LuceneIndex index = LuceneIndex.GetInstance();
    private readonly Searcher searcher;
    private readonly ILogger<SpeechService> log;
    private readonly ConcurrentDictionary<string, SpeechDocument> works = new();
    private string root = "";

    public SpeechService(SearchParser parser, ILogger<SpeechService> log)
    {
        searcher = new Searcher(index, parser);
        this.log = log;
    }

    public bool HasIdent(string ident) => works.ContainsKey(ident);

    /// <summary>Every recording, for the Browse page</summary>
    public IReadOnlyCollection<SpeechDocument> Works => works.Values.ToList();

    /// <summary>Indexes every work under <paramref name="path"/> (default: SpeechData beside the
    /// server). A missing directory leaves the speech corpus empty, with a warning</summary>
    public void Load(string? path)
    {
        path = string.IsNullOrEmpty(path) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpeechData") : path;
        if (!Directory.Exists(path))
        {
            log.LogWarning("No speech corpus at '{Path}': speech search is empty", path);
            return;
        }
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        root = path;
        var documents = Directory.GetFiles(path, "manifest.json.txt", SearchOption.AllDirectories).Select(manifest =>
        {
            var document = JsonConvert.DeserializeObject<SpeechDocument>(File.ReadAllText(manifest))
                           ?? throw new InvalidOperationException($"'{manifest}' deserialized to null");
            document.LocationOnDisk = Path.GetDirectoryName(manifest);
            document.GitHubRepo = GitHubRepo;
            return document;
        }).ToList();
        // as the text corpus (Startup.AddDocuments): parallel, a failed document skipped
        Parallel.ForEach(documents, document =>
        {
            try
            {
                foreach (var chunk in document.LoadPreparedLines().Chunk(500))
                {
                    index.Add(document, chunk);
                }
                works.TryAdd(document.Ident, document);
            }
            catch (Exception e)
            {
                log.LogError(e, "Failed loading speech recording {Ident}", document.Ident);
            }
        });
        index.Compact();
        log.LogInformation("Loaded {Count} speech recordings in {Milliseconds}ms", works.Count, stopwatch.ElapsedMilliseconds);
    }

    public record Filter(string? Origin = null, int? MinConfidence = null, string? Platform = null,
        int? MinYear = null, int? MaxYear = null)
    {
        /// <summary>A line passes on its origin and confidence (human lines have none, and always
        /// pass a confidence floor), and on its recording's platform and date (undated recordings
        /// pass a date range)</summary>
        internal bool Accepts(SpeechDocument work, string? origin, int? confidence) =>
            (Origin == null || origin == Origin)
            && (MinConfidence == null || confidence == null || confidence >= MinConfidence)
            && (Platform == null || work.Platform == Platform)
            && (MinYear == null || work.CreatedCircaEnd == null || work.CreatedCircaEnd.Value.Year >= MinYear)
            && (MaxYear == null || work.CreatedCircaStart == null || work.CreatedCircaStart.Value.Year <= MaxYear);
    }

    public record Hit(int LineNumber, string? Manx, string? English, string? Speaker, double? Start, double? End,
        string? Origin, int? Confidence, IReadOnlyList<HighlightRange>? ManxHighlights,
        IReadOnlyList<HighlightRange>? EnglishHighlights, int? Time, string? Link);

    public record Recording(string Ident, string Name, string? Platform, string? Origin, string? Source,
        DateTime? Date, string? LinkStatus, bool Seekable, int Count, int MatchedLines, List<Hit> Hits);

    public record SearchResult(string Query, long NumberOfMatches, int NumberOfLines, int NumberOfRecordings,
        List<Recording> Recordings);

    /// <summary>The recordings with the most matches first (at most <paramref name="maxRecordings"/>),
    /// each with its first <paramref name="linesPerRecording"/> matched lines</summary>
    public SearchResult Search(string query, SearchOptions options, Filter filter, int maxRecordings = 100,
        int linesPerRecording = 3)
    {
        var (matches, documents) = searcher.ScanLines(query, options,
            (ident, origin, confidence) => works.TryGetValue(ident, out var work) && filter.Accepts(work, origin, confidence),
            linesPerRecording);
        var recordings = documents
            .OrderByDescending(x => x.Count).ThenBy(x => works[x.Ident].Name)
            .Take(maxRecordings)
            .Select(x =>
            {
                var work = works[x.Ident];
                return new Recording(work.Ident, work.Name, work.Platform, work.Origin, work.Source,
                    work.CreatedCircaStart, work.LinkStatus, work.DeepLink != null, x.Count, x.MatchedLines,
                    x.Lines.Select(line => ToHit(work, line, MatchedWordStart(line))).ToList());
            }).ToList();
        return new SearchResult(query, matches, documents.Sum(x => x.MatchedLines), documents.Count, recordings);
    }

    public record Work(string Ident, string Name, string? Platform, string? Origin, string? Source,
        List<string> AltUrls, string? LinkStatus, bool Seekable, double? Duration, DateTime? CreatedCircaStart,
        DateTime? CreatedCircaEnd, string? Notes, string? Author, string? Translated, string? AsrModel,
        string? CorpusWork, string GitHubLink, List<Hit> Lines);

    /// <summary>A recording's whole transcript, each line linked at its start</summary>
    public Work? GetWork(string ident)
    {
        if (!works.TryGetValue(ident, out var work))
        {
            return null;
        }
        var lines = index.GetAllLines(ident, getTranscript: true).Select(line => ToHit(work, line, line.SubStart)).ToList();
        var folder = Path.GetRelativePath(root, work.LocationOnDisk ?? root).Replace('\\', '/');
        return new Work(work.Ident, work.Name, work.Platform, work.Origin, work.Source, work.AltUrls, work.LinkStatus,
            work.DeepLink != null, work.Duration, work.CreatedCircaStart, work.CreatedCircaEnd, work.Notes,
            work.Author, work.Translated, work.AsrModel, work.CorpusWork,
            $"https://github.com/{GitHubRepo}/tree/main/OpenData/{Uri.EscapeDataString(folder).Replace("%2F", "/")}",
            lines);
    }

    /// <summary>The recording's link at <paramref name="seconds"/> less a second, floored to
    /// whole seconds (D25: YouTube's t= takes whole seconds; the pre-roll catches the word's onset)</summary>
    private static Hit ToHit(SpeechDocument work, DocumentLine line, double? seconds)
    {
        int? time = seconds == null ? null : (int)Math.Max(0, Math.Floor(seconds.Value - 1));
        var link = work.DeepLink != null && time != null
            ? work.DeepLink.Replace("{t}", time.Value.ToString(CultureInfo.InvariantCulture))
            : work.LinkStatus == "ok" ? work.Source : null;
        return new Hit(line.CsvLineNumber, line.Manx, line.English, line.Speaker, line.SubStart, line.SubEnd,
            line.Origin, line.Confidence, line.ManxHighlights, line.EnglishHighlights, time, link);
    }

    /// <summary>When the line's first highlighted Manx word starts: its own time if aligned, else
    /// the nearest aligned word before it, else the line's start (English matches use the line's)</summary>
    internal static double? MatchedWordStart(DocumentLine line)
    {
        var first = line.ManxHighlights?.FirstOrDefault();
        if (first == null || line.Manx == null || line.WordStarts == null)
        {
            return line.SubStart;
        }
        var wordIndex = Whitespace().Split(line.Manx[..first.Start].TrimStart()).Length - 1;
        var starts = line.WordStarts.Split(' ');
        for (var i = Math.Min(wordIndex, starts.Length - 1); i >= 0; i--)
        {
            if (double.TryParse(starts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var start))
            {
                return start;
            }
        }
        return line.SubStart;
    }

    public record LookupResult(string Ident, string Name, string? Platform, string? Source, string? Origin);

    /// <summary>"Do we have this recording?" (the Contribute page): a URL matches a work's source or
    /// alternate URLs (a YouTube video by its id), anything else the works' names</summary>
    public List<LookupResult> Lookup(string query, int limit = 20)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return [];
        }
        var key = UrlKey(query);
        return works.Values
            .Where(work => key != null
                ? new[] { work.Source }.Concat(work.AltUrls).Any(url => url != null && UrlKey(url) == key)
                : work.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(work => work.Name)
            .Take(limit)
            .Select(work => new LookupResult(work.Ident, work.Name, work.Platform, work.Source, work.Origin))
            .ToList();
    }

    /// <summary>A URL reduced to what identifies the recording: a YouTube video id, else the
    /// address without scheme, www. or trailing slash. Null if not a URL</summary>
    internal static string? UrlKey(string url)
    {
        var youTube = YouTubeId().Match(url);
        if (youTube.Success)
        {
            return "youtube:" + youTube.Groups[1].Value;
        }
        var match = WebUrl().Match(url);
        return match.Success ? match.Groups[1].Value.TrimEnd('/').ToLowerInvariant() : null;
    }

    public record Statistics(int Recordings, int Human, int Asr, double Hours);

    public Statistics GetStatistics() => new(works.Count,
        works.Values.Count(x => x.Origin == "human"), works.Values.Count(x => x.Origin == "asr"),
        Math.Round(works.Values.Sum(x => x.Duration ?? 0) / 3600, 1));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?:youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/)|youtu\.be/)([\w-]{11})")]
    private static partial Regex YouTubeId();

    [GeneratedRegex(@"^\s*https?://(?:www\.)?(\S+)$", RegexOptions.IgnoreCase)]
    private static partial Regex WebUrl();
}
