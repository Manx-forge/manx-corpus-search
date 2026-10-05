using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CorpusSearch.Services;
using CsvHelper;
using Newtonsoft.Json;

namespace CorpusSearch.Model;

/// <summary>
/// A recording of the speech corpus (Manx-forge/manx-speech-corpus): one work per recording,
/// with <c>document.csv</c> (lines, each human or AI-generated) and <c>words.csv</c> (word timings).
/// The manifest's speech fields are below; the rest are the text corpus's.
/// </summary>
public class SpeechDocument : OpenSourceDocument
{
    /// <summary>Where the recording is published: youtube, manx_radio, learn_manx, ...</summary>
    public string? Platform { get; set; }

    /// <summary>"human" or "asr": who transcribed it</summary>
    public string? Origin { get; set; }

    /// <summary>A link to a moment of the recording: <c>{t}</c> is replaced by whole seconds.
    /// Null when the source cannot seek, has no public URL, or its link is broken</summary>
    [JsonProperty("deep_link")]
    public string? DeepLink { get; set; }

    /// <summary>"ok", or the link register's issue with the source URL</summary>
    [JsonProperty("link_status")]
    public string? LinkStatus { get; set; }

    /// <summary>The same recording published elsewhere</summary>
    [JsonProperty("alt_urls")]
    public List<string> AltUrls { get; set; } = [];

    /// <summary>Seconds</summary>
    public double? Duration { get; set; }

    [JsonProperty("asr_model")]
    public string? AsrModel { get; set; }

    /// <summary>From the text corpus's manifest, for a recording it holds too</summary>
    public string? Author { get; set; }

    /// <summary>Who translated it (see <see cref="Author"/>)</summary>
    public string? Translated { get; set; }

    /// <summary>The ident of the same recording's transcript in the text corpus, if any</summary>
    [JsonProperty("corpus_work")]
    public string? CorpusWork { get; set; }

    internal override List<DocumentLine> LoadLocalFile()
    {
        var lines = base.LoadLocalFile();
        var starts = lines.Select(_ => new List<string>()).ToList();
        using (var reader = new StreamReader(Path.Combine(LocationOnDisk ?? "", "words.csv")))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var start = csv.GetField("start");
                starts[csv.GetField<int>("line")].Add(string.IsNullOrEmpty(start) ? "-" : start);
            }
        }
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i].WordStarts = string.Join(' ', starts[i]);
        }
        return lines;
    }
}
