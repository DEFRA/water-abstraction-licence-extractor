export function LicencesTableFooters() {

    return (<>
        <tr style={{fontWeight: 'bold'}}>
            <td>Total</td>
            <td id="filename-total"></td>
            <td id="licence-number-total"></td>
            <td id="licence-holder-total" className="default-hidden"></td>
            <td id="purposes-total"></td>
            <td id="points-total"></td>
            <td id="limits-total"></td>
            <td id="aggregates-total"></td>
            <td id="ocr-total"></td>
            <td id="issue-date-total"></td>
            <td id="issuer-total"></td>
            <td id="means-total"></td>
            <td id="linked-licences-total"></td>
            <td id="licence-sets-total"></td>
            <td id="status-total"></td>
        </tr>
        <tr>
            <td colSpan={11}></td>
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
                    color: '888', fontWeight: 'bold'
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
    </>);
}

export default LicencesTableFooters;