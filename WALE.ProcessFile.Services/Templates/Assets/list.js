window.onload = function () {
    const urlSearchParams = new URLSearchParams(window.location.search);
    const params = Object.fromEntries(urlSearchParams.entries());
    let bodyEle = document.getElementsByTagName("body")[0];
    
    if (params["showAll"] === "true") {
        bodyEle.className += " show-all";
    }

    window.iframeCounter = 0;
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
    
    populateTable(undefined, undefined, undefined, filterField, sortedAsc, undefined);
}

function filterData(dataSorted, filterType, filterField, filterValue, filterSubField) {
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
            } else if (filterType === 'ArrayValueMapped') {
                if (filterValue !== 'All' && value.map(x => x[filterSubField]).indexOf(filterValue) === -1) {
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

function populateTable(filterField, filterValue, filterType, sortByField, sortAsc, filterSubField) {
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
    
    dataSorted = filterData(dataSorted, filterType, filterField, filterValue, filterSubField);
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
            let licenceNumber = linkedLicence.licenceNumber;
            let backLink = linkedLicence.fromSection.length === 1 && linkedLicence.fromSection[0].indexOf("ImplicitBackLink") > -1;
            let abstractionLimits = linkedLicence.fromSection.length >= 1 && linkedLicence.fromSection.indexOf("AbstractionLimits") > -1;
            
            let styledLicenceNumber = backLink && false ? ("(" +linkedLicence.licenceNumber + ")") : linkedLicence.licenceNumber;
            let text = backLink ? 'Implicit back link' : linkedLicence.fromSection[0];
            let color = backLink ? "#888" : "rebeccapurple";
            
            if (abstractionLimits) {
                color = "lightseagreen";
            }
            
            if (licenceInList(licenceNumber)) {
                let linkedFilename = getFilename(licenceNumber);
                linkedLicencesSb.push('<li title="' + text + '"><a style="color: ' + color + '" href="report.html?filename=' + linkedFilename
                    + '" onclick="openIframe(\'' + linkedFilename + '\'); return false;">' + styledLicenceNumber + '</a></li>');
            } else {
                if (color === 'rebeccapurple') color = 'default';
                
                linkedLicencesSb.push('<li style="color: ' + color + '" title="' + text + '">' + styledLicenceNumber + '</li>');                
            }
        }

        if (item.linkedLicences.length > 0) {
            linkedLicencesSb.push('</ul>');
        }

        let licenceSetsSb = [];

        if (item.licenceSets.length > 0) {
            licenceSetsSb.push('<ul>');

            for (let j = 0; j < item.licenceSets.length; j++) {
                let licenceSet = item.licenceSets[j];
                let licenceSetId = licenceSet.licenceSetId;
                let shortLicenceSetId = licenceSet.shortLicenceSetId;

                let backLink = licenceSet.licenceSetType === "allLicencesImplicitlyReferencedInLimits";
                let abstractionLimits = licenceSet.licenceSetType === "allLicencesExplicitlyReferencedInLimits";
                let mixed = licenceSet.licenceSetType === "allLicencesIncludingImplicitlyReferenced";
                
                let color = backLink ? "#AAA" : "rebeccapurple";

                if (abstractionLimits) {
                    color = "lightseagreen";
                }
                
                if (mixed) {
                    color = "orange";
                }
                
                let html1 = "<span class='lsId' title='" + licenceSetId + " " + licenceSet.licenceSetType + "'><a style='color: " + color + "' href='licencesetreport.html?filename="
                    + item.filename + "&licenceSetId=" + licenceSetId + "' onclick=\"openIframeSet('" + item.filename
                    + "', '" + licenceSetId + "'); return false;\">" + shortLicenceSetId + "</a></span>";
                
                licenceSetsSb.push('<li>' + html1 + '</li>');
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
            "<td>" + (item.licenceSets.length > 0 ? licenceSetsSb.join('') : "--") + "</td>" +
            "</tr>";

        htmlSb.push(html);
    }

    tbody1.innerHTML = htmlSb.join('');
    
    populateLicenceSetTable(dataSorted);
}

function populateLicenceSetTable(dataSorted) {
    let licenceSets = [];
    
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        let ary = item.licenceSets;

        for (let j = 1; j < ary.length; j++) {
            let value = ary[j];

            if (licenceSets.find(uv => uv.licenceSetId === value.licenceSetId) === undefined) {
                licenceSets.push(value);
            }
        }
    }

    setHierarchy(licenceSets);
    
    var topLevel = getChildrenOf(licenceSets, null);
    var level2 = getChildrenOf(licenceSets, topLevel[0].shortLicenceSetId);
    
    let htmlSb = [];
    
    for (let i = 0; i < licenceSets.length; i++) {
        let color = i % 2 === 0 ? "#F6F6F6" : "#FAFAFA";
        let backgroundCss = "background-color: " + color;
        let licenceSet = licenceSets[i];
        let licenceSetId = licenceSet.licenceSetId;
        
        let licencesInSet = getLicencesInSet(dataSorted, licenceSetId);
        let licenceInSet = licencesInSet[0];
        
        let linkHtml = licenceInSet.filename !== '--' ? "<a href='report.html?filename=" + licenceInSet.filename + "' onclick=\"openIframe('" + licenceInSet.filename + "'); return false;\" class='filenameSet'>" + licenceInSet.filename + "</a>" : "--";
        
        let imgsSb = [];

        for (let j = 0; j < licencesInSet.length; j++) {
            let item = licencesInSet[j];
            
            if (item.imagePath === undefined) {
                imgsSb.push("<div style='display: inline-block; width: 57px; text-align: center; font-size: 80px; line-height: 60px; vertical-align: top; color: #EEE'>--</div>");
            } else {
                imgsSb.push("<img src='" + item.imagePath + "' style='height: 80px' alt='No image found' onerror='this.style.display='none' />");
            }
        }
        
        let imgs = imgsSb.join('');
        let licenceSetTypes = licenceSet.licenceSetTypes.join('</li><li>');
        
        let html =
            "<tr style='" + backgroundCss + "'>" +
                "<td rowspan='" + licencesInSet.length + "'>" + imgs + "</td>" +
                "<td rowspan='" + licencesInSet.length + "'><span class='lsId' title='" + licenceSetId + "'><a href='licencesetreport.html?filename="
                    + licencesInSet[0].filename + "&licenceSetId=" + licenceSetId + "' onclick=\"openIframeSet('"
                    + licencesInSet[0].filename + "', '" + licenceSetId + "'); return false;\">" + licenceSet.shortLicenceSetId + "</a></span></td>" +
                "<td rowspan='" + licencesInSet.length + "'><ul><li>" + licenceSetTypes + "</li></ul></td>" +
                "<td>" + licenceInSet.licenceNumber + "</td>" +
                "<td>" + linkHtml + "</td>" +
            "</tr>";

        htmlSb.push(html);
        
        for (let j = 1; j < licencesInSet.length; j++) {
            let licenceInSet = licencesInSet[j];
            linkHtml = licenceInSet.filename !== '--' ? "<a href='report.html?filename=" + licenceInSet.filename + "' onclick=\"openIframe('" + licenceInSet.filename + "'); return false;\" class='filenameSet'>" + licenceInSet.filename + "</a>" : "--";
            
            html =
                "<tr style='" + backgroundCss + "'>" +
                    "<td>" + licenceInSet.licenceNumber + "</td>" +
                    "<td>" + linkHtml + "</td>" +
                "</tr>";

            htmlSb.push(html);
        }
    }

    const tbody2 = document.querySelector("#licenceSets tbody");
    tbody2.innerHTML = htmlSb.join('');

    setLicenceSetTotals();
}

function getChildrenOf(licenceSets, shortLicenceSetId) {
    return licenceSets.filter(x => {
        if (!shortLicenceSetId) {
            return x.parentLicences.length === 0;            
        }

        if (x.parentLicences.length === 0) {
            return false;
        }
        
        x.parentLicences.sort((a, b) => b.shortLicenceSetId.length - a.shortLicenceSetId.length);
        let shortestId = x.parentLicences[x.parentLicences.length - 1];
        
        return shortestId.shortLicenceSetId === shortLicenceSetId;
    });
}

function setHierarchy(licenceSets) {
    licenceSets.sort((a, b) => b.shortLicenceSetId.length - a.shortLicenceSetId.length);

    for (let i = 0; i < licenceSets.length; i++) {
        let licenceSet = licenceSets[i];

        if (licenceSet.childLicences === undefined) {
            licenceSet.childLicences = [];
        }

        if (licenceSet.parentLicences === undefined) {
            licenceSet.parentLicences = [];
        }
    }
    
    for (let i = 0; i < licenceSets.length; i++) {
        let licenceSetA = licenceSets[i];
        let shortLicenceIdsA = licenceSetA.shortLicenceSetId.split('-');
        let bContainsLicenceNotInA = false;

        for (let j = 0; j < licenceSets.length; j++) {
            if (i === j) {
                continue;
            }

            let licenceSetB = licenceSets[j];
            let shortLicenceIdsB = licenceSetB.shortLicenceSetId.split('-');

            if (shortLicenceIdsA.length <= shortLicenceIdsB.length) {
                continue;
            }
            
            for (let k = 0; k < shortLicenceIdsB.length; k++) {
                let shortLicenceIdB = shortLicenceIdsB[k]

                if (shortLicenceIdsA.indexOf(shortLicenceIdB) === -1) {
                    bContainsLicenceNotInA = true;
                    break;
                }
            }
            
            if (!bContainsLicenceNotInA) {
                // A is a parent of B
                
                if (licenceSetA.childLicences.find(cl => cl.shortLicenceSetId === licenceSetB.shortLicenceSetId) === undefined) {
                    licenceSetA.childLicences.push(licenceSetB);
                }

                if (licenceSetB.parentLicences.find(cl => cl.shortLicenceSetId === licenceSetA.shortLicenceSetId) === undefined) {
                    licenceSetB.parentLicences.push(licenceSetA);
                }
            }
        }
    }
}

function getLicencesInSet(dataSorted, licenceSetId) {
    let returnList = [];
    let licenceNumbers = [];
    
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        let ary = item.licenceSets;

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
            let value = ary[j].licenceNumber;
            
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
    let dataSubField = select.getAttribute("data-subfield");
    
    select.addEventListener("change", function (event) {
        if (window.resetting) {
            return;
        }

        resetFilters(select);
        populateTable(dataField, event.target.value, dataType, null, null, dataSubField);
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
    document.getElementById('licence-sets-total').innerHTML = getCount(window.dataFiltered, 'licenceSets', false);
}

function setLicenceSetTotals() {
    document.getElementById('ls-licence-set-total').innerHTML = document.querySelectorAll('#licenceSets .lsId').length;
    document.getElementById('ls-types-total').innerHTML = "TODO";
    document.getElementById('ls-licence-number-total').innerHTML = document.querySelectorAll("#licenceSets tbody tr").length;
    document.getElementById('ls-filename-total').innerHTML = document.getElementsByClassName('filenameSet').length;
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
        let ary = item.licenceSets;

        for (let j = 1; j < ary.length; j++) {
            let value = ary[j].shortLicenceSetId;
            
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
    let iframeOpen = document.getElementById('iframeDiv' + window.iframeCounter)?.style.display === 'block';
    if (iframeOpen) window.iframeCounter += 1;

    setupIframe(window.iframeCounter);
    
    document.getElementById('iframeDiv' + window.iframeCounter).style.display = 'block';
    document.getElementById('iframe' + window.iframeCounter).src = 'report.html?filename=' + filename + '&hideBackLink=true';
}

function openIframeSet(filename, licenceSetId) {
    let iframeOpen = document.getElementById('iframeDiv' + window.iframeCounter)?.style.display === 'block';
    if (iframeOpen) window.iframeCounter += 1;
    
    setupIframe(window.iframeCounter);

    document.getElementById('iframeDiv' + window.iframeCounter).style.display = 'block';
    document.getElementById('iframe' + window.iframeCounter).src = 'licencesetreport.html?filename=' + filename + '&licenceSetId=' + licenceSetId + '&hideBackLink=true';
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

function setupIframe(number) {
    let ele = document.getElementById('iframeDiv' + number);
    if (!!ele) {
        document.getElementById('iframe' + number).src = "about:blank";
        return;
    }
    
    let template = document.getElementsByClassName('iframeDivTemplate')[0];
    ele = template.cloneNode(true);
    
    ele.classList.remove('iframeDivTemplate');
    ele.id = 'iframeDiv' + number;

    for (let i = 0; i < ele.childNodes.length; i++) {
        let childNode = ele.childNodes[i];
        
        if (!!childNode.id) {
            childNode.id = childNode.id.replace('NUMBER', number);
        } else {
            continue;
        }

        for (let j = 0; j < childNode.childNodes.length; j++) {
            let childNode2 = childNode.childNodes[j];

            if (!!childNode2.id) {
                childNode2.id = childNode2.id.replace('NUMBER', number);
            }
        }
    }
    
    document.getElementsByTagName('body')[0].appendChild(ele);
    
    document.getElementById('closeLink' + number).onclick = function () {
        document.getElementById('iframeDiv' + number).style.display = 'none';
        return false;
    }

    document.getElementById('maximiseLink' + number).onclick = function () {
        let iframeDiv = document.getElementById('iframeDiv' + number);
        iframeDiv.style.top = '0';
        iframeDiv.style.width = '100%';
        iframeDiv.style.left = '0';
        iframeDiv.style.height = '100%';

        return false;
    }

    document.getElementById('minimiseLink' + number).onclick = function () {
        let iframeDiv = document.getElementById('iframeDiv' + number);
        iframeDiv.style.top = "40px";
        iframeDiv.style.height = "calc(100% - 60px)";
        iframeDiv.style.left = "350px";
        iframeDiv.style.width = "calc(100% - 370px)";

        return false;
    }

    dragElement(document.getElementById("iframeDiv" + number));
}

function dragElement(elmnt) {
    let pos1 = 0, pos2 = 0, pos3 = 0, pos4 = 0;

    document.getElementById(elmnt.id + "Header").onmousedown = startDrag;

    function startDrag(e) {
        e = e || window.event;
        e.preventDefault();

        // get the mouse cursor position at startup:
        pos3 = e.clientX;
        pos4 = e.clientY;
        document.onmouseup = stopDrag;

        // call a function whenever the cursor moves:
        document.onmousemove = elementDrag;
        document.getElementsByTagName('body')[0].classList.add('dragging');
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
        
        let top = elmnt.offsetTop - pos2;
        if (0 > top) top = 0;

        let left = elmnt.offsetLeft - pos1;
        if (0 > left) left = 0;
        
        elmnt.style.top = top + "px";
        elmnt.style.left = left + "px";
    }

    function stopDrag() {
        document.onmouseup = null;
        document.onmousemove = null;
        
        document.getElementsByTagName('body')[0].classList.remove('dragging');
    }
}