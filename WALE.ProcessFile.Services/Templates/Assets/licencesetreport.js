function loadScript(file, callback) {
    const newScript = document.createElement('script');
    newScript.setAttribute('src', file);
    newScript.setAttribute('type', 'text/javascript');
    newScript.setAttribute('async', 'true');

    newScript.onload = () => callback();
    newScript.onerror = () => console.error(`Error loading script: ${file}`);

    document.head.appendChild(newScript);
}

function loadReport(filename, licenceSetId) {
    window.onload = function () {
        document.getElementById('licence-set-id').innerHTML = licenceSetId;

        let data = licenceSets.filter(x => x.licenceSetId === licenceSetId)[0];
        
        // create json tree object
        const tree = jsonview.create(data);

        // render tree into dom element
        jsonview.render(tree, document.querySelector('#dataSetOutput'));
        jsonview.toggleNode(tree);
        
        let licences = data.licences;
        let colsEle = document.getElementById('cols');

        colsEle.className = 'cols-' + (licences.length + 1);

        for (let i = 0; i < licences.length; i++) {
            let licence = licences[i];
            let div = document.createElement('div');
            
            if (typeof licence.filename === 'undefined') {
                div.innerHTML += "<div>--</div>";
                colsEle.appendChild(div);

                continue;
            }
            
            div.innerHTML += "<img src='" + filename + "/PdfPig/Images/page-1.jpg' alt='Licence sheet 1 for "
                + filename + "' style='width: 100%' />";
            
            colsEle.appendChild(div);
        }
    };
}