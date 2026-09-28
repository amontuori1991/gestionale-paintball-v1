const { readFileSync } = require('node:fs');
const { runInNewContext } = require('node:vm');
const assert = require('node:assert/strict');
let click, shares = 0, fetches = 0;
const button = { textContent: 'Condividi / Salva in Foto', hidden: true,
    dataset: { url: '/Foto/File/test/photo', expiry: new Date(Date.now() + 60000).toISOString() },
    addEventListener: (_, handler) => { click = handler; } };
const feedback = {};
runInNewContext(readFileSync('FullMetalPaintballCarmagnola/wwwroot/js/photo-album.js', 'utf8'), {
    document: { documentElement: { lang: 'it' }, getElementById: id => id === 'album-feedback' ? feedback : null,
        querySelectorAll: selector => selector === '.album-share' ? [button] : [] },
    window: { addEventListener() {} }, navigator: { canShare: () => true, share: async data => { assert.equal(data.files.length, 1); shares++; } },
    File: class { constructor(_, name, options) { this.name = name; this.type = options.type; } },
    fetch: async () => { fetches++; return { ok: true, headers: { get: () => 'image/jpeg' }, blob: async () => new Uint8Array([255,216]) }; }
});
(async () => {
    assert.equal(button.hidden, false);
    await click();
    assert.equal(shares, 0);
    assert.equal(button.textContent, 'Condividi foto');
    await click();
    assert.equal(shares, 1);
    assert.equal(fetches, 1);
    button.dataset.expiry = new Date(0).toISOString();
    await click();
    assert.equal(shares, 1);
    assert.match(feedback.textContent, /scaduta/);
    console.log('PASS: two-tap sharing, file payload, expiry guard.');
})().catch(error => { console.error(error); process.exitCode = 1; });
