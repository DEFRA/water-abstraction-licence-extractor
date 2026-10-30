interface DocumentTextViewProps {
    text: string;
}

/// Matches the banner DocumentTextService writes between pages. The two must agree: a change
/// there shows up here as the marker appearing verbatim in the body text.
const pageBanner = /^--- Page (\d+) ---\n?/m;

function toPages(text: string) {
    const [beforeFirstBanner, ...bannerThenBody] = text.split(
        new RegExp(pageBanner.source, 'm'));

    const pages: {number: string | null, body: string}[] = [];

    // A single-page document gets no banner, so its text arrives here whole.
    if (beforeFirstBanner.trim()) {
        pages.push({number: null, body: beforeFirstBanner});
    }

    for (let index = 0; index < bannerThenBody.length; index += 2) {
        pages.push({
            number: bannerThenBody[index],
            body: bannerThenBody[index + 1] ?? ''
        });
    }

    return pages;
}

export function DocumentTextView({text}: DocumentTextViewProps) {
    const pages = toPages(text);

    return (
        <pre style={{whiteSpace: 'pre-wrap', fontSize: '0.85em', margin: 0}}>
            {pages.map((page, index) => (
                <div key={page.number ?? index}>
                    {page.number && (
                        <div style={{
                            fontWeight: 'bold',
                            fontFamily: 'sans-serif',
                            fontSize: '1.1em',
                            color: '#555',
                            borderBottom: '1px solid #ddd',
                            paddingBottom: '2px',
                            marginTop: index > 0 ? '1.5em' : 0,
                            marginBottom: '0.5em'
                        }}>
                            Page {page.number}
                        </div>
                    )}
                    {page.body.replace(/\n+$/, '')}
                </div>
            ))}
        </pre>
    );
}

export default DocumentTextView;
