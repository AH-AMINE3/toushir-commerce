import {prepareMobileExport} from './prepare-mobile-export.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const target=path.join(root,'backend/wwwroot');
await fs.rm(target,{recursive:true,force:true});
await fs.mkdir(target,{recursive:true});
await fs.cp(path.join(root,'apps/web/dist'),target,{recursive:true});
const mobile=path.join(target,'mobile');await fs.mkdir(mobile,{recursive:true});
await fs.cp(path.join(root,'apps/mobile/dist'),mobile,{recursive:true});
await prepareMobileExport(mobile);
console.log('Desktop and mobile browser applications installed under one API origin.');

await fs.cp(path.join(root,"downloads"),path.join(target,"downloads"),{recursive:true});
