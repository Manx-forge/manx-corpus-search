import { afterEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { confidenceBand, excerpt, OriginBadge, SpeechLine } from "./SpeechLine"
import { formatTime, SpeechHit, youTubeId } from "../api/SpeechApi"

afterEach(cleanup)

const hit: SpeechHit = {
    lineNumber: 2,
    manx: "va moddey mooar echey",
    english: "there was a big dog at him",
    origin: "asr",
    confidence: 72,
    manxHighlights: [{ start: 3, end: 9 }],
    time: 75,
    link: "https://www.youtube.com/watch?v=abcdefghijk&t=75s",
}

describe("confidenceBand", () => {
    it("bands AI confidence as calibrated (D37)", () => {
        expect(confidenceBand(90)).toBe("green")
        expect(confidenceBand(89)).toBe("amber")
        expect(confidenceBand(60)).toBe("amber")
        expect(confidenceBand(59)).toBe("red")
    })
})

describe("OriginBadge", () => {
    it("shows an AI line's confidence, coloured by its band", () => {
        const { container } = render(
            <OriginBadge origin="asr" confidence={95} />,
        )
        screen.getByText("AI · 95% confidence")
        expect(container.querySelector(".band-green")).not.toBeNull()
    })

    it("marks human lines", () => {
        render(<OriginBadge origin="human" />)
        screen.getByText("Human")
    })
})

describe("SpeechLine", () => {
    it("links to the moment at the source, and highlights the match", () => {
        const { container } = render(
            <SpeechLine hit={hit} seekable={true} showEnglish={false} />,
        )
        const link = screen.getByRole("link", { name: /1:15/ })
        expect(link.getAttribute("href")).toBe(hit.link)
        expect(container.querySelector("mark")?.textContent).toBe("moddey")
        expect(screen.queryByText(hit.english ?? "")).toBeNull()
    })

    it("seeks an embedded player instead, when given one", () => {
        const onSeek = vi.fn()
        render(
            <SpeechLine
                hit={hit}
                seekable={true}
                showEnglish={true}
                onSeek={onSeek}
            />,
        )
        fireEvent.click(screen.getByRole("button", { name: /1:15/ }))
        expect(onSeek).toHaveBeenCalledWith(75)
        screen.getByText(hit.english ?? "")
    })

    it("shows the time as text when there is no link", () => {
        render(
            <SpeechLine
                hit={{ ...hit, link: undefined }}
                seekable={false}
                showEnglish={false}
            />,
        )
        expect(screen.queryByRole("link")).toBeNull()
        screen.getByText("1:15")
    })
})

describe("formatTime", () => {
    it("formats seconds as m:ss, or h:mm:ss past an hour", () => {
        expect(formatTime(5)).toBe("0:05")
        expect(formatTime(75.9)).toBe("1:15")
        expect(formatTime(3725)).toBe("1:02:05")
    })
})

describe("youTubeId", () => {
    it("finds the video id in the forms of YouTube URL", () => {
        expect(youTubeId("https://www.youtube.com/watch?v=abcdefghijk")).toBe(
            "abcdefghijk",
        )
        expect(
            youTubeId("https://youtube.com/watch?feature=x&v=abcdefghijk"),
        ).toBe("abcdefghijk")
        expect(youTubeId("https://youtu.be/abcdefghijk")).toBe("abcdefghijk")
        expect(youTubeId("https://example.org/a.mp3")).toBeNull()
        expect(youTubeId(undefined)).toBeNull()
    })
})

describe("excerpt", () => {
    const text = "a b c d e f moddey g h i j k"
    const moddey = { start: 12, end: 18 }

    it("cuts a long line to the match and its context, moving the highlight", () => {
        const cut = excerpt(text, [moddey], 2)
        expect(cut.text).toBe("… e f moddey g h …")
        const h = cut.highlights?.[0] ?? { start: 0, end: 0 }
        expect(cut.text.slice(h.start, h.end)).toBe("moddey")
    })

    it("centres on the first match, dropping later ones outside the cut", () => {
        const long = "x x x moddey x x x x x x x x x x moddey"
        const cut = excerpt(
            long,
            [
                { start: 6, end: 12 },
                { start: 33, end: 39 },
            ],
            2,
        )
        expect(cut.text).toBe("… x x moddey x x …")
        expect(cut.highlights).toHaveLength(1)
    })

    it("keeps a line short enough already, or one without matches", () => {
        expect(excerpt(text, [moddey], 20).text).toBe(text)
        expect(excerpt(text, undefined, 2).text).toBe(text)
    })
})
