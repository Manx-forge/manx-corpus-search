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
    // recordings per folder, for CollectionOf
    private Dictionary<string, int> folderSizes = [];
    private string root = "";

    public SpeechService(SearchParser parser, ILogger<SpeechService> log)
    {
        searcher = new Searcher(index, parser);
        this.log = log;
    }

    public bool HasIdent(string ident) => works.ContainsKey(ident);

    /// <summary>Every recording, for the Browse page</summary>
    public IReadOnlyCollection<SpeechDocument> Works => works.Values.ToList();

    /// <summary>A recording's collection: its folder's parent under OpenData ("manx_radio/abbyr_shen_reesht"), with
    /// the strays put back (a spoken-dictionary clip in a folder of its own, Abbyr Shen Reesht's older episodes, and
    /// the text corpus's recordings by their series)</summary>
    public string CollectionOf(SpeechDocument work)
    {
        var folder = Folder(work);
        return work.CorpusWork switch
        {
            { } w when w.StartsWith("YouTube-Skeealyn-Vannin") => "youtube/manx_national_heritage/skeealyn_vannin",
            { } w when w.StartsWith("UOSH-") => "youtube/uosh",
            // the text corpus's other videos, and YouTube folders of a single video
            _ when folder == "youtube/manx-search-data"
                   || (work.Platform == "youtube" && folderSizes.GetValueOrDefault(folder) == 1) => "youtube/other",
            _ => folder,
        };
    }

    private string Folder(SpeechDocument work) => StrayFolder().Replace(
        Path.GetDirectoryName(Path.GetRelativePath(root, work.LocationOnDisk ?? root))?.Replace('\\', '/') ?? "", "");

    /// <param name="Years">"1990-2023" or "2015": the span of its recordings' dates; null if none is dated</param>
    /// <param name="Hours">the recordings' total length</param>
    public record Collection(string Key, string Name, string? Platform, string? Domain, string? Origin, int Count,
        string? Years, double Hours);

    /// <summary>The collections' names; an unlisted folder is named after itself</summary>
    private static readonly Dictionary<string, string> CollectionNames = new()
    {
        ["clilstore/misc"] = "Clilstore: Manx readings",
        ["common_voice/cv-24.0/validated"] = "Common Voice 24.0: read sentences",
        ["common_voice/sps-2.0"] = "Common Voice Spontaneous Speech 2.0",
        ["learn_manx/1000_words"] = "Learn Manx: 1000 Words",
        ["learn_manx/adult_manx"] = "Learn Manx: Loayr Gaelg",
        ["learn_manx/american_inheritance"] = "Learn Manx: American Inheritance",
        ["learn_manx/cowag"] = "Learn Manx: Cowag",
        ["learn_manx/podcast_gaelgagh"] = "Learn Manx: Learning Manx podcast",
        ["learn_manx/shen_recortyssyn"] = "Learn Manx: Shenn Recortyssyn",
        ["learn_manx/short_stories/bob_carswell"] = "Learn Manx: Short stories by John Pilling, translated by Bob Carswell",
        ["learn_manx/short_stories/intermediate"] = "Learn Manx: Skeealyn Zen",
        ["learn_manx/site_scrape"] = "Learn Manx: website audio",
        ["learn_manx/spkn_dict"] = "Learn Manx: Spoken Dictionary",
        ["learn_manx/spoken_dictionary_rejects"] = "Learn Manx app: other recordings",
        ["manx_radio/abbyr_shen_reesht"] = "Abbyr Shen Reesht (Manx Radio)",
        ["saysomething/lessons"] = "Say Something in Manx",
        ["youtube/culture_vannin"] = "Culture Vannin",
        ["youtube/de_linguis"] = "De Linguis: Cooishyn Gailckagh",
        ["youtube/learn_manx/a_walk_around_cregneash"] = "A Walk Around Cregneash",
        ["youtube/learn_manx/adrian_cain_ayns_purt_le_moirrey"] = "Adrian Cain ayns Purt le Moirrey",
        ["youtube/learn_manx/archibald_cregeen"] = "Archibald Cregeen",
        ["youtube/learn_manx/cappan_y_theihll"] = "Cappan y Theihll",
        ["youtube/learn_manx/cliaghtaghyn_as_skeealyn_-_traditions_and_stories"] = "Cliaghtaghyn as Skeealyn: Traditions and Stories",
        ["youtube/learn_manx/conversations_with"] = "Conversations with…",
        ["youtube/learn_manx/cooish"] = "Yn Chooish",
        ["youtube/learn_manx/cuchulainn"] = "Cuchulainn",
        ["youtube/learn_manx/daa_whooinney_ayns_baatey"] = "Daa Whooinney ayns Baatey",
        ["youtube/learn_manx/gaelg_son_paarantyn"] = "Gaelg son Paarantyn",
        ["youtube/learn_manx/island_of_culture"] = "Island of Culture",
        ["youtube/learn_manx/laa_mie_er_y_cholloo"] = "Laa Mie er y Cholloo",
        ["youtube/learn_manx/loayrt_rish"] = "Loayrt rish…",
        ["youtube/learn_manx/loayrt_taggloo_cowag"] = "Loayrt, Taggloo, Cowag",
        ["youtube/learn_manx/manannan"] = "Manannan",
        ["youtube/learn_manx/shooyl_mygeayrt_meayll_marish_davy_fisher"] = "Shooyl mygeayrt Meayll marish Davy Fisher",
        ["youtube/learn_manx/various_language_videos"] = "Learn Manx: various videos",
        ["youtube/learn_manx/yn_cholloo"] = "Yn Cholloo",
        ["youtube/other"] = "YouTube Other",
        ["youtube/manx_national_heritage/foillan_film_archive"] = "Foillan Films (Manx National Heritage)",
        ["youtube/manx_national_heritage/skeealyn_vannin"] = "Skeealyn Vannin (Irish Folklore Commission, 1948)",
        ["youtube/uosh"] = "UOSH: native speakers, 1950s",
    };

    /// <summary>Every collection of two or more recordings: Browse lists these rather than the recordings</summary>
    public List<Collection> Collections() => works.Values
        .GroupBy(CollectionOf)
        .Where(g => g.Count() >= 2)
        // oldest first, the undated last (Browse's default order is by date)
        .OrderBy(g => g.Min(x => x.CreatedCircaStart) ?? DateTime.MaxValue)
        .ThenBy(g => CollectionNames.GetValueOrDefault(g.Key) ?? FolderName(g.Key), StringComparer.OrdinalIgnoreCase)
        .Select(g => new Collection(g.Key, CollectionNames.GetValueOrDefault(g.Key) ?? FolderName(g.Key),
            Common(g.Select(x => x.Platform)), Common(g.Select(x => x.Domain)), Common(g.Select(x => x.Origin)),
            g.Count(), Years(g), g.Sum(x => x.Duration ?? 0) / 3600))
        .ToList();

    /// <summary>"youtube/learn_manx/yn_cholloo" -> "Yn cholloo"</summary>
    private static string FolderName(string key)
    {
        var name = key.Split('/')[^1].Replace('_', ' ');
        return name.Length == 0 ? key : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string? Years(IEnumerable<SpeechDocument> works)
    {
        var years = works.SelectMany(x => new[] { x.CreatedCircaStart?.Year, x.CreatedCircaEnd?.Year })
            .OfType<int>().ToList();
        return years.Count == 0 ? null
            : years.Min() == years.Max() ? $"{years.Min()}" : $"{years.Min()}-{years.Max()}";
    }

    private static string? Common(IEnumerable<string?> values) =>
        values.Where(x => x != null).GroupBy(x => x).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;

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
        folderSizes = works.Values.GroupBy(Folder).ToDictionary(g => g.Key, g => g.Count());
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

    /// <summary>A folder of one spoken-dictionary clip (".../spkn_dict/05506113"), or Abbyr Shen Reesht's "/episodes"</summary>
    [GeneratedRegex(@"/(\d{5,}|episodes)$")]
    private static partial Regex StrayFolder();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?:youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/)|youtu\.be/)([\w-]{11})")]
    private static partial Regex YouTubeId();

    [GeneratedRegex(@"^\s*https?://(?:www\.)?(\S+)$", RegexOptions.IgnoreCase)]
    private static partial Regex WebUrl();
}
