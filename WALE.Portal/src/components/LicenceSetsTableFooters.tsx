export function LicenceSetsTableFooters({totals}: {totals?: any}) {
    return (
        <>
            <tr style={{fontWeight: 'bold'}}>
                <td>Total</td>
                <td id="ls-licence-set-total">{totals?.licenceSetsCount}</td>
                <td id="ls-types-total">{totals?.licenceSetTypesCount}</td>
                <td id="ls-licence-number-total">{totals?.licenceSetsLicenceNumberCount}</td>
                <td id="ls-filename-total">{totals?.licenceSetsFilenameCount}</td>
            </tr>
            <tr>
                <td colSpan={3}></td>
                <td style={{verticalAlign: 'top', fontWeight: 'normal', fontSize: '10pt'}}>
                            <span
                                style={{
                                    fontWeight: 'bold',
                                    textDecoration: 'underline',
                                    color: 'lightseagreen'
                                }}>Blue</span> -
                    Mentioned in limits<br/>
                    <span style={{
                        fontWeight: 'bold',
                        textDecoration: 'underline'
                    }}>Black</span> - Mentioned<br/>
                    <span style={{
                        fontWeight: 'bold',
                        textDecoration: 'underline',
                        color: 'orange'
                    }}>Orange</span> -
                    Mentioned + back linked<br/>
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
                    }}>Pink</span> -
                    Partially encompassed in
                </td>
                <td></td>
            </tr>
        </>
    );
}

export default LicenceSetsTableFooters;