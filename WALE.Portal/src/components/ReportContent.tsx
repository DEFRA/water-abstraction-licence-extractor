interface ReportContentProps {
    filename: string;
}

export function ReportContent({ filename }: ReportContentProps) {
    // TODO: Replace with your actual report implementation
    return (
        <div style={{ padding: '20px' }}>
            <h2>Report: {filename}</h2>
            <p>This is where your report content would go.</p>
            <p>You can load data based on the filename prop.</p>
        </div>
    );
}