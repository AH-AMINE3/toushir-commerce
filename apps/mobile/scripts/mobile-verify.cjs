const fs=require('node:fs');
const path=require('node:path');
const config=require('../app.config');
const role=process.env.EXPO_PUBLIC_APP_ROLE==='DRIVER'?'DRIVER':'MERCHANT';
const expected=role==='DRIVER'?'com.toushir.livreur':'com.toushir.commerce';
if(config.android.package!==expected||config.ios.bundleIdentifier!==expected)throw Error('Native variant identifier mismatch');
if(config.experiments.baseUrl!=='/mobile')throw Error('Wrong browser base URL');
for(const p of ['assets/DMSans.ttf','assets/icon.png','assets/driver-icon.png'])if(!fs.statSync(path.resolve(__dirname,'..',p)).size)throw Error('Missing asset: '+p);
console.log(`${role} variant: ${config.name}, ${expected}, /mobile; required assets present`);
