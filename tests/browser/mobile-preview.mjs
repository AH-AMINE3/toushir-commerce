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
 const accountResponse=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/profile/summary'&&r.status()===200);
 await page.getByRole('tab',{name:'Mon Toushir',exact:true}).click();const summary=await(await accountResponse).json();
 await page.getByRole('button',{name:/^Ma carte Toushir/}).waitFor();if(summary.netPaidThisMonth!==0||summary.deliveredOrders!==0)throw Error('Fresh merchant summary contains unrelated payments');
 await page.reload();await page.getByRole('button',{name:/^Ma carte Toushir/}).waitFor();if(new URL(page.url()).hash!=='#account')throw Error('Merchant account link lost on session restore');await page.screenshot({path:reportDir+'/merchant-account.png'});checks.push('Shared account route survives session restoration and refresh');
 await page.getByRole('button',{name:'Mes cadeaux',exact:true}).click();await page.getByText('Actualiser mes points',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Retour à Mon Toushir',exact:true}).click();await page.getByRole('button',{name:'Mon activité',exact:true}).click();await page.getByRole('button',{name:'Voir mes commandes',exact:true}).click();
 await page.getByRole('tab',{name:'Commandes',exact:true}).waitFor();checks.push('Merchant card, points and actual account activity navigate to connected records');
 await page.getByRole('tab',{name:'Mon Toushir',exact:true}).click();await page.getByRole('button',{name:'Paramètres du compte',exact:true}).click();
 await page.getByRole('button',{name:/^Mon magasin Coordonnées/}).click();await page.getByRole('button',{name:'Ajouter une adresse',exact:true}).click();await page.getByRole('textbox',{name:'Rue et numéro',exact:true}).waitFor();await page.getByRole('button',{name:'Fermer l’adresse',exact:true}).click();
 checks.push('Account settings preserve contact and address workflows');
 await page.getByRole('tab',{name:'Messages',exact:true}).click();await page.getByRole('button',{name:'Préférences de communication',exact:true}).click();await page.getByRole('switch',{name:'Recevoir les offres commerciales',exact:true}).waitFor();checks.push('Title-row message settings expose actual communication consent');
 await page.goto(base+'/mobile/driver',{waitUntil:'domcontentloaded'});await page.getByText('Votre tournée',{exact:true}).first().waitFor({timeout:30000});checks.push('Courier browser route loads independently');
 const fresh=await browser.newPage({viewport:{width:402,height:874}});try{await fresh.goto(base+'/mobile/merchant#account',{waitUntil:'domcontentloaded'});await fresh.getByRole('button',{name:/^Ma carte Toushir/}).waitFor({timeout:60000});if(new URL(fresh.url()).hash!=='#account')throw Error('Fresh shared account link lost during bootstrap');checks.push('A first-time visitor can open the shared account link directly');}finally{await fresh.close();}
}catch(error){checks.push('FAIL: '+error.message);process.exitCode=1;await page.screenshot({path:reportDir+'/mobile-preview-failure.png'})}finally{await fs.writeFile(reportDir+'/mobile-preview.json',JSON.stringify({at:new Date().toISOString(),base,checks,errors},null,2));console.log(JSON.stringify({checks,errors}));if(errors.length)process.exitCode=1;await browser.close()}
