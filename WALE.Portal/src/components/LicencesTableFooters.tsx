import type {Totals} from '../utils/useTotals';
import { CumulativeFilterCounts } from '../api/generated/apiClient.ts'

interface LicencesTableFootersProps {
    totals: Totals;
    cumulativeTotals: CumulativeFilterCounts | undefined ;
}

export function LicencesTableFooters({ totals, cumulativeTotals }: LicencesTableFootersProps) {
    return (
        <>
            <tr style={{fontWeight: 'bold'}}>
                <td>Current Page Total</td>
                <td>{totals.licenceNumber}</td>
                <td className="default-hidden">{totals.licenceHolder}</td>
                <td>{totals.purposes}</td>
                <td>{totals.points}</td>
                <td>{totals.limitsCount}</td>
                <td>{totals.aggregatesCount}</td>
                <td>{totals.ocr}</td>
                <td>{totals.issueDate}</td>
                <td>{totals.issuer}</td>
                <td>{totals.meansFound}</td>
                <td>{totals.linkedLicences}</td>
                <td>{totals.licenceSets}</td>
                <td>{totals.licenceSectionVerifications}</td>
            </tr>
            <tr style={{fontWeight: 'bold'}}>
                <td>Total All</td>
                <td>{cumulativeTotals?.licenceNumbers}</td>
                <td className="default-hidden">{totals.licenceHolder}</td>
                <td>{cumulativeTotals?.purposes}</td>
                <td>{cumulativeTotals?.points}</td>
                <td>{cumulativeTotals?.absLimits}</td>
                <td>{cumulativeTotals?.aggregates}</td>
                <td>{cumulativeTotals?.ocr}</td>
                <td>{cumulativeTotals?.issueDates}</td>
                <td>{cumulativeTotals?.issuers}</td>
                <td>{cumulativeTotals?.meansOfAbs}</td>
                <td>{cumulativeTotals?.linkedLicences}</td>
                <td>{totals.licenceSets}</td>
                <td>{cumulativeTotals?.licenceSectionVerifications}</td>
            </tr>
            <tr>
                <td colSpan={15}></td>
                <td></td>
            </tr>
            <tr>
                <td colSpan={10}></td>
                <td style={{
                    fontWeight: 'normal', verticalAlign: 'top', fontSize: '10pt'
                }}>
                    <span style={{
                        color: 'lightseagreen', fontWeight: 'bold'
                    }}>Blue</span> - Mentioned in limits<br/>
                    &nbsp;&nbsp;
                    <span style={{
                        color: 'lightseagreen', fontWeight: 'bold', textDecoration: 'underline'
                    }}>+underlined</span> - File found<br/>
                    <span style={{
                        color: '#888', fontWeight: 'bold'
                    }}>Grey</span> - Back linked<br/>
                    &nbsp;&nbsp;
                    <span style={{
                        color: '#888', fontWeight: 'bold', textDecoration: 'underline'
                    }}>+underlined</span> - File found<br/>
                    <span style={{fontWeight: 'bold'}}>Black</span> - Mentioned<br/>
                    &nbsp;&nbsp;
                    <span style={{
                        fontWeight: 'bold', textDecoration: 'underline'
                    }}>+underlined</span> - File found
                </td>
                <td style={{fontWeight: 'normal', verticalAlign: 'top', fontSize: '10pt'}}>
                    <span style={{
                        fontWeight: 'bold',
                        textDecoration: 'underline',
                        color: 'deeppink'
                    }}>Pink</span> - Grouped by abstraction limits<br/>
                    <span
                        style={{
                            fontWeight: 'bold',
                            textDecoration: 'underline',
                            color: 'forestgreen'
                        }}>Green</span> - Single licence only<br/>
                    <span style={{fontWeight: 'bold', textDecoration: 'underline'}}>Black</span> - Others<br/>
                    <span style={{
                        fontWeight: 'bold',
                        textDecoration: 'underline',
                        color: 'orange'
                    }}>Orange</span> - Unknown

                </td>
                <td></td>
            </tr>
        </>
    );
}

export default LicencesTableFooters;