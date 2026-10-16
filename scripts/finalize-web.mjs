import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dist = path.join(root, 'apps/mobile/dist');
let html = await fs.readFile(path.join(dist, 'index.html'), 'utf8');
const additions = `<meta name="description" content="Toushir Gros : catalogue, demandes de devis et suivi des commandes pour les commerçants et grossistes d’Oran.">
<link rel="manifest" href="/manifest.webmanifest"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-status-bar-style" content="default"><link rel="apple-touch-icon" href="/app-icon.png">
<style>html{background:#f6f7f7;color-scheme:light}*{box-sizing:border-box}button,input,textarea{font:inherit}body{font-variant-numeric:tabular-nums}input,textarea{font-size:16px!important}input,textarea{caret-color:#087a41}::selection{background:#dcefe4;color:#171b19}:focus-visible{outline:2px solid #087a41!important;outline-offset:3px}a{color:#087a41;text-underline-offset:3px}@media(prefers-reduced-motion:reduce){*,*::before,*::after{scroll-behavior:auto!important;animation-duration:.01ms!important;transition-duration:.01ms!important}}@media(pointer:fine){html{overscroll-behavior:none}}::-webkit-scrollbar{width:7px;height:7px}::-webkit-scrollbar-thumb{background:#bac2be;border-radius:8px}::-webkit-scrollbar-track{background:transparent}</style>`;
html = html.replace('</head>', additions + '\n</head>');
html = html.replace('</body>', '<script>if("serviceWorker" in navigator){window.addEventListener("load",function(){navigator.serviceWorker.register("/service-worker.js").catch(function(){});});}</script></body>');
await fs.writeFile(path.join(dist, 'index.html'), html);
await fs.copyFile(path.join(root, 'apps/mobile/assets/app-icon.png'), path.join(dist, 'app-icon.png'));
await fs.writeFile(path.join(dist, 'manifest.webmanifest'), JSON.stringify({ name: 'Toushir Gros', short_name: 'Toushir', description: 'Votre réassort, du devis à la livraison.', lang: 'fr-DZ', start_url: '/', scope: '/', display: 'standalone', background_color: '#f6f7f7', theme_color: '#171b19', icons: [{ src: '/app-icon.png', sizes: '1024x1024', type: 'image/png', purpose: 'any' }] }, null, 2));
await fs.writeFile(path.join(dist, 'service-worker.js'), `const CACHE='toushir-shell-v2';self.addEventListener('install',()=>self.skipWaiting());self.addEventListener('activate',event=>event.waitUntil((async()=>{for(const key of await caches.keys())if(key!==CACHE)await caches.delete(key);await self.clients.claim()})()));self.addEventListener('fetch',event=>{const req=event.request,url=new URL(req.url);if(req.method!=='GET'||url.origin!==location.origin||url.pathname.startsWith('/api/'))return;if(url.pathname.startsWith('/assets/')||url.pathname.startsWith('/_expo/')||url.pathname.startsWith('/catalog/images/'))event.respondWith((async()=>{const cache=await caches.open(CACHE),saved=await cache.match(req);if(saved)return saved;const fresh=await fetch(req);if(fresh.ok)await cache.put(req,fresh.clone());return fresh})());});`);
if (process.argv.includes('--install')) {
  const target = path.join(root, 'backend/wwwroot');
  await fs.mkdir(target, { recursive: true });
  await fs.cp(dist, target, { recursive: true });
}
console.log('Web app finalized with manifest, accessible focus styles and public-asset caching.');
