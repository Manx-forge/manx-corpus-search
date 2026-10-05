/* eslint @typescript-eslint/no-misused-promises: 0 */
import "./Speech.css"

import {
    ChangeEvent,
    Suspense,
    use,
    useEffect,
    useMemo,
    useState,
    useTransition,
} from "react"
import { Link, useSearchParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import { SearchBar } from "../components/SearchBar"
import { ManxEnglishSelector } from "../components/ManxEnglishSelector"
import { SpeechLine } from "../components/SpeechLine"
import { playable, SpeechPlayer } from "../components/SpeechPlayer"
import { defaultSearchOptions } from "../api/SearchOptions"
import { MAX_QUERY_LENGTH } from "../api/SearchApi"
import {
    getSpeechStatistics,
    Origin,
    platformNames,
    searchSpeech,
    SpeechFilters,
    SpeechHit,
    SpeechRecording,
    SpeechSearchResponse,
    SpeechStatistics,
} from "../api/SpeechApi"
import { SearchLanguage } from "./Home"

type Result =
    { status: "success"; data: SpeechSearchResponse } | { status: "error" }

/** The speech search (D33): matched lines of recordings, each linked to the
 * moment it is spoken at the recording's source */
export const Speech = () => {
    const [searchParams, setSearchParams] = useSearchParams()
    const urlQuery = searchParams.get("q") ?? ""
    const language: SearchLanguage =
        searchParams.get("lang") == "en" ? "English" : "Manx"
    // as Home: the input keeps its own state, so the cursor survives router updates
    const [query, setQueryState] = useState(urlQuery)
    useEffect(() => setQueryState(urlQuery), [urlQuery])

    const filters: SpeechFilters = {
        origin: (searchParams.get("origin") as Origin | null) ?? undefined,
    }
    const setParam = (key: string, value: string | undefined) => {
        const next = new URLSearchParams(searchParams)
        if (value) {
            next.set(key, value)
        } else {
            next.delete(key)
        }
        setSearchParams(next, { replace: true })
    }

    const [isPending, startTransition] = useTransition()
    const [result, setResult] = useState<Result | null>(null)
    const filterKey = JSON.stringify(filters)
    const tooLong = query.length > MAX_QUERY_LENGTH

    useEffect(() => {
        document.title = "Speech | Manx Corpus Search"
        return () => {
            document.title = "Manx Corpus Search"
        }
    }, [])

    useEffect(() => {
        if (query.trim() == "" || tooLong) {
            return
        }
        const controller = new AbortController()
        startTransition(async () => {
            try {
                const data = await searchSpeech(
                    query,
                    language == "English",
                    JSON.parse(filterKey) as SpeechFilters,
                    defaultSearchOptions,
                    controller.signal,
                )
                if (!controller.signal.aborted)
                    setResult({ status: "success", data })
            } catch (e) {
                if (controller.signal.aborted) return
                setResult({ status: "error" })
                console.error(e)
            }
        })
        return () => controller.abort()
    }, [query, language, filterKey, tooLong])

    const statsPromise = useMemo(
        () => getSpeechStatistics().catch(() => "error" as const),
        [],
    )

    return (
        <div className="speech-page">
            <div className="search-row search-row-hero">
                <SearchBar
                    query={query}
                    language={language}
                    onChange={(e: ChangeEvent<HTMLInputElement>) => {
                        setQueryState(e.target.value)
                        setParam("q", e.target.value)
                    }}
                />
                <ManxEnglishSelector
                    initialLanguage={language}
                    onLanguageChange={(lang) =>
                        setParam("lang", lang == "English" ? "en" : undefined)
                    }
                />
            </div>
            <div className="speech-filters">
                <label>
                    Transcribed by
                    <select
                        className="corpus-select"
                        value={filters.origin ?? ""}
                        onChange={(e) => setParam("origin", e.target.value)}
                    >
                        <option value="">Any</option>
                        <option value="human">Human</option>
                        <option value="asr">AI</option>
                    </select>
                </label>
            </div>
            {query.trim() == "" ? (
                <Suspense fallback={<Progress />}>
                    <SpeechIntro statsPromise={statsPromise} />
                </Suspense>
            ) : tooLong ? (
                <div className="home-error">
                    Search text is too long. The maximum is {MAX_QUERY_LENGTH}{" "}
                    characters.
                </div>
            ) : result == null ? (
                isPending && <Progress />
            ) : result.status == "error" ? (
                <div className="home-error">
                    Something went wrong, please try again
                </div>
            ) : (
                <div
                    style={{
                        opacity: isPending ? 0.5 : 1,
                        transition: "opacity 150ms ease",
                    }}
                >
                    <SpeechResults
                        data={result.data}
                        showEnglish={language == "English"}
                    />
                </div>
            )}
        </div>
    )
}

const SpeechIntro = (props: {
    statsPromise: Promise<SpeechStatistics | "error">
}) => {
    const stats = use(props.statsPromise)
    return (
        <div className="home-intro speech-intro">
            {stats != "error" && stats.recordings > 0 ? (
                <>
                    Search what was said in{" "}
                    <b>
                        {stats.recordings.toLocaleString()} recordings (
                        {Math.round(stats.hours).toLocaleString()} hours)
                    </b>{" "}
                    of Manx
                    <br />
                    and listen at the source.
                </>
            ) : (
                <>
                    Search what was said in recordings of Manx, and listen at
                    the source.
                </>
            )}
            <div className="speech-intro-note">
                Lines marked <b>AI</b> were transcribed by speech recognition
                and contain mistakes: the percentage is how sure it was. Missing
                a recording? <Link to="/contribute">Contribute it</Link>.
            </div>
        </div>
    )
}

const SpeechResults = (props: {
    data: SpeechSearchResponse
    showEnglish: boolean
}) => {
    const { data } = props
    if (data.recordings.length == 0) {
        return (
            <div className="no-results">
                No matches for “{data.query}”. Try another spelling: AI
                transcripts may spell a word differently.
            </div>
        )
    }
    return (
        <>
            <div className="results-header">
                <div className="results-count">
                    Found{" "}
                    <b className="results-count-matches">
                        {data.numberOfMatches.toLocaleString()}
                    </b>{" "}
                    matches in <b>{data.numberOfRecordings.toLocaleString()}</b>{" "}
                    recordings
                </div>
                {data.numberOfRecordings > data.recordings.length && (
                    <div className="results-controls">
                        Showing the {data.recordings.length} with the most
                        matches
                    </div>
                )}
            </div>
            <ol className="speech-results">
                {data.recordings.map((recording) => (
                    <RecordingResult
                        key={recording.ident}
                        recording={recording}
                        showEnglish={props.showEnglish}
                    />
                ))}
            </ol>
        </>
    )
}

const RecordingResult = (props: {
    recording: SpeechRecording
    showEnglish: boolean
}) => {
    const { recording } = props
    // the hit whose time was clicked: it plays in a popup, from that moment
    const [playing, setPlaying] = useState<SpeechHit | null>(null)
    return (
        <li className="speech-recording">
            {playing && (
                <SpeechPlayer
                    ident={recording.ident}
                    name={recording.name}
                    source={recording.source}
                    hit={playing}
                    onClose={() => setPlaying(null)}
                />
            )}
            <div className="speech-recording-head">
                <Link
                    to={`/speech/${recording.ident}`}
                    className="speech-recording-name"
                >
                    {recording.name}
                </Link>
                <span className="speech-recording-meta">
                    {platformNames[recording.platform ?? ""] ??
                        recording.platform}
                    {recording.date &&
                        ` · ${new Date(recording.date).getFullYear()}`}
                    {recording.linkStatus != "ok" && " · no public link"}
                </span>
            </div>
            <ul className="speech-lines">
                {recording.hits.map((hit) => (
                    <SpeechLine
                        key={hit.lineNumber}
                        hit={hit}
                        seekable={recording.seekable}
                        showEnglish={props.showEnglish}
                        context={12}
                        onPlay={
                            playable(hit, recording.source)
                                ? () => setPlaying(hit)
                                : undefined
                        }
                    />
                ))}
            </ul>
            {recording.matchedLines > recording.hits.length && (
                <Link to={`/speech/${recording.ident}`} className="speech-more">
                    {recording.matchedLines - recording.hits.length} more
                    matching{" "}
                    {recording.matchedLines - recording.hits.length == 1
                        ? "line"
                        : "lines"}
                </Link>
            )}
        </li>
    )
}

const Progress = () => (
    <div
        style={{ margin: "40px 0", display: "flex", justifyContent: "center" }}
    >
        <CircularProgress />
    </div>
)
