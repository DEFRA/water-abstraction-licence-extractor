window.onload = function () {
    const tbody1 = document.getElementsByTagName("tbody")[0];
    let htmlSb = [];

    for (let i = 0; i < data.length; i++) {
        let item = data[i];

        let color = i % 2 === 0 ? "#F6F6F6" : "#FAFAFA";
        let backgroundCss = "background-color: " + color;

        let purposesSb = [];
        purposesSb.push('<ul>');
        
        for (let j = 0; j < item.purposes.length; j++) {
            let purpose = item.purposes[j];
            purposesSb.push('<li>' + purpose + '</li>');
        }
        
        purposesSb.push('</ul>');

        let pointsSb = [];
        pointsSb.push('<ul>');

        for (let j = 0; j < item.points.length; j++) {
            let point = item.points[j];
            pointsSb.push('<li>' + point + '</li>');
        }

        pointsSb.push('</ul>');        
                
        let html =
            "<tr style='" + backgroundCss + "'>" +
            "<td style='text-align: center'><img src='" + item.imagePath + "' style='height: 80px' alt='No image found' onerror='this.style.display='none' /></td>" +
            "<td><a href='report.html?filename=" + item.filename + "'>" + item.filename + "</a></td>" +
            "<td>" + item.licenceNumber + "</td>" +
            "<td>" + item.licenceHolder + "</td>" +
            "<td>" + purposesSb.join('') + "</td>" +
            "<td>" + pointsSb.join('') + "</td>" +
            "<td>" + (item.limitsFound ? "True" : "False") + "</td>" +
            "<td>" + (item.aggregatesFound ? "True" : "False") + "</td>" +
            "<td>" + (item.ocr ? "True" : "False") + "</td>" +
            "<td>" + item.issueDate + "</td>" +
            "<td>" + item.issuer + "</td>" +
            "<td>" + (item.meansFound ? "True" : "False") + "</td>" +
            "<td>" + (item.linkedLicences ? "True" : "False") + "</td>" +
            "</tr>";

        htmlSb.push(html);
    }
    
    tbody1.innerHTML = htmlSb.join('');

    setTotals();
    populateDropdowns();
};

function setTotals() {
    document.getElementById('filename-total').innerHTML = getCount('filename', '');
    document.getElementById('licence-number-total').innerHTML = getCount('licenceNumber', '');
    document.getElementById('licence-holder-total').innerHTML = getCount('licenceHolder', '');
    document.getElementById('purposes-total').innerHTML = getCount('purposes', []);
    document.getElementById('points-total').innerHTML = getCount('points', []);
    document.getElementById('limits-total').innerHTML = getCount('limitsFound', false);
    document.getElementById('aggregates-total').innerHTML = getCount('aggregatesFound', false);
    document.getElementById('ocr-total').innerHTML = getCount('ocr', false);
    document.getElementById('issue-date-total').innerHTML = getCount('issueDate', '');
    document.getElementById('issuer-total').innerHTML = getCount('issuer', '');
    document.getElementById('means-total').innerHTML = getCount('meansFound', false);
    document.getElementById('linked-licences-total').innerHTML = getCount('linkedLicences', false);
}

function getCount(field, comparisonValue) {
    let count = 0;
    
    for (let i = 0; i < data.length; i++) {
        let item = data[i];
        var value = item[field];
        
        if (value !== comparisonValue) {
            count++;
        }
    }
    
    return count;
}

function populateDropdowns() {
    let issuersFilter = document.getElementById('issuer-filter');
    let uniqueValues = []

    let issuerSb = [];
    issuerSb.push('<option>All</option>')
    
    for (let i = 0; i < data.length; i++) {
        let item = data[i];
        let value = item["issuer"];

        if (uniqueValues.indexOf(value) === -1) {
            uniqueValues.push(value);
            issuerSb.push('<option>' + value + '</option>')
        }
    }

    issuersFilter.innerHTML = issuerSb.join('');
}