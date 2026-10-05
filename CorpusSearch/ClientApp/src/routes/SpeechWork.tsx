import "./Speech.css"

import { Suspense, use, useEffect, useMemo, useRef } from "react"
import { Link, useParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import YouTuber, { Player } from "../components/YouTuber"
import { OriginBadge, SpeechLine } from "../components/SpeechLine"
import {
    formatTime,
    getSpeechWork,
    platformNames,
    SpeechWork as Work,
    youTubeId,
} from "../api/SpeechApi"

/** A recording's transcript: click-to-seek in an embedded player for YouTube,
 * links into the source (or times to find by hand) for everything else */
export const SpeechWork = () => {
    const { ident } = useParams()
    const workPromise = useMemo(
        () => getSpeechWork(ident ?? "").catch(() => "error" as const),
        [ident],
    )
    return (
        <Suspense
            fallback={
                <div
                    style={{
                        margin: "40px 0",
                        display: "flex",
                        justifyContent: "center",
                    }}
                >
                    <CircularProgress />
                </div>
            }
        >
            <WorkPage workPromise={workPromise} />
        </Suspense>
    )
}

const WorkPage = (props: { workPromise: Promise<Work | "error"> }) => {
    const work = use(props.workPromise)
    const player = useRef<Player>(null)

    useEffect(() => {
        if (work != "error") {
            document.title = `${work.name} | Manx Corpus Search`
        }
        return () => {
            document.title = "Manx Corpus Search"
        }
    }, [work])

    if (work == "error") {
        return (
            <div className="home-error">
                This recording could not be loaded.
            </div>
        )
    }
    const videoId = work.seekable ? youTubeId(work.source) : null
    const hasEnglish = work.lines.some((x) => x.english)
    const origins = new Set(work.lines.map((x) => x.origin))

    return (
        <div className="speech-work">
            <h1 className="speech-work-title">{work.name}</h1>
            <dl className="speech-work-meta">
                <dt>Source</dt>
                <dd>
                    {work.source && work.linkStatus == "ok" ? (
                        <a href={work.source} target="_blank" rel="noreferrer">
                            {platformNames[work.platform ?? ""] ??
                                work.platform}
                        </a>
                    ) : (
                        <>
                            {platformNames[work.platform ?? ""] ??
                                work.platform}
                            {work.linkStatus != "ok" &&
                                ` (no public link: ${work.linkStatus})`}
                        </>
                    )}
                    {work.altUrls.map((url) => (
                        <span key={url}>
                            {" · "}
                            <a href={url} target="_blank" rel="noreferrer">
                                also here
                            </a>
                        </span>
                    ))}
                </dd>
                {work.createdCircaStart && (
                    <>
                        <dt>Date</dt>
                        <dd>
                            {new Date(work.createdCircaStart).getFullYear()}
                        </dd>
                    </>
                )}
                {work.duration != null && (
                    <>
                        <dt>Length</dt>
                        <dd>{formatTime(work.duration)}</dd>
                    </>
                )}
                {work.author && (
                    <>
                        <dt>Speakers</dt>
                        <dd>{work.author}</dd>
                    </>
                )}
                <dt>Transcript</dt>
                <dd>
                    {origins.has("asr") ? (
                        <>
                            <OriginBadge origin="asr" /> AI-generated (
                            {work.asrModel}). It contains mistakes; each line
                            shows how confident the model was.
                        </>
                    ) : (
                        <>
                            <OriginBadge origin="human" /> Transcribed by a
                            person
                            {work.translated &&
                                `; translated by ${work.translated}`}
                            .
                        </>
                    )}{" "}
                    Word timings by forced alignment.
                </dd>
                {work.notes && (
                    <>
                        <dt>Notes</dt>
                        <dd>{work.notes}</dd>
                    </>
                )}
                <dt>Data</dt>
                <dd>
                    <a href={work.gitHubLink} target="_blank" rel="noreferrer">
                        View or correct it on GitHub
                    </a>
                    {work.corpusWork && (
                        <>
                            {" · "}
                            <Link to={`/docs/${work.corpusWork}`}>
                                In the text corpus
                            </Link>
                        </>
                    )}
                </dd>
            </dl>
            {videoId != null && (
                <div className="video-dock">
                    <div className="youtube-container center">
                        <YouTuber videoId={videoId} ref={player} />
                    </div>
                </div>
            )}
            {!work.seekable && work.linkStatus == "ok" && work.source && (
                <p className="speech-work-hint">
                    This source cannot be linked to a moment: open it and go to
                    the time shown.
                </p>
            )}
            <ul className="speech-lines speech-transcript">
                {work.lines.map((hit) => (
                    <SpeechLine
                        key={hit.lineNumber}
                        hit={hit}
                        seekable={work.seekable}
                        showEnglish={hasEnglish}
                        onSeek={
                            videoId != null
                                ? (t) => player.current?.seek(t)
                                : undefined
                        }
                    />
                ))}
            </ul>
        </div>
    )
}
