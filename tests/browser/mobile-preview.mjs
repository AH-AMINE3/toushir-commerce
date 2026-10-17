import {chromium} from 'playwright';
import fs from 'node:fs/promises';
const reportDir=process.env.REPORT_DIR||'reports';await fs.mkdir(reportDir,{recursive:true});
const base=process.env.WEB_URL||'http://127.0.0.1:5860';
const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_EXECUTABLE,args:['--no-sandbox','--disable-dev-shm-usage']});
const page=await browser.newPage({viewport:{width:402,height:874}});const checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
try{
 await page.goto(base+'/mobile/merchant',{waitUntil:'domcontentloaded'});
 await page.getByRole('textbox',{name:'Rechercher un produit, une marque ou un code-barres',exact:true}).waitFor({timeout:60000});
 const add=page.getByRole('button',{name:/^Ajouter .+ au panier$/}).first();await add.waitFor({timeout:30000});
 await page.screenshot({path:reportDir+'/merchant-browser-final.png'});checks.push('Packaged React Native web catalogue rendered at402x874');
 const responsePromise=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/cart'&&r.request().method()==='PUT');await add.click();const response=await responsePromise;const quote=await response.json();if(!response.ok()||!quote.items?.length)throw Error('Basket save did not persist');checks.push('Mobile basket mutation persisted through the shared API');
 const metadata=await page.evaluate(async()=>await(await fetch('/api/distribution')).json());if(!metadata.merchant.available&&await page.getByRole('link',{name:/Télécharger.*Android/}).count())throw Error('Unavailable native build exposed');
 await page.reload();await page.getByRole('textbox',{name:'Rechercher un produit, une marque ou un code-barres',exact:true}).waitFor();checks.push('Per-tab merchant identity restored after reload');
 await page.goto(base+'/mobile/driver',{waitUntil:'domcontentloaded'});await page.getByText('Votre tournée',{exact:true}).first().waitFor({timeout:30000});checks.push('Courier browser route loads independently');
}catch(error){checks.push('FAIL: '+error.message);process.exitCode=1;await page.screenshot({path:reportDir+'/mobile-preview-failure.png'})}finally{await fs.writeFile(reportDir+'/mobile-preview.json',JSON.stringify({at:new Date().toISOString(),base,checks,errors},null,2));console.log(JSON.stringify({checks,errors}));if(errors.length)process.exitCode=1;await browser.close()}
