using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.csly;
using CorpusSearch.Infrastructure;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The speech corpus (Manx-forge/manx-speech-corpus): its own index, line-level hits linked at
/// the matched word less a second (D25), and the filters of the speech search page.
/// </summary>
[TestFixture]
public class SpeechServiceTest
{
    // written once to a temporary directory: a YouTube recording with a human transcript and
    // English, and a podcast with an AI transcript whose source can seek (an mp3) and one which can't
    private static string root = null!;
    private static SpeechService speech = null!;

    [OneTimeSetUp]
    public static void LoadFixture()
    {
        root = Path.Combine(Path.GetTempPath(), "speech-fixture-" + Path.GetRandomFileName());
        Write("youtube/demo/0001", """
            {"ident": "speech-0001", "name": "Moddey Dhoo", "source": "https://www.youtube.com/watch?v=abcdefghijk",
             "platform": "youtube", "origin": "human", "deep_link": "https://www.youtube.com/watch?v=abcdefghijk&t={t}s",
             "alt_urls": ["https://archive.org/details/moddey"], "link_status": "ok", "duration": 30.0,
             "createdCircaStart": "1948-01-01", "createdCircaEnd": "1948-12-31"}
            """, """
            Speaker,Manx,English,SubStart,SubEnd,Origin,Confidence
            HB,Va moddey mooar echey,There was a big dog at him,10.00,14.00,human,
            TB,"Oh, moddey doo",A black dog,15.00,17.00,human,
            """, """
            line,idx,word,start,end,status
            0,0,Va,10.00,10.40,aligned
            0,1,moddey,12.60,13.00,aligned
            0,2,mooar,13.00,13.40,aligned
            0,3,echey,13.40,14.00,aligned
            1,0,"Oh,",15.00,15.30,aligned
            1,1,moddey,,,unaligned
            1,2,doo,16.50,17.00,aligned
            """);
        Write("manx_radio/podcast/0002", """
            {"ident": "speech-0002", "name": "Abbyr Shen Reesht", "source": "https://example.org/episode",
             "platform": "manx_radio", "origin": "asr", "deep_link": "https://example.org/episode.mp3#t={t}",
             "alt_urls": [], "link_status": "ok", "duration": 60.0, "asr_model": "whisper"}
            """, """
            Speaker,Manx,SubStart,SubEnd,Origin,Confidence
            ,ta'n moddey goll,0.50,2.00,asr,95
            ,moddey elley,40.00,41.00,asr,40
            """, """
            line,idx,word,start,end,status
            0,0,ta'n,0.50,0.80,aligned
            0,1,moddey,0.80,1.40,aligned
            0,2,goll,1.40,2.00,aligned
            1,0,moddey,40.00,40.50,aligned
            1,1,elley,40.50,41.00,aligned
            """);
        Write("clilstore/misc/0003", """
            {"ident": "speech-0003", "name": "Clilstore reading", "source": "https://clilstore.eu/page",
             "platform": "clilstore", "origin": "human", "deep_link": null, "alt_urls": [], "link_status": "ok"}
            """, """
            Speaker,Manx,SubStart,SubEnd,Origin,Confidence
            ,yn moddey beg,5.00,7.00,human,
            """, """
            line,idx,word,start,end,status
            0,0,yn,5.00,5.20,aligned
            0,1,moddey,5.20,5.80,aligned
            0,2,beg,5.80,7.00,aligned
            """);
        speech = new SpeechService(SearchParser.GetParser(), NullLogger<SpeechService>.Instance);
        speech.Load(root);
    }

    [OneTimeTearDown]
    public static void RemoveFixture() => Directory.Delete(root, recursive: true);

    private static void Write(string folder, string manifest, string document, string words)
    {
        var path = Directory.CreateDirectory(Path.Combine(root, folder)).FullName;
        File.WriteAllText(Path.Combine(path, "manifest.json.txt"), manifest);
        File.WriteAllText(Path.Combine(path, "document.csv"), document);
        File.WriteAllText(Path.Combine(path, "words.csv"), words);
    }

    private static SpeechService.SearchResult Search(string query, SpeechService.Filter? filter = null,
        bool english = false) =>
        speech.Search(query, SearchOptions.Default with { SearchType = english ? SearchType.English : SearchType.Manx },
            filter ?? new SpeechService.Filter());

    private static SpeechService.Hit HitIn(SpeechService.SearchResult result, string ident, int index = 0) =>
        result.Recordings.Single(x => x.Ident == ident).Hits[index];

    [Test]
    public void FindsEveryMatchedLine()
    {
        var result = Search("moddey");
        Assert.That(result.NumberOfRecordings, Is.EqualTo(3));
        Assert.That(result.NumberOfLines, Is.EqualTo(5));
        // most matches first
        Assert.That(result.Recordings.Select(x => x.Count).First(), Is.EqualTo(2));
    }

    [Test]
    public void LinksToTheMatchedWordLessASecond()
    {
        // 'moddey' starts at 12.6s, not at the line's 10s: 11.6 floored
        var hit = HitIn(Search("moddey"), "speech-0001");
        Assert.That(hit.Time, Is.EqualTo(11));
        Assert.That(hit.Link, Is.EqualTo("https://www.youtube.com/watch?v=abcdefghijk&t=11s"));
        Assert.That(hit.ManxHighlights, Is.Not.Null);
    }

    [Test]
    public void AnUnalignedWordLinksToTheAlignedWordBeforeIt()
    {
        // the second line's 'moddey' has no time: 'Oh,' at 15s stands in
        Assert.That(HitIn(Search("moddey"), "speech-0001", 1).Time, Is.EqualTo(14));
    }

    [Test]
    public void TheLinkNeverPrecedesTheRecording()
    {
        Assert.That(HitIn(Search("goll"), "speech-0002").Time, Is.EqualTo(0));
    }

    [Test]
    public void ASourceWhichCannotSeekLinksToTheRecordingAndShowsTheTime()
    {
        var recording = Search("beg").Recordings.Single();
        Assert.That(recording.Seekable, Is.False);
        Assert.That(recording.Hits[0].Link, Is.EqualTo("https://clilstore.eu/page"));
        Assert.That(recording.Hits[0].Time, Is.EqualTo(4));
    }

    [Test]
    public void EnglishMatchesLinkToTheLine()
    {
        var hit = HitIn(Search("dog", english: true), "speech-0001");
        Assert.That(hit.Time, Is.EqualTo(9));
        Assert.That(hit.EnglishHighlights, Is.Not.Null);
    }

    [Test]
    public void FiltersByOrigin()
    {
        var result = Search("moddey", new SpeechService.Filter(Origin: "asr"));
        Assert.That(result.Recordings.Select(x => x.Ident), Is.EqualTo(new[] { "speech-0002" }));
    }

    [Test]
    public void AConfidenceFloorHidesUncertainAiLinesButNoHumanOnes()
    {
        var result = Search("moddey", new SpeechService.Filter(MinConfidence: 60));
        Assert.That(result.NumberOfLines, Is.EqualTo(4));
        Assert.That(result.Recordings.Single(x => x.Ident == "speech-0002").Hits.Select(x => x.Confidence),
            Is.EqualTo(new int?[] { 95 }));
    }

    [Test]
    public void FiltersByPlatformAndDate()
    {
        Assert.That(Search("moddey", new SpeechService.Filter(Platform: "clilstore")).NumberOfRecordings,
            Is.EqualTo(1));
        // undated recordings pass a date range
        Assert.That(Search("moddey", new SpeechService.Filter(MinYear: 1950)).NumberOfRecordings, Is.EqualTo(2));
    }

    [Test]
    public void AWorkIsItsWholeTranscriptInOrder()
    {
        var work = speech.GetWork("speech-0001")!;
        Assert.That(work.Lines.Select(x => x.Speaker), Is.EqualTo(new[] { "HB", "TB" }));
        Assert.That(work.Lines[1].English, Is.EqualTo("A black dog"));
        Assert.That(work.Lines[0].Link, Is.EqualTo("https://www.youtube.com/watch?v=abcdefghijk&t=9s"));
        Assert.That(work.GitHubLink,
            Is.EqualTo("https://github.com/Manx-forge/manx-speech-corpus/tree/main/OpenData/youtube/demo/0001"));
        Assert.That(speech.GetWork("speech-9999"), Is.Null);
    }

    [TestCase("https://youtu.be/abcdefghijk")]
    [TestCase("https://www.youtube.com/watch?v=abcdefghijk&t=60s")]
    [TestCase("http://archive.org/details/moddey/")]
    [TestCase("moddey dhoo")]
    public void LooksUpARecordingByUrlOrTitle(string query)
    {
        Assert.That(speech.Lookup(query).Select(x => x.Ident), Is.EqualTo(new[] { "speech-0001" }));
    }

    [Test]
    public void AnUnknownUrlIsNotFound()
    {
        Assert.That(speech.Lookup("https://www.youtube.com/watch?v=zzzzzzzzzzz"), Is.Empty);
    }

    [Test]
    public void SpeechPagesFallThroughToTheShell()
    {
        var works = new WorkService();
        Assert.That(SpaRouteGuard.IsSpaPage("/speech", works, speech), Is.True);
        Assert.That(SpaRouteGuard.IsSpaPage("/Speech/", works, speech), Is.True);
        Assert.That(SpaRouteGuard.IsSpaPage("/contribute", works, speech), Is.True);
        Assert.That(SpaRouteGuard.IsSpaPage("/speech/speech-0001", works, speech), Is.True);
        Assert.That(SpaRouteGuard.IsSpaPage("/speech/speech-9999", works, speech), Is.False);
        Assert.That(SpaRouteGuard.IsSpaPage("/speech/speech-0001/extra", works, speech), Is.False);
    }
}
