// Saves options to chrome.storage
function save_options() {
    var port = document.getElementById('port').value;
    var playfromlocal = document.getElementById('playfromlocal').checked;
    var localdir = document.getElementById("location").value;
    chrome.storage.sync.set({
        port: port,
        host: `http://localhost:${port}`,    
        playfromlocal: playfromlocal,
        localdir: localdir
    }, function () {
        // Update status to let user know options were saved.
        var status = document.getElementById('status');
        status.textContent = 'Options saved.';
        setTimeout(function () {
            status.textContent = '';
        }, 3000);
    });
}

function set_command(folder, port) {
    var command = document.getElementById("command");

    let cmd = `docker run -p ${port}:80 -v /var/run/docker.sock:/var/run/docker.sock -v ${folder}:/data -e HOST_VIDEODIR=${folder} -e LOCAL_VIDEODIR=/data --restart unless-stopped sjkp/ytbandwidthsaver`
    command.innerText = cmd;
}

// Restores select box and checkbox state using the preferences
// stored in chrome.storage.
function restore_options() {  
    chrome.storage.sync.get({
        port: '6886',
        playfromlocal: true,
        localdir: 'c:/videos'
    }, function (items) {
        document.getElementById('port').value = items.port;        
        document.getElementById('playfromlocal').checked = items.playfromlocal;    
        document.getElementById('location').value = items.localdir;
        set_command(items.localdir, items.port);
    });
}





function update_command(e){
    var folder = document.getElementById("location").value;
    var port = document.getElementById('port').value;
    set_command(folder, port);
}


document.addEventListener('DOMContentLoaded', restore_options);
document.getElementById('save').addEventListener('click', save_options);


document.getElementById("location").addEventListener('input', update_command);
document.getElementById("port").addEventListener('input', update_command);