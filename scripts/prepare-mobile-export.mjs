import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
export async function prepareMobileExport(directory){
 let html=await fs.readFile(path.join(directory,'index.html'),'utf8');
 html=html.replace('<html lang="en">','<html lang="fr-DZ">');
 if(!html.includes('toushir-mobile-manifest')){
  html=html.replace('</head>','<link id="toushir-mobile-manifest" rel="manifest" href="/mobile/merchant.webmanifest"><meta name="theme-color" content="#ffffff"><script>if(location.pathname.includes("/mobile/driver"))document.getElementById("toushir-mobile-manifest").href="/mobile/driver.webmanifest";</script></head>');
 }
 for(const [name,label,icon] of [['merchant','Commerce','icon.png'],['driver','Livreur','driver-icon.png']]){
  await fs.copyFile(path.join(root,'apps/mobile/assets',icon),path.join(directory,name+'-icon.png'));
  const manifest={id:'/mobile/'+name,name:'TOUSHIR '+label,short_name:'TOUSHIR '+label,lang:'fr-DZ',start_url:'/mobile/'+name,scope:'/mobile/',display:'standalone',background_color:'#ffffff',theme_color:'#ffffff',icons:[{src:'/mobile/'+name+'-icon.png',sizes:'1024x1024',type:'image/png'}]};
  await fs.writeFile(path.join(directory,name+'.webmanifest'),JSON.stringify(manifest,null,2));
 }
 await fs.writeFile(path.join(directory,'index.html'),html);
 // A coherent export must not publish an index pointing at absent bundles.
 for(const match of html.matchAll(/(?:src|href)="(\/mobile\/[^"?#]+\.(?:js|css))"/g))await fs.access(path.join(directory,match[1].slice('/mobile/'.length)));
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))await prepareMobileExport(path.resolve(process.argv[2]||path.join(root,'apps/mobile/dist')));
