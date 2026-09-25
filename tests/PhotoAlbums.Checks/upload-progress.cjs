const { readFileSync } = require('node:fs');
const { runInNewContext } = require('node:vm');
const assert = require('node:assert/strict');

async function check(mode) {
    let submit, reloaded = false;
    const messages = [];
    const progress = { removeAttribute(name) { this.indeterminate = name === 'value'; } };
    const status = { set textContent(value) { messages.push(value); }, append() {} };
    const failures = { items: [], replaceChildren() {}, appendChild(li) { this.items.push(li); } };
    const files = { files: [{ name: 'photo.jpg', type: 'image/jpeg', size: 100 }] };
    const form = {
        action: 'https://example.test/Foto/Carica', elements: [{ disabled: false }],
        reportValidity: () => true, querySelector: () => ({ value: 'test' }),
        addEventListener: (_, handler) => { submit = handler; }
    };
    const nodes = { 'album-upload': form, 'album-status': status, 'album-progress': progress,
        'album-files': files, 'album-failures': failures };
    class Xhr {
        upload = {};
        open(_, url) { this.responseURL = url; }
        send() {
            this.upload.onprogress({ lengthComputable: true, loaded: 42, total: 100 });
            assert.match(messages.at(-1), /42%/);
            this.upload.onload();
            assert.match(messages.at(-1), /Applicazione logo/);
            assert.equal(progress.indeterminate, true);
            if (mode === 'timeout') return this.ontimeout();
            this.status = 200;
            this.responseText = mode === 'login' ? '<html>login</html>' : '{"message":"Foto caricata con logo."}';
            this.onload();
        }
    }
    runInNewContext(readFileSync('FullMetalPaintballCarmagnola/wwwroot/js/photo-album.js', 'utf8'), {
        document: { getElementById: id => nodes[id], querySelectorAll: () => [], createElement: () => ({ addEventListener() {} }) },
        window: { addEventListener() {}, location: { reload() { reloaded = true; } } },
        location: { href: form.action }, URL, XMLHttpRequest: Xhr,
        FormData: class { append() {} }
    });
    await submit({ preventDefault() {} });
    assert.equal(reloaded, mode === 'success');
    assert.equal(form.elements[0].disabled, false);
    assert.equal(failures.items.length, mode === 'success' ? 0 : 1);
}
(async () => {
    for (const mode of ['success', 'timeout', 'login']) await check(mode);
    console.log('PASS: real transfer percentage, processing phase, success, timeout and login recovery.');
})().catch(error => { console.error(error); process.exitCode = 1; });
