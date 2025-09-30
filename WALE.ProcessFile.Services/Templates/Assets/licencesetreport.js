function loadScript(file, callback) {
    const newScript = document.createElement('script');
    newScript.setAttribute('src', file);
    newScript.setAttribute('type', 'text/javascript');
    newScript.setAttribute('async', 'true');

    newScript.onload = () => callback();
    newScript.onerror = () => console.error(`Error loading script: ${file}`);

    document.head.appendChild(newScript);
}

function loadReport(licenceSetId) {
    window.onload = function () {
        document.getElementById('licence-set-id').innerHTML = licenceSetId;

        var data = licenceSets.filter(x => x.licenceSetId === licenceSetId)[0];
        
        // create json tree object
        const tree = jsonview.create(data);

        // render tree into dom element
        jsonview.render(tree, document.querySelector('#dataSetOutput'));
        jsonview.toggleNode(tree);
    };
}