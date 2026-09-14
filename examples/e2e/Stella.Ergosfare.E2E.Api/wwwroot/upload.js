const form = document.querySelector('#upload');
const file = document.querySelector('#file');
const send = document.querySelector('#send');
const cancel = document.querySelector('#cancel');
const progress = document.querySelector('#progress');
const status = document.querySelector('#status');
const result = document.querySelector('#result');
let request;
form.addEventListener('submit', event => {
  event.preventDefault();
  if (!file.files.length || request) return;
  if (file.files[0].size >= 2 * 1024 ** 3) {
    status.textContent = 'Choose a file smaller than 2 GiB, including multipart headers.';
    return;
  }
  const body = new FormData(form);
  const xhr = request = new XMLHttpRequest();
  xhr.open('POST', form.action);
  send.disabled = file.disabled = true;
  cancel.disabled = false;
  result.hidden = true;
  progress.value = 0;
  status.textContent = 'Upload started…';
  xhr.upload.onprogress = event => {
    if (event.lengthComputable) progress.value = event.loaded / event.total * 100;
    status.textContent = `${(event.loaded / 1024 ** 2).toFixed(1)} MiB sent`;
  };
  xhr.upload.onload = () => { status.textContent = 'Transfer complete; waiting for the server to finish writing…'; };
  xhr.onload = () => {
    result.hidden = false;
    if (xhr.status >= 200 && xhr.status < 300) {
      const data = JSON.parse(xhr.responseText);
      status.textContent = 'File saved.';
      result.textContent = `File: ${data.fileName}\nStored as: ${data.storedName}\nDeclared MIME: ${data.contentType ?? '(not provided)'}\nSize: ${data.bytes} bytes\nChunks: ${data.chunks}\nSHA-256: ${data.sha256}\nMetadata: ${data.storedName}.json`;
    } else {
      status.textContent = `Upload failed (${xhr.status}).`;
      result.textContent = xhr.responseText;
    }
  };
  xhr.onerror = () => { status.textContent = 'Connection lost; the upload could not be completed.'; };
  xhr.onabort = () => { status.textContent = 'Cancelled. The server is cleaning up the partial file.'; };
  xhr.onloadend = () => { request = null; send.disabled = file.disabled = false; cancel.disabled = true; };
  xhr.send(body);
});
cancel.addEventListener('click', () => request?.abort());
