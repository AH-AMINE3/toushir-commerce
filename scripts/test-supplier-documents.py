#!/usr/bin/env python3
"""Exercises private supplier evidence on an explicitly enabled, isolated demo API."""
import base64, concurrent.futures, datetime as dt, hashlib, json, os, sys, urllib.error, urllib.request, uuid
from pathlib import Path

BASE = (sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:5861').rstrip('/') + '/api'
checks = []
requests = 0
def key(): return uuid.uuid4().hex
def now(days=0): return (dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=days)).isoformat()
def call(path, token=None, body=None, expected=200, raw=False):
    global requests
    requests += 1
    req = urllib.request.Request(BASE+path, method='POST' if body is not None else 'GET',
        data=json.dumps(body).encode() if body is not None else None,
        headers={'Content-Type':'application/json', **({'Authorization':'Bearer '+token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=45) as response:
            status=response.status
            data=response.read() if raw else json.load(response)
            headers=dict(response.headers)
    except urllib.error.HTTPError as error:
        status=error.code
        try: data=json.load(error)
        except Exception: data={}
        headers=dict(error.headers)
    assert status == expected, (path, status, expected, data if not raw else 'binary response')
    return (data,headers) if raw else data
def ok(name, condition=True):
    assert condition,name
    checks.append(name)
    print('PASS '+name,flush=True)

PDF=(Path(__file__).resolve().parent.parent/'tests/fixtures/supplier-example.pdf').read_bytes()
success=False
try:
    admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token'];owner=admin['workspaceToken']
    def persona(role): return call('/demo/session',body={'role':role,'workspaceToken':owner})['token']
    F=persona('FINANCE');W=persona('WAREHOUSE');M=persona('MERCHANT');D=persona('DRIVER')
    X=call('/demo/session',body={'role':'ADMIN'})['token']
    product=call('/products?pageSize=1',A)['items'][0];pid=product['id']
    supplier=call('/admin/suppliers',A,{'name':'EXEMPLE QA pièces fournisseur','contact':'Équipe de test','phone':'Exemple','note':'Fictif'})
    def purchase():
        return call('/admin/purchases',A,{'supplierId':supplier['id'],'expectedAt':now(2),
            'items':[{'productId':pid,'quantity':10,'unitCostMinor':10000}],
            'note':'EXEMPLE QA · aucune commande réelle','idempotencyKey':key()})
    po=purchase();oid=po['order']['id'];path='/admin/purchases/'+oid
    def dossier(token=A): return call(path+'/dossier',token)
    def upload(kind,reference,token=A,**extra):
        body={'orderVersion':dossier()['orderVersion'],'kind':kind,'reference':reference,'documentDate':dt.datetime.now(dt.timezone(dt.timedelta(hours=1))).isoformat(),
            'fileName':'exemple-qa.pdf','contentBase64':base64.b64encode(PDF).decode(),'idempotencyKey':key(),**extra}
        return call(path+'/documents',token,body),body
    call(path+'/dossier',expected=401)
    call(path+'/dossier',M,expected=403);call(path+'/dossier',D,expected=403);call(path+'/dossier',X,expected=404)
    ok('anonymous, merchant, driver and other workspace cannot read supplier evidence')
    first,first_body=upload('PURCHASE_ORDER','PO-EXEMPLE')
    doc=first['document']
    assert doc['sha256']==hashlib.sha256(PDF).hexdigest()
    replay=call(path+'/documents',A,first_body)
    ok('PDF upload is idempotent and records its actual SHA256',replay['document']['id']==doc['id'] and replay['orderVersion']==first['orderVersion'])
    ok('supplier document dates are normalized to UTC',dt.datetime.fromisoformat(doc['documentDate'].replace('Z','+00:00')).utcoffset()==dt.timedelta(0))
    call(path+'/documents',A,{**first_body,'reference':'OTHER'},expected=409)
    ok('reused document key cannot replace content or reference')
    data,headers=call(path+'/documents/'+doc['id']+'/content',W,raw=True)
    ok('authorized download returns exact PDF bytes as a private attachment', data==PDF and headers.get('Cache-Control')=='no-store' and 'attachment' in headers.get('Content-Disposition',''))
    call(path+'/documents/'+doc['id']+'/content',X,expected=404)
    call(path+'/documents/'+doc['id']+'/content',M,expected=403)
    ok('PDF content has the same workspace and role boundaries as its dossier')
    v=dossier()['orderVersion']
    bad={**first_body,'orderVersion':v,'idempotencyKey':key()}
    call(path+'/documents',A,{**bad,'fileName':'../exemple.pdf'},expected=400)
    call(path+'/documents',A,{**bad,'contentBase64':base64.b64encode(b'<html>not a pdf</html>').decode()},expected=400)
    call(path+'/documents',A,{**bad,'contentBase64':'invalid?base64'},expected=400)
    ok('path-like names, non-PDF bytes and malformed encodings are rejected',dossier()['orderVersion']==v)
    call(path+'/documents',A,{**bad,'contentBase64':base64.b64encode(b'%PDF-'+b'0'*524288).decode()},expected=400)
    ok('demo file-size limit is enforced before storing content')
    call(path+'/documents',A,{**bad,'kind':'RECEIPT'},expected=400)
    ok('a receipt document requires an actual linked receipt')
    replacement,replacement_body=upload('PURCHASE_ORDER','PO-EXEMPLE-R2',supersedesId=doc['id'],supersededVersion=doc['version'])
    ds=dossier();old=next(x for x in ds['documents'] if x['id']==doc['id'])
    ok('revisions preserve the previous content and explicit supersession',old['status']=='SUPERSEDED' and replacement['document']['supersedesId']==doc['id'])
    call(path+'/documents',A,{**replacement_body,'idempotencyKey':key(),'orderVersion':ds['orderVersion']},expected=409)
    ok('a stale predecessor cannot be superseded again')
    po=call(path+'/approve',F,{'version':ds['orderVersion']})
    confirmation_doc,_=upload('SUPPLIER_CONFIRMATION','CF-EXEMPLE',W)
    proposal={'orderVersion':dossier()['orderVersion'],'documentId':confirmation_doc['document']['id'],
        'supplierReference':'CF-EXEMPLE','expectedAt':now(3),'items':[{'productId':pid,'quantity':8,'unitCostMinor':12000}],'idempotencyKey':key()}
    for invalid_items in (None, [None], [], [{'productId':pid,'quantity':0,'unitCostMinor':12000}]):
        call(path+'/confirmations',W,{**proposal,'items':invalid_items,'idempotencyKey':key()},expected=400)
    ok('null, empty and entirely unavailable supplier confirmations are rejected without changing the purchase',dossier()['orderVersion']==proposal['orderVersion'])
    proposal['expectedAt']=(dt.datetime.now(dt.timezone(dt.timedelta(hours=1)))+dt.timedelta(days=3)).isoformat()
    recorded=call(path+'/confirmations',W,proposal);confirmation=recorded['confirmations'][0]
    ok('supplier expected dates with an explicit offset are normalized to UTC',dt.datetime.fromisoformat(confirmation['expectedAt'].replace('Z','+00:00')).utcoffset()==dt.timedelta(0))
    same=call(path+'/confirmations',W,proposal)
    ok('recording supplier terms is idempotent and does not amend the purchase before review',len(same['confirmations'])==1 and call(path,A)['items'][0]['orderedQuantity']==10)
    review={'orderVersion':same['orderVersion'],'version':confirmation['version'],'decision':'ACCEPT','reason':'EXEMPLE QA · termes contrôlés'}
    call(path+'/confirmations/'+confirmation['id']+'/review',W,review,expected=403)
    # A separately recorded proposal exercises the same-account independent approval rule.
    own=call(path+'/confirmations',A,{**proposal,'orderVersion':dossier()['orderVersion'],'idempotencyKey':key()})
    own_confirmation=next(x for x in own['confirmations'] if x['recordedBy']==admin['user']['id'])
    call(path+'/confirmations/'+own_confirmation['id']+'/review',A,{**review,'orderVersion':own['orderVersion'],'version':own_confirmation['version']},expected=409)
    ok('warehouse cannot approve and the proposal recorder cannot approve their own terms')
    reviewed=call(path+'/confirmations/'+confirmation['id']+'/review',F,{**review,'orderVersion':dossier()['orderVersion']})
    actual=call(path,A)
    ok('independent acceptance applies confirmed quantities, costs and expected date once',actual['items'][0]['orderedQuantity']==8 and actual['items'][0]['unitCostMinor']==12000)
    call(path+'/confirmations/'+confirmation['id']+'/review',F,{**review,'orderVersion':reviewed['orderVersion'],'version':2},expected=409)
    ok('an accepted confirmation cannot be applied twice')
    call(path+'/documents/'+confirmation_doc['document']['id']+'/void',F,{'version':1,'reason':'QA'},expected=409)
    ok('accepted supporting evidence cannot be voided in place')
    # Another pending proposal is stale after changed accepted terms.
    stale=dossier();own_confirmation=next(x for x in stale['confirmations'] if x['id']==own_confirmation['id'])
    call(path+'/confirmations/'+own_confirmation['id']+'/review',F,{**review,'orderVersion':stale['orderVersion'],'version':own_confirmation['version']},expected=409)
    ok('approval of an older commercial proposal cannot overwrite changed terms')
    po=call(path,A);received=call(path+'/receive',W,{'version':po['order']['version'],'idempotencyKey':key(),
        'lines':[{'productId':pid,'receivedQuantity':5,'quarantineQuantity':0,'lotNumber':'EXEMPLE-LOT-DOC','expiresAt':now(100)}],'note':'EXEMPLE réception physique'})
    call(path+'/confirmations',A,{**proposal,'orderVersion':dossier()['orderVersion'],'items':[{'productId':pid,'quantity':9,'unitCostMinor':12000}],'idempotencyKey':key()},expected=409)
    ok('supplier terms cannot rewrite an order after physical receiving begins')
    invoice,_=upload('INVOICE','FACT-EXEMPLE',F)
    call(path+'/documents/'+invoice['document']['id']+'/content',W,expected=403)
    warehouse_view=dossier(W)
    ok('warehouse users cannot list or download invoice and credit-note files',all(x['kind']!='INVOICE' for x in warehouse_view['documents']) and not any(x['referenceId']==invoice['document']['id'] for x in warehouse_view['events']))
    call(path+'/invoice-review',F,{'version':dossier()['orderVersion'],'invoiceReference':'FACT-EXEMPLE','invoiceMinor':60000})
    ok('matching entered invoice amount alone never certifies the document chain',not dossier()['evidence']['completeDocumentChain'])
    receipt=received['receipts'][0]
    upload('RECEIPT','REC-EXEMPLE',W,receiptId=receipt['id'])
    ok('complete evidence chain requires accepted supplier terms, actual receipt, its PDF and matching invoice reference',dossier()['evidence']['completeDocumentChain'])
    closing={'version':dossier()['orderVersion'],'reason':'EXEMPLE QA · solde non livré annulé'}
    call(path+'/close-remainder',A,closing,expected=409)
    closed=call(path+'/close-remainder',F,closing)
    final=call(path,A)
    ok('independent short close records three cancelled units without inventing receipt',closed['orderStatus']=='SHORT_CLOSED' and final['items'][0]['receivedQuantity']==5 and final['items'][0]['cancelledQuantity']==3 and final['items'][0]['outstandingQuantity']==0)
    call(path+'/receive',W,{'version':closed['orderVersion'],'idempotencyKey':key(),'lines':[{'productId':pid,'receivedQuantity':1,'quarantineQuantity':0,'lotNumber':'LATE','expiresAt':now(100)}]},expected=409)
    ok('closed remainder cannot be received later without a new purchase')
    po2=purchase();p2='/admin/purchases/'+po2['order']['id']
    cancelled=call(p2+'/cancel',A,{'version':po2['order']['version'],'reason':'EXEMPLE brouillon abandonné'})
    ok('draft cancellation remains explicit and preserves its event history',cancelled['orderStatus']=='CANCELLED' and any(x['kind']=='SUPPLY_CANCELLED' for x in cancelled['events']))
    # A replacement PDF never silently inherits an earlier approval.
    revised_doc,_=upload('SUPPLIER_CONFIRMATION','CF-EXEMPLE-R2',A,supersedesId=confirmation_doc['document']['id'],supersededVersion=1)
    ok('replaced supplier evidence must be reviewed again',not dossier()['evidence']['acceptedSupplierConfirmation'])
    before=call(path,A)
    late=call(path+'/confirmations',A,{'orderVersion':dossier()['orderVersion'],'documentId':revised_doc['document']['id'],
        'supplierReference':'CF-EXEMPLE-R2','expectedAt':before['order']['expectedAt'],'items':[{'productId':pid,'quantity':8,'unitCostMinor':12000}],'idempotencyKey':key()})
    late_confirmation=next(x for x in late['confirmations'] if x['documentId']==revised_doc['document']['id'])
    ok('late evidence is explicitly distinguished from a pre-receipt agreement',late_confirmation['evidenceOnly'])
    call(path+'/confirmations/'+late_confirmation['id']+'/review',F,{'orderVersion':late['orderVersion'],'version':late_confirmation['version'],'decision':'ACCEPT','reason':'EXEMPLE piece corrigee, termes inchanges'})
    after=call(path,A)
    ok('late evidence approval preserves all received quantities and historical costs',after['receivedMinor']==before['receivedMinor'] and after['items']==before['items'] and dossier()['evidence']['completeDocumentChain'])
    original_path=path
    # Two simultaneous approvals have one winner and one conflict.
    race_po=purchase();path='/admin/purchases/'+race_po['order']['id']
    call(path+'/approve',F,{'version':1})
    race_doc,_=upload('SUPPLIER_CONFIRMATION','CF-RACE',A)
    race_data=call(path+'/confirmations',A,{'orderVersion':dossier()['orderVersion'],'documentId':race_doc['document']['id'],'supplierReference':'CF-RACE','expectedAt':now(3),'items':[{'productId':pid,'quantity':7,'unitCostMinor':11000}],'idempotencyKey':key()})
    race_confirmation=race_data['confirmations'][0]
    race_body=json.dumps({'orderVersion':race_data['orderVersion'],'version':race_confirmation['version'],'decision':'ACCEPT','reason':'EXEMPLE examen concurrent'}).encode()
    def review_race(_):
        req=urllib.request.Request(BASE+path+'/confirmations/'+race_confirmation['id']+'/review',method='POST',data=race_body,headers={'Content-Type':'application/json','Authorization':'Bearer '+F})
        try:
            with urllib.request.urlopen(req,timeout=45) as r:r.read();return r.status
        except urllib.error.HTTPError as e:e.read();return e.code
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:statuses=list(pool.map(review_race,range(2)))
    requests+=2
    ok('concurrent supplier approvals commit exactly once',sorted(statuses)==[200,409] and len([x for x in dossier()['events'] if x['kind']=='SUPPLIER_CONFIRMATION_ACCEPTED'])==1)
    # Invalidating an unreviewed attachment blocks approval without changing terms.
    void_po=purchase();path='/admin/purchases/'+void_po['order']['id'];call(path+'/approve',F,{'version':1})
    void_doc,_=upload('SUPPLIER_CONFIRMATION','CF-VOID',A)
    pending=call(path+'/confirmations',A,{'orderVersion':dossier()['orderVersion'],'documentId':void_doc['document']['id'],'supplierReference':'CF-VOID','expectedAt':now(3),'items':[{'productId':pid,'quantity':6,'unitCostMinor':9000}],'idempotencyKey':key()})
    pending_confirmation=pending['confirmations'][0]
    call(path+'/documents/'+void_doc['document']['id']+'/void',F,{'version':1,'reason':'EXEMPLE mauvaise piece'})
    call(path+'/confirmations/'+pending_confirmation['id']+'/review',F,{'orderVersion':dossier()['orderVersion'],'version':pending_confirmation['version'],'decision':'ACCEPT','reason':'EXEMPLE refus attendu'},expected=409)
    ok('voided evidence cannot approve new commercial terms',call(path,A)['items'][0]['orderedQuantity']==10)
    path=original_path
    # Stored history counts toward quota, including superseded and voided originals.
    large_pdf=b'%PDF-'+b'0'*(524288-5)
    for index in range(3):upload('PURCHASE_ORDER','QUOTA-'+str(index),A,contentBase64=base64.b64encode(large_pdf).decode())
    before_quota=dossier()['orderVersion']
    call(path+'/documents',A,{'orderVersion':before_quota,'kind':'PURCHASE_ORDER','reference':'QUOTA-OVER','documentDate':now(),'fileName':'exemple-quota.pdf','contentBase64':base64.b64encode(large_pdf).decode(),'idempotencyKey':key()},expected=409)
    ok('workspace storage quota includes retained history and rejects writes atomically',dossier()['orderVersion']==before_quota)
    success=True
finally:
    report={'at':now(),'base':BASE[:-4],'passed':len(checks),'failed':0 if success else 1,'requests':requests,'checks':checks}
    out=os.environ.get('SUPPLIER_DOCUMENT_REPORT','supplier-documents-verification.json')
    with open(out,'w') as f: json.dump(report,f,indent=2,ensure_ascii=False)
    print(json.dumps({'passed':len(checks),'requests':requests}),flush=True)
