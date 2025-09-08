window.onload = function () {
    const urlSearchParams = new URLSearchParams(window.location.search);
    const params = Object.fromEntries(urlSearchParams.entries());
    let bodyEle = document.getElementsByTagName("body")[0];
    
    if (params["showAll"] === "true") {
        bodyEle.className += " show-all";
    }

    window.aiData = {};
    window.loadedOrErrored = 0;
    
    for (let i = 0; i < data.length; i++) {
        let item = data[i];
        loadScript(item.filename, bodyEle);
    }
    
    let timeCount = 0;
    let checkInterval = 50;
    let timeout = 3000;
    
    let checksBeforeTimeout = timeout / checkInterval;

    let intervalId = setInterval(function () {
        if (window.loadedOrErrored !== data.length || timeCount++ >= checksBeforeTimeout) return;

        setup();

        clearInterval(intervalId);
        intervalId = null;
    }, checkInterval);
};

function setup() {
    populateTable();
    setTotals();
    populateDropdowns();

    window.sortedAsc = true;
    window.sortedBy = "filename";
    
    let selects = document.getElementsByTagName("select");
    
    for (let i = 0; i < selects.length; i++) {
        let select = selects[i];
        addChangeEvent(select);
    }
}

function loadScript(filename, bodyEle) {
    let script = document.createElement('script');
    script.type = 'text/javascript';
    script.src = '../Data/' + filename + '.js';

    script.onload = function () {
        window.loadedOrErrored += 1;
    };
    
    script.onerror = function () {
        window.loadedOrErrored += 1;
    }

    bodyEle.appendChild(script);
}

function resetFilters(except) {
    window.resetting = true;
    let selects = document.getElementsByTagName("select");

    for (let i = 0; i < selects.length; i++) {
        let select = selects[i];
        if (select === except) continue;
        
        select.selectedIndex = 0;
    }

    window.resetting = false;
}

function sortBy(filterField) {
    let previousSortedAsc = window.sortedAsc;
    
    window.sortedAsc = (window.sortedBy !== filterField);
    if (window.sortedAsc === previousSortedAsc) window.sortedAsc = !window.sortedAsc;
    
    window.sortedBy = filterField;
    
    populateTable(undefined, undefined, undefined, filterField, sortedAsc);
}

function populateTable(filterField, filterValue, filterType, sortByField, sortAsc) {
    const tbody1 = document.getElementsByTagName("tbody")[0];
    let htmlSb = [];

    let dataSorted = data.slice(0);
    
    if (!!sortByField) {
        dataSorted.sort(function(a, b) {
            if (a[sortByField] === b[sortByField]) {
                return 0;
            }

            return (a[sortByField] < b[sortByField]) ? -1 : 1;
        });
        
        if (!sortAsc) {
            dataSorted.reverse();
        }
    }
    
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];

        if (filterField !== undefined && filterValue !== undefined && filterValue !== 'All') {
            let value = item[filterField];
            
            if (filterType === 'Year') {
                let valueParts = value.split('-');
                
                if (valueParts[0] !== filterValue) {
                    continue;
                }
            } else if (filterType === 'Bool') {
                if (filterValue === 'true' && !value) {
                    continue;
                }

                if (filterValue === 'false' && value) {
                    continue;
                }
            } else if (filterType === 'EmptyOrNot') {
                if (filterValue === 'populated' && value === '') {
                    continue;
                }

                if (filterValue === 'empty' && value !== '') {
                    continue;
                }
            } else if (filterType === 'EmptyOrNotArray') {
                if (filterValue === 'populated' && value.length === 0) {
                    continue;
                }

                if (filterValue === 'empty' && value.length > 0) {
                    continue;
                }
            } else if (value !== filterValue) {
                continue;
            }
        }
        
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

        let filenameNoExtension = item.filename.split('.')[0];
        let filenameNoSpacesOrDashes = filenameNoExtension
            .replaceAll("-", "")
            .replaceAll(" ", "");
        let aiItem = window.aiData[filenameNoSpacesOrDashes];

        let aiPurposesSb = [];
        let aiPointsSb = [];

        let aiLicenceNumberLine = "";
        let aiLimitsFoundLine = "";
        let aiAggregatesFoundLine = "";
        let aiIssueDateLine = "";
        let aiIssuerLine = "";
        let aiMeansLine = "";
        let aiLinkedLicencesLine = "";
        let aiPurposesLine = "";
        let aiPointsLine = "";
        
        if (!!aiItem) {
            aiPurposesSb.push('<ul>');

            for (let j = 0; j < aiItem.purposes.length; j++) {
                let purpose = aiItem.purposes[j].description;
                aiPurposesSb.push('<li>' + purpose + '</li>');
            }

            aiPurposesSb.push('</ul>');
            aiPointsSb.push('<ul>');

            for (let j = 0; j < aiItem.points.length; j++) {
                let point = aiItem.points[j].description;
                aiPointsSb.push('<li>' + point + '</li>');
            }

            aiPointsSb.push('</ul>');

            let issueDate = aiItem.licenceVersion.issueDate;
            if (!!issueDate) {
                issueDate = issueDate.split('T')[0];
            }
            
            let aggregates = aiItem.abstractionLimits.aggregates;
            let hasLinkedLicences = false;
            
            for (let j = 0; j < aggregates.length; j++) {
                let aggregate = aggregates[j];
                
                if (aggregate.linkedLicences.length > 0) {
                    hasLinkedLicences = true;
                    break;
                }
            }
            
            aiLicenceNumberLine = "<br /><span class='ai-line'>" + dashesIfNullOrEmpty(aiItem.licenceNumber) + "</span>";
            aiLimitsFoundLine = "<br /><span class='ai-line'>" + (aiItem.abstractionLimits.individual.length > 0
                || aiItem.abstractionLimits.aggregates.length > 0 ? "True" : "False") + "</span>";
            aiAggregatesFoundLine = "<br /><span class='ai-line'>" + (aggregates.length > 0 ? "True" : "False") + "</span>";
            aiIssueDateLine = "<br /><span class='ai-line'>" + dashesIfNullOrEmpty(issueDate) + "</span>";
            aiIssuerLine = "<br /><span class='ai-line'>" + dashesIfNullOrEmpty(aiItem.licenceVersion.issuer) + "</span>";
            aiMeansLine = "<br /><span class='ai-line'>" + (aiItem.meansOfAbstraction.length > 0 ? "True" : "False") + "</span>";
            aiLinkedLicencesLine = "<br /><span class='ai-line'>" + (hasLinkedLicences ? "True" : "False") + "</span>";
            aiPurposesLine = "\n<span class='ai-line'>" + (aiItem.purposes.length > 0 ? aiPurposesSb.join('') : '--') + "</span>";
            aiPointsLine = "\n<span class='ai-line'>" + (aiItem.points.length > 0 ? aiPointsSb.join('') : '--') + "</span>";
        }
                
        let html =
            "<tr style='" + backgroundCss + "'>" +
            "<td style='text-align: center'><img src='" + item.imagePath + "' style='height: 80px' alt='No image found' onerror='this.style.display='none' /></td>" +
            "<td><a href='report.html?filename=" + item.filename + "'>" + item.filename + "</a></td>" +
            "<td>" + dashesIfNullOrEmpty(item.licenceNumber) + aiLicenceNumberLine + "</td>" +
            "<td class='default-hidden'>" + dashesIfNullOrEmpty(item.licenceHolder) + "</td>" +
            "<td>" + (item.purposes.length > 0 ? purposesSb.join('') : '--') + aiPurposesLine + "</td>" +
            "<td>" + (item.points.length > 0 ? pointsSb.join('') : '--') + aiPointsLine + "</td>" +
            "<td>" + (item.limitsFound ? "True" : "False") + aiLimitsFoundLine + "</td>" +
            "<td>" + (item.aggregatesFound ? "True" : "False") + aiAggregatesFoundLine + "</td>" +
            "<td>" + (item.ocr ? "True" : "False") + "</td>" +
            "<td>" + dashesIfNullOrEmpty(item.issueDate) + aiIssueDateLine + "</td>" +
            "<td>" + dashesIfNullOrEmpty(item.issuer) + aiIssuerLine + "</td>" +
            "<td>" + (item.meansFound ? "True" : "False") + aiMeansLine + "</td>" +
            "<td>" + (item.linkedLicences ? "True" : "False") + aiLinkedLicencesLine + "</td>" +
            "</tr>";

        htmlSb.push(html);
    }

    tbody1.innerHTML = htmlSb.join('');
}

function dashesIfNullOrEmpty(str) {
    if (str == null || str === '') {
        return "--";
    }
    
    return str;
}

function addChangeEvent(select) {
    let dataField = select.getAttribute("data-field");
    let dataType = select.getAttribute("data-type");
    
    select.addEventListener("change", function (event) {
        if (window.resetting) {
            return;
        }

        resetFilters(select);
        populateTable(dataField, event.target.value, dataType, null);
    });
}

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
    let uniqueValues = [];

    let issuerSb = [];
    issuerSb.push('<option value="All">All</option>');
    
    for (let i = 0; i < data.length; i++) {
        let item = data[i];
        let value = item["issuer"];

        if (uniqueValues.indexOf(value) === -1) {
            uniqueValues.push(value);
        }
    }

    uniqueValues.sort();

    for (let i = 0; i < uniqueValues.length; i++) {
        let value = uniqueValues[i];
        issuerSb.push('<option value="' + value + '">' + value + '</option>')
    }
    
    issuersFilter.innerHTML = issuerSb.join('');

    let issueDateFilter = document.getElementById('issue-date-filter');
    uniqueValues = [];

    let issueDateSb = [];
    issueDateSb.push('<option value="All">All</option>');

    for (let i = 0; i < data.length; i++) {
        let item = data[i];
        let value = item["issueDate"];
        let year = value.split('-')[0];
        
        if (uniqueValues.indexOf(year) === -1 && year !== '') {
            uniqueValues.push(year);
        }
    }
    
    uniqueValues.sort();
    uniqueValues.reverse();

    for (let i = 0; i < uniqueValues.length; i++) {
        let year = uniqueValues[i];
        
        if (year >= 1900) {
            issueDateSb.push('<option value="' + year + '">' + year + '</option>')
        }
    }

    issueDateSb.push('<option value="">--</option>');
    issueDateFilter.innerHTML = issueDateSb.join('');
}