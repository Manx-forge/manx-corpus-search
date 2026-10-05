import { Box, Modal } from "@mui/material"
import { Link } from "react-router-dom"
import { formatTime, SpeechHit, youTubeId } from "../api/SpeechApi"
import YouTuber from "./YouTuber"

/** As the audio attestation popup's (AudioAttestationModal) */
const style = {
    position: "absolute",
    top: "50%",
    left: "50%",
    transform: "translate(-50%, -50%)",
    maxWidth: 640,
    width: "calc(100% - 32px)",
    maxHeight: "85vh",
    overflowY: "auto",
    bgcolor: "var(--c-surface, #fff)",
    color: "var(--c-ink, inherit)",
    borderRadius: 2,
    boxShadow: 24,
    p: 3,
}

/** How a hit can be played in the page: an embedded YouTube video, or the
 * source's own audio file (which seeks by its #t= fragment); null when the
 * source cannot be played from a moment (Clilstore, no public link) */
export const playable = (
    hit: SpeechHit,
    source?: string,
): { youTube: string } | { audio: string } | null => {
    if (hit.link == null || hit.time == null) {
        return null
    }
    const video = youTubeId(source)
    if (video != null && /[?&]t=\d+/.test(hit.link)) {
        return { youTube: video }
    }
    return /\.(mp3|m4a|ogg|wav)#t=\d+$/i.test(hit.link)
        ? { audio: hit.link }
        : null
}

/** The recording, playing from the moment of the line a time was clicked on:
 * the reader asked to hear it, so it autoplays */
export const SpeechPlayer = (props: {
    ident: string
    name: string
    source?: string
    hit: SpeechHit
    onClose: () => void
}) => {
    const { hit } = props
    const media = playable(hit, props.source)
    return (
        <Modal open onClose={props.onClose}>
            <Box sx={style} className="speech-player-modal">
                <div className="speech-player-title">{props.name}</div>
                {media != null && "youTube" in media && (
                    <div className="youtube-container center">
                        <YouTuber
                            videoId={media.youTube}
                            startSeconds={hit.time}
                            autoplay
                        />
                    </div>
                )}
                {media != null && "audio" in media && (
                    <audio
                        className="speech-player-audio"
                        src={media.audio}
                        controls
                        autoPlay
                    />
                )}
                <p className="speech-player-line">
                    <b>{formatTime(hit.time ?? 0)}</b> {hit.manx}
                </p>
                <div className="speech-player-links">
                    <a href={hit.link} target="_blank" rel="noreferrer">
                        Open at the source
                    </a>
                    {" · "}
                    <Link to={`/speech/${props.ident}`}>Whole transcript</Link>
                </div>
            </Box>
        </Modal>
    )
}
