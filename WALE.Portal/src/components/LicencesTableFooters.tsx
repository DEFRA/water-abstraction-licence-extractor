import type {Totals} from '../utils/useTotals';

interface LicencesTableFootersProps {
    totals: Totals;
}

export function LicencesTableFooters({ totals }: LicencesTableFootersProps) {
    return (
        <>
            <tr style={{fontWeight: 'bold'}}>
                <td>Total</td>
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
                <td className={'default-hidden'}>{totals.licenceSets}</td>
                <td>{totals.latestLicenceSectionVerifications}</td>
            </tr>
            <tr>
                <td colSpan={14}></td>
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
                    <span
                        style={{
                            fontWeight: 'bold',
                            textDecoration: 'underline',
                            color: 'lightseagreen'
                        }}>Blue</span> - Mentioned in limits<br/>
                    <span
                        style={{fontWeight: 'bold', textDecoration: 'underline', color: '#888'}}>Grey</span> -
                    Back linked<br/>
                    <span style={{fontWeight: 'bold', textDecoration: 'underline'}}>Black</span> -
                    Mentioned<br/>
                    <span style={{
                        fontWeight: 'bold',
                        textDecoration: 'underline',
                        color: 'orange'
                    }}>Orange</span> - Mentioned + back linked<br/>
                    <span
                        style={{
                            fontWeight: 'bold',
                            textDecoration: 'underline',
                            color: 'forestgreen'
                        }}>Green</span> -
                    Fully encompassed in<br/>
                    <span style={{
                        fontWeight: 'bold',
                        textDecoration: 'underline',
                        color: 'deeppink'
                    }}>Pink</span> - Partially encompassed in
                </td>
                <td></td>
            </tr>
        </>
    );
}

export default LicencesTableFooters;