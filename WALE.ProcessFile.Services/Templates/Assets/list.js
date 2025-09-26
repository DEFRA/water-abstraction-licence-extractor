window.onload = function () {
    const urlSearchParams = new URLSearchParams(window.location.search);
    const params = Object.fromEntries(urlSearchParams.entries());
    let bodyEle = document.getElementsByTagName("body")[0];
    
    if (params["showAll"] === "true") {
        bodyEle.className += " show-all";
    }

    window.aiData = {};
    
    if (LOAD_AI) {
        window.loadedOrErrored = 0;

        for (let i = 0; i < data.length; i++) {
            let item = data[i];
            loadScript(item.filename, bodyEle);
        }
    } else {
        window.loadedOrErrored = data.length;
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
    script.src = filename + '/ai-data.jsonp';

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

function filterData(dataSorted, filterType, filterField, filterValue) {
    let returnData = [];
    
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];

        if (filterField !== undefined && filterValue !== undefined && filterValue !== 'All') {
            let value = item[filterField];

            if (filterType === 'Year') {
                let valueParts = value !== undefined ? value.split('-') : "";

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
            }  else if (filterType === 'EmptyOrNotInt') {
                if (filterValue === 'populated' && value === 0) {
                    continue;
                }

                if (filterValue === 'empty' && value !== 0) {
                    continue;
                }
            }  else if (filterType === 'EmptyOrNotArray') {
                if (filterValue === 'populated' && value.length === 0) {
                    continue;
                }

                if (filterValue === 'empty' && value.length > 0) {
                    continue;
                }
            } else if (filterType === 'ArrayValue') {
                if (filterValue !== 'All' && value.indexOf(filterValue) === -1) {
                    continue;
                }
            } else if (value !== filterValue) {
                continue;
            }
        }
        
        returnData.push(item);
    }
    
    return returnData;
}

function getLicence(licenceNumber) {
    for (let itemNumber in data) {
        let item = data[itemNumber];

        if (item.licenceNumber === licenceNumber) {
            return item;
        }
    }

    return null;
}

function getFilename(licenceNumber) {
    for (let itemNumber in data) {
        let item = data[itemNumber];

        if (item.licenceNumber === licenceNumber) {
            return item.filename;
        }
    }

    return false;
}

function licenceInList(licenceNumber) {
    for (let itemNumber in data) {
        let item = data[itemNumber];
        
        if (item.licenceNumber === licenceNumber) {
            return true;
        }
    }

    return false;
}

function populateTable(filterField, filterValue, filterType, sortByField, sortAsc) {
    const tbody1 = document.querySelector("#licencesTable tbody");
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
    
    dataSorted = filterData(dataSorted, filterType, filterField, filterValue);
    window.dataFiltered = dataSorted;
    setTotals();
    
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        
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
        
        let linkedLicencesSb = [];

        if (item.linkedLicences.length > 0) {
            linkedLicencesSb.push('<ul>');
        }
        
        for (let j = 0; j < item.linkedLicences.length; j++) {
            let linkedLicence = item.linkedLicences[j];
            
            if (licenceInList(linkedLicence)) {
                let linkedFilename = getFilename(linkedLicence);
                linkedLicencesSb.push('<li><a href="report.html?filename=' + linkedFilename
                    + '" onclick="openIframe(\'' + linkedFilename + '\'); return false;">' + linkedLicence + '</a></li>');
            } else {
                linkedLicencesSb.push('<li>' + linkedLicence + '</li>');                
            }
        }

        if (item.linkedLicences.length > 0) {
            linkedLicencesSb.push('</ul>');
        }

        let licenceSetsSb = [];

        if (item.shortLicenceSetIds.length > 0 && item.shortLicenceSetIds[0] !== '') {
            licenceSetsSb.push('<ul>');

            for (let j = 0; j < item.licenceSetIds.length; j++) {
                let licenceSetId = item.licenceSetIds[j];
                let shortLicenceSetId = item.shortLicenceSetIds[j] ?? '';
                
                licenceSetsSb.push('<li title="' + licenceSetId + '">' + shortLicenceSetId + '</li>');
            }

            licenceSetsSb.push('</ul>');
        } else {
            licenceSetsSb.push('--');
        }
        
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
            "<td><a href='report.html?filename=" + item.filename + "' onclick=\"openIframe('" + item.filename + "'); return false;\">" + item.filename + "</a></td>" +
            "<td id='" + dashesIfNullOrEmpty(item.licenceNumber) + "'>" + dashesIfNullOrEmpty(item.licenceNumber) + aiLicenceNumberLine + "</td>" +
            "<td class='default-hidden'>" + dashesIfNullOrEmpty(item.licenceHolder) + "</td>" +
            "<td>" + (item.purposes.length > 0 ? purposesSb.join('') : '--') + aiPurposesLine + "</td>" +
            "<td>" + (item.points.length > 0 ? pointsSb.join('') : '--') + aiPointsLine + "</td>" +
            "<td>" + (item.limitsCount > 0 ? item.limitsCount : "--") + aiLimitsFoundLine + "</td>" +
            "<td>" + (item.aggregatesCount > 0 ? item.aggregatesCount : "--") + aiAggregatesFoundLine + "</td>" +
            "<td>" + (item.ocr ? "True" : "False") + "</td>" +
            "<td>" + dashesIfNullOrEmpty(item.issueDate) + aiIssueDateLine + "</td>" +
            "<td>" + dashesIfNullOrEmpty(item.issuer) + aiIssuerLine + "</td>" +
            "<td>" + (item.meansFound ? "True" : "False") + aiMeansLine + "</td>" +
            "<td>" + (item.linkedLicences.length > 0 ? linkedLicencesSb.join('') : "--") + aiLinkedLicencesLine + "</td>" +
            "<td>" + (item.licenceSetIds.length > 0 ? licenceSetsSb.join('') : "--") + "</td>" +
            "</tr>";

        htmlSb.push(html);
    }

    tbody1.innerHTML = htmlSb.join('');
    
    populateLicenceSetTable(dataSorted);
}

function populateLicenceSetTable(dataSorted) {
    let uniqueValues = [];
    let uniqueShortValues = [];
    
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        let ary = item.licenceSetIds;

        for (let j = 1; j < ary.length; j++) {
            let value = ary[j];

            if (uniqueValues.indexOf(value) === -1) {
                uniqueValues.push(value);
                uniqueShortValues.push(item.shortLicenceSetIds[j]);
            }
        }
    }

    let htmlSb = [];
    
    for (let i = 0; i < uniqueValues.length; i++) {
        let color = i % 2 === 0 ? "#F6F6F6" : "#FAFAFA";
        let backgroundCss = "background-color: " + color;
        let licenceSetId = uniqueValues[i];
        let shortLicenceSetId = uniqueShortValues[i];
        
        let licencesInSet = getLicencesInSet(dataSorted, licenceSetId);
        let licenceInSet = licencesInSet[0];
        
        let html =
            "<tr style='" + backgroundCss + "'>" +
                "<td rowspan='" + licencesInSet.length + "'><span title='" + licenceSetId + "'>" + shortLicenceSetId + "</span></td>" +
                "<td>" + licenceInSet.licenceNumber + "</td>" +
                "<td>" + licenceInSet.filename + "</td>" +
            "</tr>";

        htmlSb.push(html);
        
        for (let j = 1; j < licencesInSet.length; j++) {
            let licenceInSet = licencesInSet[j];
            
            html =
                "<tr style='" + backgroundCss + "'>" +
                    "<td>" + licenceInSet.licenceNumber + "</td>" +
                    "<td>" + licenceInSet.filename + "</td>" +
                "</tr>";

            htmlSb.push(html);
        }
    }

    const tbody2 = document.querySelector("#licenceSets tbody");
    tbody2.innerHTML = htmlSb.join('');
}

function getLicencesInSet(dataSorted, licenceSetId) {
    let returnList = [];
    let licenceNumbers = [];
    
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        let ary = item.licenceSetIds;

        for (let j = 1; j < ary.length; j++) {
            let value = ary[j];

            if (value === licenceSetId && licenceNumbers.indexOf(item.licenceNumber) === -1) {
                returnList.push(item);
                licenceNumbers.push(item.licenceNumber);
            }
        }
    }
    
    let parts = licenceSetId.split('-');

    for (let i = 0; i < parts.length; i += 2) {
        let licenceNumber = parts[i];
        let fullLicenceNumber = getFullLicenceNumber(dataSorted, licenceNumber);

        if (licenceNumbers.indexOf(fullLicenceNumber) === -1) {
            let licence = getLicence(fullLicenceNumber) ?? {
                filename: '--',
                licenceNumber: fullLicenceNumber,
            };
            
            licenceNumbers.push(fullLicenceNumber);
            returnList.push(licence);
        }
    }

    returnList.sort(compareItems);
    return returnList;
}

function compareItems(a, b) {
    if (a.licenceNumber < b.licenceNumber) {
        return -1;
    }
    
    if (a.licenceNumber > b.licenceNumber) {
        return 1;
    }
    
    return 0;
}

function getFullLicenceNumber(dataSorted, shortLicenceNumber) {
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        let licenceNumberStripped = item.licenceNumber
            .replaceAll("/", "")
            .replaceAll(" ", "")
            .replaceAll(".", "");
        
        if (licenceNumberStripped === shortLicenceNumber) {
            return item.licenceNumber;
        }
        
        let ary = item.linkedLicences ?? [];

        for (let j = 0; j < ary.length; j++) {
            let value = ary[j];
            licenceNumberStripped = value
                .replaceAll("/", "")
                .replaceAll(" ", "")
                .replaceAll(".", "");

            if (licenceNumberStripped === shortLicenceNumber) {
                return value;
            }
        }
    }
    
    return shortLicenceNumber;
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
    document.getElementById('filename-total').innerHTML = getCount(window.dataFiltered, 'filename', '');
    document.getElementById('licence-number-total').innerHTML = getCount(window.dataFiltered, 'licenceNumber', '');
    document.getElementById('licence-holder-total').innerHTML = getCount(window.dataFiltered, 'licenceHolder', '');
    document.getElementById('purposes-total').innerHTML = getCount(window.dataFiltered, 'purposes', []);
    document.getElementById('points-total').innerHTML = getCount(window.dataFiltered, 'points', []);
    document.getElementById('limits-total').innerHTML = getCount(window.dataFiltered, 'limitsFound', false);
    document.getElementById('aggregates-total').innerHTML = getCount(window.dataFiltered, 'aggregatesFound', false);
    document.getElementById('ocr-total').innerHTML = getCount(window.dataFiltered, 'ocr', false);
    document.getElementById('issue-date-total').innerHTML = getCount(window.dataFiltered, 'issueDate', '');
    document.getElementById('issuer-total').innerHTML = getCount(window.dataFiltered, 'issuer', '');
    document.getElementById('means-total').innerHTML = getCount(window.dataFiltered, 'meansFound', false);
    document.getElementById('linked-licences-total').innerHTML = getCount(window.dataFiltered, 'linkedLicences', false);
    document.getElementById('licence-sets-total').innerHTML = getCount(window.dataFiltered, 'licenceSetIds', false);
}

function getCount(dataFiltered, field, comparisonValue) {
    let count = 0;
    
    for (let i = 0; i < dataFiltered.length; i++) {
        let item = dataFiltered[i];
        let value = item[field];
        
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
        let year = !!value ? value.split('-')[0] : '';
        
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

    let licenceSetsFilter = document.getElementById('licence-sets-filter');
    uniqueValues = [];
    
    let licenceSetsSb = [];
    licenceSetsSb.push('<option value="All">All</option>');

    for (let i = 0; i < data.length; i++) {
        let item = data[i];
        let ary = item["shortLicenceSetIds"];

        for (let j = 1; j < ary.length; j++) {
            let value = ary[j];
            
            if (uniqueValues.indexOf(value) === -1) {
                uniqueValues.push(value);
            }
        }
    }

    uniqueValues.sort();

    for (let i = 0; i < uniqueValues.length; i++) {
        let value = uniqueValues[i];
        licenceSetsSb.push('<option value="' + value + '">' + value + '</option>')
    }

    licenceSetsFilter.innerHTML = licenceSetsSb.join('');
}

function openIframe(filename) {
    document.getElementById('iframeDiv').style.display = 'block';
    document.getElementById('iframe').src = 'report.html?filename=' + filename + '&hideBackLink=true';
}

document.getElementById('closeLink').onclick = function () {
    document.getElementById('iframeDiv').style.display = 'none';
    return false;
}

document.getElementById('maximiseLink').onclick = function () {
    let iframeDiv = document.getElementById('iframeDiv');
    iframeDiv.style.top = '0';
    iframeDiv.style.width = '100%';
    iframeDiv.style.left = '0';
    iframeDiv.style.height = '100%';
    
    return false;
}

document.getElementById('minimiseLink').onclick = function () {
    let iframeDiv = document.getElementById('iframeDiv');
    iframeDiv.style.top = "40px";
    iframeDiv.style.height = "calc(100% - 60px)";
    iframeDiv.style.left = "350px";
    iframeDiv.style.width = "calc(100% - 370px)";

    return false;
}

let licencesLinkEle = document.getElementById('licencesLink');
licencesLinkEle.onclick = function () {
    if (licencesLinkEle.classList.contains('selected')) {
        return false;
    }

    licencesLinkEle.classList.add('selected');
    licenceSetsLinkEle.classList.remove('selected');

    document.getElementById('licences').style.display = 'block';
    document.getElementById('licenceSets').style.display = 'none';
    
    return false;
}

let licenceSetsLinkEle = document.getElementById('licenceSetsLink');
licenceSetsLinkEle.onclick = function () {
    if (licenceSetsLinkEle.classList.contains('selected')) {
        return false;
    }

    licencesLinkEle.classList.remove('selected');
    licenceSetsLinkEle.classList.add('selected');

    document.getElementById('licences').style.display = 'none';
    document.getElementById('licenceSets').style.display = 'block';

    return false;
}

dragElement(document.getElementById("iframeDiv"));

function dragElement(elmnt) {
    let pos1 = 0, pos2 = 0, pos3 = 0, pos4 = 0;
    document.getElementById(elmnt.id + "Header").onmousedown = dragMouseDown;

    function dragMouseDown(e) {
        e = e || window.event;
        e.preventDefault();

        // get the mouse cursor position at startup:
        pos3 = e.clientX;
        pos4 = e.clientY;
        document.onmouseup = closeDragElement;

        // call a function whenever the cursor moves:
        document.onmousemove = elementDrag;
    }

    function elementDrag(e) {
        e = e || window.event;
        e.preventDefault();
        // calculate the new cursor position:
        pos1 = pos3 - e.clientX;
        pos2 = pos4 - e.clientY;
        pos3 = e.clientX;
        pos4 = e.clientY;
        // set the element's new position:
        elmnt.style.top = (elmnt.offsetTop - pos2) + "px";
        elmnt.style.left = (elmnt.offsetLeft - pos1) + "px";
    }

    function closeDragElement() {
        // stop moving when mouse button is released:
        document.onmouseup = null;
        document.onmousemove = null;
    }
}