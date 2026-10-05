/* eslint @typescript-eslint/no-misused-promises: 0 */
import "./Speech.css"

import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import {
    lookupSpeech,
    platformNames,
    SpeechLookupResult,
} from "../api/SpeechApi"

const repo = "https://github.com/Manx-forge/manx-speech-corpus"
const timestamper = "https://gaelgai.im/#timestamp"

/** A prefilled issue on the speech repo (D32): its form asks for the rest */
export const contributeIssueLink = (recording: string): string =>
    `${repo}/issues/new?${new URLSearchParams({ template: "recording.yml", title: `Recording: ${recording}`, url: recording }).toString()}`

/** Contribute (D23): check whether we have a recording; if not, timestamp it and send it in */
export const Contribute = () => {
    const [query, setQuery] = useState("")
    const [results, setResults] = useState<
        SpeechLookupResult[] | "error" | null
    >(null)

    useEffect(() => {
        document.title = "Contribute | Manx Corpus Search"
        return () => {
            document.title = "Manx Corpus Search"
        }
    }, [])

    useEffect(() => {
        if (query.trim().length < 3) {
            setResults(null)
            return
        }
        const controller = new AbortController()
        const timer = setTimeout(async () => {
            try {
                const found = await lookupSpeech(query, controller.signal)
                if (!controller.signal.aborted) setResults(found)
            } catch {
                if (!controller.signal.aborted) setResults("error")
            }
        }, 250)
        return () => {
            clearTimeout(timer)
            controller.abort()
        }
    }, [query])

    return (
        <div className="contribute-page">
            <h1>Contribute a recording</h1>
            <p>
                The speech corpus aims to hold every recording of Manx on the
                web. Know one we're missing? Three steps:
            </p>
            <ol className="contribute-steps">
                <li>
                    <b>Check we don't have it.</b> Paste its link, or type words
                    of its title:
                    <input
                        className="corpus-search-input contribute-lookup"
                        type="search"
                        placeholder="https://www.youtube.com/watch?v=… or a title"
                        value={query}
                        onChange={(e) => setQuery(e.target.value)}
                    />
                    <LookupResults query={query} results={results} />
                </li>
                <li>
                    <b>Timestamp it (optional).</b> If you have a transcript,
                    the{" "}
                    <a href={timestamper} target="_blank" rel="noreferrer">
                        Manx timestamper
                    </a>{" "}
                    lines it up with the audio. Without one, we transcribe it
                    with speech recognition.
                </li>
                <li>
                    <b>Send it to us</b> on{" "}
                    <a
                        href={contributeIssueLink(query.trim())}
                        target="_blank"
                        rel="noreferrer"
                    >
                        GitHub
                    </a>
                    : the form asks for the link, and any transcript or
                    timestamps you have.
                </li>
            </ol>
            <p className="contribute-note">
                Found a mistake in a transcript, or a broken link? Every
                recording page links to its data on GitHub, where you can
                correct it or{" "}
                <a href={`${repo}/issues`} target="_blank" rel="noreferrer">
                    tell us
                </a>
                .
            </p>
        </div>
    )
}

const LookupResults = (props: {
    query: string
    results: SpeechLookupResult[] | "error" | null
}) => {
    const { results } = props
    if (results == null) {
        return null
    }
    if (results == "error") {
        return (
            <div className="contribute-result">
                Something went wrong, please try again.
            </div>
        )
    }
    if (results.length == 0) {
        return (
            <div className="contribute-result">
                We don't have “{props.query.trim()}” yet. Please send it in.
            </div>
        )
    }
    return (
        <ul className="contribute-result">
            {results.map((x) => (
                <li key={x.ident}>
                    We have <Link to={`/speech/${x.ident}`}>{x.name}</Link> (
                    {platformNames[x.platform ?? ""] ?? x.platform},{" "}
                    {x.origin == "asr" ? "AI transcript" : "human transcript"})
                </li>
            ))}
        </ul>
    )
}
