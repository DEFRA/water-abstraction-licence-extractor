const HEADER_SIZE = 200;
const PAGE_HEIGHT_PX = 1060;

window.onload = function () {
    let pdfPath = jssettings.pdfFolder + data.filename;

    let filenameEle = document.getElementById("filename");
    filenameEle.innerHTML = data.filename;
    filenameEle.href = pdfPath;

    var pdfImagesSb = [];

    for (var i = 1; i <= data.numberOfPages; i++) {
        pdfImagesSb.push("<img id='page" + i + "' src='PdfPig/Images/page-" + i + ".png'  alt='PDF image (text not available)' /><br />")
    }

    document.getElementById("pdf-images").innerHTML = pdfImagesSb.join('\n');
    document.title = data.filename + " validation report";

    document.getElementById("dataOutput").innerHTML = JSON.stringify(data, null, '   ');

    addJsonPathElement(data, '$.matches[?(@.labelGroupName==\'LicenceNumber\')]', "licenceNumber", "<strong>Licence number</strong>", "licenceNumberGroup");
    document.getElementById("licenceNumberTxt").value = getText(data, '$.matches[?(@.labelGroupName==\'LicenceNumber\')]');

    addJsonPathElement(data, '$.matches[?(@.labelGroupName==\'Company\')]', "grantedTo", "<strong>Licence holder</strong>", "grantedToGroup");
    document.getElementById("licenceHolderTxt").value = getText(data, '$.matches[?(@.labelGroupName==\'Company\')]');

    var sb2 = [];
    sb2.push("<dt><strong>Purpose</strong></dt><dd id='purposes'><dl>");

    let purposeMatches = jsonPath(data, '$.matches[?(@.labelGroupName==\'Purpose\')]');

    if (purposeMatches != null && purposeMatches.length > 0) {
        for (let idx = 0, len = purposeMatches.length; idx < len; idx++) {
            let purposeMatch = purposeMatches[idx];

            if (purposeMatch == null || purposeMatch.text == null || purposeMatch.text.length === 0) {
                continue;
            }

            sb2.push("<dt><a href='#' onclick='jumpToPage(this); return false;' data-page='" + purposeMatch.pageNumber + "'>"
                + purposeMatch.text[0].text + "</a></dt><dd><dl>");

            let abstractionLimitsMatches = jsonPath(data, '$.matches[?(@.labelGroupName==\'AbstractionLimits\')]');
            let meansOfAbstractionMatches = jsonPath(data, '$.matches[?(@.labelGroupName==\'MeansOfAbstraction\')]');

            let hasAbstractionLimits = abstractionLimitsMatches != null && abstractionLimitsMatches.length > 0;
            let hasMeansOfAbstraction = meansOfAbstractionMatches != null && meansOfAbstractionMatches.length > 0;

            if (hasAbstractionLimits || hasMeansOfAbstraction) {
                let sb = [];

                if (hasAbstractionLimits) {
                    let abstractionLimitsSection = abstractionLimitsMatches[0];
                    let abstractionLimitsConditionBlocks = abstractionLimitsSection.subResults;

                    sb.push(...processAbstractionLimits(abstractionLimitsConditionBlocks, 0));
                }

                if (hasMeansOfAbstraction) {
                    let secondValue = getText(meansOfAbstractionMatches[0],
                        '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerSecondValueMeans\')]');

                    let secondUnits = getText(meansOfAbstractionMatches[0],
                        '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerSecondUnitsMeans\')]');

                    if (!!secondValue) {
                        sb.push('<dt>Means of abstraction</dt><dd><dl>'
                            + '<dt><strong>Per second</strong></dt><dd>' + parseFloat(secondValue).toLocaleString()
                            + ' ' + secondUnits + '</dd></dl></dd>');
                    }
                }

                sb2.push(sb.join(''));
            }

            sb2.push("</dl></dd>");
        }
    }

    sb2.push("</dl></dd>");
    document.getElementById('properties').innerHTML += sb2.join('');
}

function processAbstractionLimits(abstractionLimitsConditionBlocks, level) {
    if (level > 3) return [];

    let isLinkedLicenceLevel = level > 0
    let sb = [];

    sb.push("<dt><strong>Authorised quantities</strong></dt><dd id='abstractionLimits'><dl>");

    for (let idx = 0, len = abstractionLimitsConditionBlocks.length; idx < len; idx++) {
        let conditionBlock = abstractionLimitsConditionBlocks[idx];

        for (let jdx = 0, jen = conditionBlock.subResults.length; jdx < jen; jdx++) {
            sb.push("<dt>");

            let conditionBlockSub = conditionBlock.subResults[jdx];
            let conditionBlockPurpose = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PointPurpose\')]');

            //TODO when no purpose found, carry on with a placeholder purpose

            let linkedLicenceNumbers = getMatches(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'LinkedLicenceNumber\')]');

            if (!!conditionBlockPurpose) {
                if (!isLinkedLicenceLevel) sb.push("<a href='#' onclick='jumpToPage(this); return false;' data-page='" + conditionBlockSub.pageNumber + "'>");
                sb.push(conditionBlockPurpose);
                if (!isLinkedLicenceLevel) sb.push("</a>");
            } else if (linkedLicenceNumbers.length > 0) {
                sb.push("In aggregation with other licences");
            } else {
                sb.push("All Year");
            }

            sb.push("</dt><dd><dl>");

            let secondValue = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerSecondValue\')]');

            let secondUnits = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerSecondUnits\')]');

            if (!!secondValue) {
                sb.push("<dt><strong>Per second</strong></dt>");
                sb.push("<dd>");
                if (!isLinkedLicenceLevel) sb.push("<a href='#' onclick='jumpToPage(this); return false;' data-page='" + conditionBlockSub.pageNumber + "'>");
                sb.push(parseFloat(secondValue).toLocaleString() + " " + secondUnits);
                if (!isLinkedLicenceLevel) sb.push("</a>");
                sb.push("</dd>");
            }

            let hourValue = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerHourValue\')]');

            let hourUnits = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerHourUnits\')]');

            if (!!hourValue) {
                sb.push("<dt><strong>Per hour</strong></dt>");
                sb.push("<dd>");
                if (!isLinkedLicenceLevel) sb.push("<a href='#' onclick='jumpToPage(this); return false;' data-page='" + conditionBlockSub.pageNumber + "'>");
                sb.push(parseFloat(hourValue).toLocaleString() + " " + hourUnits);
                if (!isLinkedLicenceLevel) sb.push("</a>");
                sb.push("</dd>");
            }

            let dayValue = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerDayValue\')]');

            let dayUnits = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerDayUnits\')]');

            if (!!dayValue) {
                sb.push("<dt><strong>Per day</strong></dt>");
                sb.push("<dd>");
                if (!isLinkedLicenceLevel) sb.push("<a href='#' onclick='jumpToPage(this); return false;' data-page='" + conditionBlockSub.pageNumber + "'>");
                sb.push(parseFloat(dayValue).toLocaleString() + " " + dayUnits);
                if (!isLinkedLicenceLevel) sb.push("</a>");
                sb.push("</dd>");
            }

            let monthValue = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerMonthValue\')]');

            let monthUnits = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerMonthUnits\')]');

            if (!!monthValue) {
                sb.push("<dt><strong>Per month</strong></dt>");
                sb.push("<dd>");
                if (!isLinkedLicenceLevel) sb.push("<a href='#' onclick='jumpToPage(this); return false;' data-page='" + conditionBlockSub.pageNumber + "'>");
                sb.push(parseFloat(monthValue).toLocaleString() + " " + monthUnits);
                if (!isLinkedLicenceLevel) sb.push("</a>");
                sb.push("</dd>");
            }

            let yearValue = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerYearValue\')]');

            let yearUnits = getText(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerYearUnits\')]');

            if (!!yearValue) {
                sb.push("<dt><strong>Per year</strong></dt>");
                sb.push("<dd>");
                if (!isLinkedLicenceLevel) sb.push("<a href='#' onclick='jumpToPage(this); return false;' data-page='" + conditionBlockSub.pageNumber + "'>");
                sb.push(parseFloat(yearValue).toLocaleString() + " " + yearUnits);
                if (!isLinkedLicenceLevel) sb.push("</a>");
                sb.push("</dd>");
            }

            let linkedLicences = getMatches(conditionBlockSub,
                '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'LinkedLicence\')]');

            for (let kdx = 0, ken = linkedLicenceNumbers.length; kdx < ken; kdx++) {
                let linkedLicenceNumber = toText(linkedLicenceNumbers[kdx]);

                if (!linkedLicenceNumber) {
                    continue;
                }

                let mapMatch = typeof (mapData) !== 'undefined'
                    && mapData.licenceNumberToFilename[linkedLicenceNumber];

                if (!mapMatch) mapMatch = "--";

                sb.push("<dt><strong>Linked licence</strong></dt><dd><dl>");
                sb.push("<dt><strong>Licence number</strong></dt><dd>"
                    + "<a href='../" + mapMatch.replace(".pdf", "").replace(".PDF", "").replaceAll(".", "-")
                    + "/report.html'>" + linkedLicenceNumber + "</a></dd>");

                let linkedLicence = linkedLicences.length > kdx ? linkedLicences[kdx] : null;

                if (!linkedLicence) {
                    sb.push("</dl></dd>");
                    continue;
                }

                let linkedAssignedTo = getText(linkedLicence,
                    '$.subResults[?(@.labelGroupName==\'Company\')]');

                sb.push("<dt><strong>Licence holder</strong></dt>");
                sb.push("<dd>");
                sb.push(linkedAssignedTo);
                sb.push("</dd>");

                let linkedLicenceAbstractionLimits = jsonPath(
                    linkedLicence,
                    '$.subResults[?(@.labelGroupName==\'AbstractionLimits\')]')[0];

                let linkedLicenceAbstractionLimitsSubResults = linkedLicenceAbstractionLimits.subResults;
                sb.push(...processAbstractionLimits(linkedLicenceAbstractionLimitsSubResults, level + 1));

                sb.push("</dl></dd>");
            }

            sb.push("</dl></dd>");
        }
    }

    sb.push("</dl></dd>");
    return sb;
}

function getMatches(dataToUse, path) {
    let jsonPathResults = jsonPath(dataToUse, path);

    if (jsonPathResults == null || jsonPathResults.length === 0) {
        return [];
    }

    return jsonPathResults;
}

function getMatch(dataToUse, path) {
    let matches = getMatches(dataToUse, path);
    return matches.length > 0 ? matches[0] : null;
}

function getText(dataToUse, path) {
    let matched = getMatch(dataToUse, path);

    return toText(matched);
}

function toText(matched) {
    if (matched == null || matched.text == null || matched.text.length === 0) {
        return null;
    }

    return matched.text[0].text;
}

function addJsonPathElement(dataToUse, path, eleId, text, groupEleId, extraHtml) {
    let jsonPathResults = jsonPath(dataToUse, path);

    if (jsonPathResults == null || jsonPathResults.length === 0) {
        return null;
    }

    let matched = jsonPathResults[0];

    if (matched == null || matched.text.length === 0) {
        return null;
    }

    if (text != null) {
        let ddEle = document.getElementById(groupEleId + "-dd");
        let ddExists = ddEle != null;

        let sb = [];

        if (!ddExists) {
            sb.push("<dt>" + text + "</dt>");
            sb.push("<dd id='" + groupEleId + "-dd'>")
        }

        sb.push("<a href='#' onclick='jumpToPage(this); return false;' data-page='0' id='" + eleId + "'>[NOT FOUND]</a> ");

        if (extraHtml != null)
        {
            sb.push(extraHtml);
        }

        if (!ddExists) {
            sb.push("</dd>")
        }

        if (!ddExists) {
            document.getElementById('properties').innerHTML += sb.join('');
        } else {
            ddEle.innerHTML += sb.join('');
        }
    }

    let ele = document.getElementById(eleId);
    ele.innerHTML = matched.text[0].text;
    ele.setAttribute("data-page", matched.pageNumber);

    return matched.text[0].text;
}

function jumpToPage(ele) {
    let pageNumber = parseInt(ele.getAttribute('data-page'));// - 1;
    jumpToPageNumber(pageNumber);
}

function jumpToPageNumber(pageNumber) {
    var imgEle = document.getElementById("page" + pageNumber);

    document.getElementById("iframeParent").scrollTo({
        top: imgEle.offsetTop + 350,
        left: 0,
        behavior: 'smooth'
    });
}

function evaluateJsonPath() {
    let path = document.getElementById("txtJsonPath").value;
    let result  = jsonPath(data, path);

    document.getElementById("jsonPathOutput").innerHTML = JSON.stringify(result)
}

function showTab(tabName) {
    if (tabName === "pdf-images") {
        document.getElementById("iframeParent").style.visibility = "visible";
        document.getElementById("jsonPath").style.visibility = "hidden";
        
        return false;
    }
    
    document.getElementById("iframeParent").style.visibility = "hidden";
    document.getElementById("jsonPath").style.visibility = "visible";
    
    return false;
}

function showView(index) {
    if (index === 2) {
        document.getElementById("view1").className = "";
        document.getElementById("view2").className = "viewSelected";

        document.getElementById("properties").style.display = "none";
        document.getElementById("propertiesNew").style.display = "block";

        jumpToPageNumber(2);
        return;
    }
    
    document.getElementById("view1").className = "viewSelected";
    document.getElementById("view2").className = "";

    document.getElementById("properties").style.display = "block";
    document.getElementById("propertiesNew").style.display = "none";

    jumpToPageNumber(0);
}

function continuePressed() {
    document.getElementById('licenceNumberTxtDiv').style.visibility = 'hidden';
    document.getElementById('licenceHolderTxtDiv').style.visibility = 'visible';
    
    document.getElementById('backButton').style.display = 'inline-block';

    return false;
}

function backPressed() {
    document.getElementById('licenceNumberTxtDiv').style.visibility = 'visible';
    document.getElementById('licenceHolderTxtDiv').style.visibility = 'hidden';

    return false;
}