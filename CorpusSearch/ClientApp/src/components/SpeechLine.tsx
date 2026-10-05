import { HighlightRange } from "../api/SearchApi"
import { formatTime, Origin, SpeechHit } from "../api/SpeechApi"
import { markChunks } from "./LineText"
import "../routes/Speech.css"

/** Confidence bands of AI lines (D37): calibrated so green ≈ 7%, amber ≈ 19%
 * and red ≈ 34% word error rate */
export const confidenceBand = (
    confidence: number,
): "green" | "amber" | "red" =>
    confidence >= 90 ? "green" : confidence >= 60 ? "amber" : "red"

/** "Human" or "AI · 87% confidence": every line says who transcribed it (D4,
 * D30). Without a confidence, a plain "AI" (a recording rather than a line) */
export const OriginBadge = (props: { origin?: Origin; confidence?: number }) =>
    props.origin == "asr" ? (
        props.confidence == null ? (
            <span
                className="speech-badge speech-badge-ai"
                title="AI-generated transcript"
            >
                AI
            </span>
        ) : (
            <span
                className={`speech-badge speech-badge-ai band-${confidenceBand(props.confidence)}`}
                title={`AI-generated transcript, ${props.confidence}% confidence`}
            >
                AI · {props.confidence}% confidence
            </span>
        )
    ) : (
        <span
            className="speech-badge speech-badge-human"
            title="Transcribed by a person"
        >
            Human
        </span>
    )

/** The moment a line is spoken: a control playing it in the page (onSeek, an
 * embedded player; onPlay, the popup player) when there is one, else a link
 * into the source, or the time to find by hand */
export const TimeLink = (props: {
    hit: SpeechHit
    seekable: boolean
    onSeek?: (time: number) => void
    onPlay?: () => void
}) => {
    const { hit, seekable, onSeek, onPlay } = props
    if (hit.time == null) {
        return null
    }
    const label = formatTime(hit.time)
    if (onSeek || onPlay) {
        return (
            <button
                className="speech-time"
                title="Play from here"
                onClick={() => (onSeek ? onSeek(hit.time ?? 0) : onPlay?.())}
            >
                ▶ {label}
            </button>
        )
    }
    if (hit.link == null) {
        return <span className="speech-time speech-time-plain">{label}</span>
    }
    return (
        <a
            className="speech-time"
            href={hit.link}
            target="_blank"
            rel="noreferrer"
            title={
                seekable
                    ? "Listen at the source"
                    : "Open the source and go to this time"
            }
        >
            {seekable ? "▶ " : ""}
            {label}
        </a>
    )
}

export const Highlighted = (props: {
    text?: string
    highlights?: HighlightRange[]
}) => <>{markChunks(props.text ?? "", props.highlights ?? [])}</>

/** One line of a recording: time, speaker, Manx (and English), and who transcribed it */
export const SpeechLine = (props: {
    hit: SpeechHit
    seekable: boolean
    onSeek?: (time: number) => void
    onPlay?: () => void
    showEnglish: boolean
}) => {
    const { hit } = props
    return (
        <li className="speech-line">
            <span className="speech-line-time">
                <TimeLink
                    hit={hit}
                    seekable={props.seekable}
                    onSeek={props.onSeek}
                    onPlay={props.onPlay}
                />
            </span>
            <span className="speech-line-text">
                {hit.speaker && (
                    <span className="speech-speaker">{hit.speaker}</span>
                )}
                <span className="speech-manx">
                    <Highlighted
                        text={hit.manx}
                        highlights={hit.manxHighlights}
                    />
                </span>
                {props.showEnglish && hit.english && (
                    <span className="speech-english">
                        <Highlighted
                            text={hit.english}
                            highlights={hit.englishHighlights}
                        />
                    </span>
                )}
            </span>
            <span className="speech-line-origin">
                <OriginBadge origin={hit.origin} confidence={hit.confidence} />
            </span>
        </li>
    )
}
