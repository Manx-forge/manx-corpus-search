import { HighlightRange } from "./SearchApi"
import { SearchOptions, searchOptionsQuery } from "./SearchOptions"

/** "human": transcribed by a person; "asr": AI-generated (speech recognition) */
export type Origin = "human" | "asr"

/** A line of a recording, with a link to the moment it is spoken */
export type SpeechHit = {
    lineNumber: number
    manx: string
    english?: string
    speaker?: string
    start?: number
    end?: number
    origin?: Origin
    /** an AI line's confidence, 0-100 */
    confidence?: number
    manxHighlights?: HighlightRange[]
    englishHighlights?: HighlightRange[]
    /** where the link lands, in whole seconds: the matched word less a second */
    time?: number
    /** the recording at its source, at `time` when the source can seek */
    link?: string
}

export type SpeechRecording = {
    ident: string
    name: string
    platform?: string
    origin?: Origin
    source?: string
    date?: string
    /** "ok", or what is wrong with the source link */
    linkStatus?: string
    /** whether `link` lands at `time` (otherwise the time is shown to the reader) */
    seekable: boolean
    count: number
    matchedLines: number
    hits: SpeechHit[]
}

export type SpeechSearchResponse = {
    query: string
    numberOfMatches: number
    numberOfLines: number
    numberOfRecordings: number
    recordings: SpeechRecording[]
}

export type SpeechFilters = {
    origin?: Origin
    minConfidence?: number
    platform?: string
}

export type SpeechWork = {
    ident: string
    name: string
    platform?: string
    origin?: Origin
    source?: string
    altUrls: string[]
    linkStatus?: string
    seekable: boolean
    duration?: number
    createdCircaStart?: string
    createdCircaEnd?: string
    notes?: string
    author?: string
    translated?: string
    asrModel?: string
    corpusWork?: string
    gitHubLink: string
    lines: SpeechHit[]
}

export type SpeechLookupResult = {
    ident: string
    name: string
    platform?: string
    source?: string
    origin?: Origin
}

export type SpeechStatistics = {
    recordings: number
    human: number
    asr: number
    hours: number
}

const getJson = async <T>(url: string, signal?: AbortSignal): Promise<T> => {
    const response = await fetch(url, { signal })
    if (!response.ok) {
        throw new Error(`${url} returned ${response.status}`)
    }
    return (await response.json()) as T
}

export const searchSpeech = (
    query: string,
    english: boolean,
    filters: SpeechFilters,
    options: SearchOptions,
    signal?: AbortSignal,
): Promise<SpeechSearchResponse> => {
    const params = new URLSearchParams({ english: english.toString() })
    for (const [key, value] of Object.entries(filters)) {
        if (value != null) {
            params.set(key, String(value))
        }
    }
    return getJson(
        `/api/Speech/Search/${encodeURIComponent(query)}?${params.toString()}${searchOptionsQuery(options)}`,
        signal,
    )
}

export const getSpeechWork = (ident: string): Promise<SpeechWork> =>
    getJson(`/api/Speech/Work/${encodeURIComponent(ident)}`)

export const lookupSpeech = (
    query: string,
    signal?: AbortSignal,
): Promise<SpeechLookupResult[]> =>
    getJson(`/api/Speech/Lookup?q=${encodeURIComponent(query)}`, signal)

export const getSpeechStatistics = (): Promise<SpeechStatistics> =>
    getJson("/api/Speech/Statistics")

/** Platform keys as the data names them, for display */
export const platformNames: Record<string, string> = {
    youtube: "YouTube",
    manx_radio: "Manx Radio",
    learn_manx: "Learn Manx",
    clilstore: "Clilstore",
    common_voice: "Common Voice",
    saysomething: "Say Something in Manx",
}

/** The YouTube video id of a URL, if it is a video's */
export const youTubeId = (url?: string): string | null =>
    url?.match(
        /(?:youtube\.com\/(?:watch\?(?:.*&)?v=|embed\/|shorts\/)|youtu\.be\/)([\w-]{11})/,
    )?.[1] ?? null

/** 75 -> "1:15", 3725 -> "1:02:05" */
export const formatTime = (seconds: number): string => {
    const s = Math.floor(seconds)
    const [h, m] = [Math.floor(s / 3600), Math.floor((s % 3600) / 60)]
    const pad = (x: number) => x.toString().padStart(2, "0")
    return h > 0 ? `${h}:${pad(m)}:${pad(s % 60)}` : `${m}:${pad(s % 60)}`
}
