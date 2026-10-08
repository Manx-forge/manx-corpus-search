import { ReactNode } from "react"
import { Link, useLocation } from "react-router-dom"
import { NavMenu } from "./NavMenu"
import { isDictionaryHost } from "../utils/Host"

export const Layout = (props: {
    onRefresh: () => void
    children: ReactNode
}) => {
    return (
        <div>
            <NavMenu onRefreshState={props.onRefresh} />
            <div className="page-main">
                {props.children}
                <SiteFooter />
            </div>
        </div>
    )
}

const SiteFooter = () => (
    <footer className="site-footer">
        {useLocation().pathname.startsWith("/speech") ? (
            <span>
                The speech corpus is led and maintained by Chris Bartley as part
                of doctoral research into speech technology for endangered
                languages at the University of Sheffield (
                <a
                    href="https://chris-sj-bartley.github.io/"
                    target="_blank"
                    rel="noreferrer"
                >
                    read more
                </a>
                ). Incorporated into the Manx Corpus in collaboration with{" "}
                <a
                    href="https://github.com/david-allison"
                    target="_blank"
                    rel="noreferrer"
                >
                    David Allison
                </a>
                .<br />
                With thanks to all the creators of the original recordings.
            </span>
        ) : (
            <span>
                Maintained by{" "}
                <a
                    href="https://github.com/david-allison"
                    target="_blank"
                    rel="noreferrer"
                >
                    David Allison
                </a>
                {/* the speech credit belongs to the corpus site's front door */}
                {!isDictionaryHost() && (
                    <>
                        {" "}
                        · <Link to="/speech">Speech corpus</Link> by Chris
                        Bartley
                    </>
                )}
                <br />
                With thanks to{" "}
                <Link to="/contributions">all the volunteers</Link> who
                transcribe, translate &amp; contribute texts.
            </span>
        )}
        <span>
            <a
                href="https://github.com/david-allison/manx-corpus-search"
                target="_blank"
                rel="noreferrer"
            >
                Free &amp; open source on GitHub
            </a>
        </span>
    </footer>
)
