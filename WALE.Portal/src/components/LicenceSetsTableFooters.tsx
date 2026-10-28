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
        </>
    );
}

export default LicenceSetsTableFooters;