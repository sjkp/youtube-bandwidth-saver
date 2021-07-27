
function getIdFromUrl(url) {
    let r = /watch\?v=([\w\-]*)/;
    var m = url.match(r);
    return m[1];
}

function reddenPage() {
    chrome.storage.sync.get({
        port: '6886'
    }, function (items) {
        host = `http://localhost:${items.port}`;


        let r = /watch\?v=([\w\-]*)/;
        var m = document.location.href.match(r);
        let id = m[1];
        console.log('replace video ' + id);

        var vid = document.getElementsByTagName('video')[0];
        vid.addEventListener('seeked', (e) => {
            e.stopImmediatePropagation();
        });
        vid.addEventListener('seeking', (e) => {
            e.stopImmediatePropagation();
        });

        setTimeout(() => {
            var currentPos = vid.currentTime;
            vid.setAttribute('preload', 'none');
            vid.setAttribute('src', `${host}/api/StreamLocalVideo/${id}`);
            var promise = vid.play();
            vid.currentTime = currentPos;
            // vid.muted = false;
        }, 1000);

    });

}

let host = '';
let playfromlocal = true;

chrome.storage.sync.get({
    port: '6886',
    playfromlocal: true
}, function (items) {
    host = `http://localhost:${items.port}`;
    playfromlocal = items.playfromlocal;
});



chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
    if (changeInfo.status == 'loading' && changeInfo.url && changeInfo.url.indexOf('youtube.com') > -1) {
        console.log(tabId + ":" + changeInfo.status + ":" + changeInfo.url);

        fetch(`${host}/api/ExistsLocalFile/${getIdFromUrl(changeInfo.url)}`).then(response => {

            if (response.status == 200) {
                chrome.action.setIcon({
                    path: 'logo-green.png',
                    tabId: tabId
                });
                if (playfromlocal) {
                    chrome.scripting.executeScript({
                        target: { tabId: tabId },
                        function: reddenPage
                    });
                }
            }
            else {
                chrome.action.setIcon({
                    path: 'logo.png',
                    tabId: tabId
                });
            }


        }).catch(e => {
            chrome.action.setIcon({
                path: 'logo.png',
                tabId: tabId
            });
            chome.action.setBadgeText({
                tabId: tabId,
                text: '0%'
            });
        });

    }
});

chrome.action.onClicked.addListener((tab) => {
    let r = /watch\?v=([\w\-]*)/;

    console.log(tab.url);

    var m = tab.url.match(r);

    if (m.length == 2) {
        console.log(m[1]);
        let id = m[1];
        chrome.action.setIcon({
            path: 'logo-yellow.png',
            tabId: tab.id
        });
        fetch(`${host}/api/DownloadYoutubeVideo/${id}`).then(response => response.json()).then(data => {

            chrome.action.setIcon({
                path: 'logo-green.png',
                tabId: tab.id
            });
            if (playfromlocal) {
                chrome.scripting.executeScript({
                    target: { tabId: tab.id },
                    function: reddenPage
                });
            }
            chrome.notifications.create('', {
                title: 'Video downloaded',
                message: 'Ready to save bandwidth',
                iconUrl: '/logo-green.png',
                type: 'basic'
            });
            console.log(data);
        })
    }
});


chrome.runtime.onInstalled.addListener(function (object) {
    chrome.tabs.create({url: `chrome-extension://${chrome.runtime.id}/options.html`}, function (tab) {
        console.log("options page opened");
    });
});