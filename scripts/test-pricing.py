#!/usr/bin/env python3
"""Isolated customer tariffs, price consent, campaigns and frozen order terms."""
import concurrent.futures, datetime as dt, json, os, sys, urllib.error, urllib.request, uuid

BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5861').rstrip('/')+'/api'
checks=[];requests=0;success=False
def key():return uuid.uuid4().hex
def now(days=0):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(days=days)).isoformat()
def call(path,token=None,body=None,expected=200,method=None,headers=None):
    global requests
    requests+=1
    req=urllib.request.Request(BASE+path,method=method or ('POST' if body is not None else 'GET'),
        data=json.dumps(body).encode() if body is not None else None,
        headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {}),**(headers or {})})
    try:
        with urllib.request.urlopen(req,timeout=45) as response:status=response.status;data=json.load(response)
    except urllib.error.HTTPError as error:
        status=error.code
        try:data=json.load(error)
        except Exception:data={}
    assert status==expected,(path,status,expected,data if status!=200 else 'unexpected success')
    return data
def ok(name,condition=True):
    assert condition,name
    checks.append(name);print('PASS '+name,flush=True)

try:
    admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token'];owner=admin['workspaceToken']
    def persona(role):return call('/demo/session',body={'role':role,'workspaceToken':owner})
    finance=persona('FINANCE');F=finance['token'];merchant=persona('MERCHANT');M=merchant['token'];mid=merchant['user']['id']
    W=persona('WAREHOUSE')['token'];D=persona('DRIVER')['token'];outsider=call('/demo/session',body={'role':'ADMIN'});X=outsider['token']
    invitation=call('/admin/demo/invitations',A,{'label':'EXEMPLE QA tarif second magasin','hours':24,'maxUses':1})
    second=call('/demo/join',body={'inviteToken':invitation['inviteToken'],'joinKey':key(),'name':'EXEMPLE QA','shopName':'EXEMPLE autre magasin','commune':'Oran','street':'EXEMPLE'})
    M2=second['token'];mid2=second['user']['id']
    pid=call('/products?pageSize=1',A)['items'][0]['id']
    def offer(price,expected=200):return call('/admin/products/'+pid,A,{'price':price,'stock':100,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH',expected=expected)
    offer(100)
    def simulate(quantity=1,token=M,merchant_id=None,at=None):
        return call('/pricing/simulate',token,{'merchantId':merchant_id,'items':[{'productId':pid,'quantity':quantity}],'at':at})['items'][0]['price']
    def rule(scope='STANDARD',target='',price=12000,minimum=1,maximum=None,**extra):
        body={'productId':pid,'scope':scope,'targetId':target,'unitPriceMinor':price,'minimumQuantity':minimum,'maximumQuantity':maximum,
            'startsAt':(dt.datetime.now(dt.timezone(dt.timedelta(hours=1)))-dt.timedelta(hours=1)).isoformat(),
            'endsAt':now(2),'allowCampaigns':False,'reference':'EXEMPLE-ACCORD-'+key()[:6],'note':'Fictif','idempotencyKey':key(),**extra}
        return call('/admin/pricing/rules',A,body),body
    def publish(value):return call('/admin/pricing/rules/'+value['id']+'/review',F,{'version':value['version'],'decision':'PUBLISH','reason':'EXEMPLE · conditions contrôlées'})

    call('/admin/pricing',expected=401)
    for token in [M,W,D]:call('/admin/pricing',token,expected=403)
    call('/pricing/simulate',D,{'items':[{'productId':pid,'quantity':1}]},expected=403)
    ok('anonymous and non-financial roles cannot manage or inspect the tariff matrix')
    call('/pricing/simulate',M,{'merchantId':mid2,'items':[{'productId':pid,'quantity':1}]},expected=404)
    call('/pricing/simulate',X,{'merchantId':mid,'items':[{'productId':pid,'quantity':1}]},expected=404)
    ok('merchant simulation cannot request another shop and other workspaces cannot target this merchant')
    ok('approved offer remains the known standard fallback',simulate()['source']=='OFFER' and simulate()['unitPriceMinor']==10000)
    offer(0,expected=400);ok('a published offer cannot be changed to a zero-price sale',simulate()['unitPriceMinor']==10000)
    segment=call('/admin/pricing/segments',A,{'name':'Épiceries QA'})
    call('/admin/pricing/segments',A,{'name':'épiceries qa'},expected=409)
    call('/admin/pricing/segments',M,{'name':'Interdit'},expected=403)
    ok('segment names are unique without case differences and cannot be self-created by merchants')
    assigned=call('/admin/pricing/assignments/'+mid,A,{'segmentId':segment['id'],'version':0,'reason':'EXEMPLE · accord de groupe'})
    call('/admin/pricing/assignments/'+mid,A,{'segmentId':None,'version':0,'reason':'EXEMPLE ancien formulaire'},expected=409)
    call('/admin/pricing/assignments/'+mid,M,{'segmentId':segment['id'],'version':assigned['version'],'reason':'Interdit'},expected=403)
    foreign_segment=call('/admin/pricing/segments',X,{'name':'Autre dépôt'})
    call('/admin/pricing/assignments/'+mid,A,{'segmentId':foreign_segment['id'],'version':assigned['version'],'reason':'Interdit'},expected=404)
    ok('shop segment changes require an authorized account, current version and same-workspace target')
    standard,body=rule()
    replay=call('/admin/pricing/rules',A,body)
    call('/admin/pricing/rules',A,{**body,'unitPriceMinor':1},expected=409)
    ok('tariff proposal retries preserve one definition and reject altered payloads',replay['id']==standard['id'])
    ok('unpublished rules never change a price',simulate()['unitPriceMinor']==10000)
    call('/admin/pricing/rules/'+standard['id']+'/review',A,{'version':1,'decision':'PUBLISH','reason':'EXEMPLE auto-approbation'},expected=409)
    call('/admin/pricing/rules/'+standard['id']+'/review',W,{'version':1,'decision':'PUBLISH','reason':'Interdit'},expected=403)
    call('/admin/pricing/rules/'+standard['id']+'/review',X,{'version':1,'decision':'PUBLISH','reason':'Interdit'},expected=404)
    ok('publication requires another authorized person and cannot cross the workspace boundary')
    standard=publish(standard)
    ok('independent publication activates the standard schedule with UTC-normalized dates',simulate()['source']=='STANDARD' and simulate()['unitPriceMinor']==12000 and dt.datetime.fromisoformat(standard['startsAt'].replace('Z','+00:00')).utcoffset()==dt.timedelta(0))
    group,_=rule('SEGMENT',segment['id'],11000,12);group=publish(group)
    contract,_=rule('MERCHANT',mid,15000,1,11);contract=publish(contract)
    ok('contract priority wins even when the segment price is cheaper',simulate(11)['source']=='MERCHANT' and simulate(11)['unitPriceMinor']==15000)
    ok('quantities outside a contract band fall through to the eligible segment',simulate(12)['source']=='SEGMENT' and simulate(12)['unitPriceMinor']==11000)
    ok('another shop in the same workspace cannot inherit a private contract or unassigned segment',simulate(11,M2)['source']=='STANDARD' and simulate(12,M2)['unitPriceMinor']==12000)
    ok('campaign stacking remains forbidden unless the chosen rule explicitly allows it',simulate()['allowCampaigns'] is False)
    overlapping,_=rule('MERCHANT',mid,14000,1,11)
    call('/admin/pricing/rules/'+overlapping['id']+'/review',F,{'version':1,'decision':'PUBLISH','reason':'EXEMPLE conflit attendu'},expected=409)
    rejected=call('/admin/pricing/rules/'+overlapping['id']+'/review',F,{'version':1,'decision':'REJECT','reason':'EXEMPLE · période en conflit'})
    ok('same-scope overlapping periods and quantity bands cannot be published',rejected['status']=='REJECTED' and simulate()['unitPriceMinor']==15000)
    for invalid in [None,[None],[],[{'productId':pid,'quantity':0}]]:
        call('/pricing/simulate',M,{'items':invalid},expected=400)
    for change in [{'unitPriceMinor':0},{'minimumQuantity':0},{'minimumQuantity':12,'maximumQuantity':11},{'scope':'STANDARD','targetId':mid},{'endsAt':now(-2)}]:
        call('/admin/pricing/rules',A,{**body,**change,'idempotencyKey':key()},expected=400)
    ok('null lines, zero prices, invalid quantity bands, wrong scope targets and invalid dates fail before publication')
    boundary=now(1)
    replacement,_=rule('MERCHANT',mid,15500,1,11,startsAt=boundary,endsAt=now(3),supersedesId=contract['id'],supersedesVersion=contract['version'])
    replacement=publish(replacement)
    ok('future replacement preserves the current contract until the exact effective instant',simulate()['unitPriceMinor']==15000 and simulate(at=boundary)['unitPriceMinor']==15500)
    overview=call('/admin/pricing',A)
    old=next(x for x in overview['rules'] if x['id']==contract['id'])
    call('/admin/pricing/rules/'+old['id']+'/retire',A,{'version':old['version'],'reason':'EXEMPLE auto-retrait'},expected=409)
    retired=call('/admin/pricing/rules/'+old['id']+'/retire',F,{'version':old['version'],'reason':'EXEMPLE · retrait immédiat avant remplacement'})
    ok('an independent immediate withdrawal can shorten an already scheduled cutoff without rewriting future replacement terms',retired['state']=='RETIRED' and simulate()['unitPriceMinor']==12000 and simulate(at=boundary)['unitPriceMinor']==15500)
    race,_=rule('STANDARD','',9000,100,200)
    # This band overlaps the standard schedule; retire that schedule first.
    call('/admin/pricing/rules/'+standard['id']+'/retire',F,{'version':standard['version'],'reason':'EXEMPLE · nouveau test concurrent'})
    payload=json.dumps({'version':race['version'],'decision':'PUBLISH','reason':'EXEMPLE · examen simultané'}).encode()
    def publish_race(_):
        req=urllib.request.Request(BASE+'/admin/pricing/rules/'+race['id']+'/review',method='POST',data=payload,headers={'Content-Type':'application/json','Authorization':'Bearer '+F})
        try:
            with urllib.request.urlopen(req,timeout=45) as response:response.read();return response.status
        except urllib.error.HTTPError as error:error.read();return error.code
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:statuses=list(pool.map(publish_race,range(2)))
    requests+=2
    ok('concurrent tariff publication commits one decision exactly once',sorted(statuses)==[200,409])
    overview=call('/admin/pricing',A)
    ok('definition, assignment, publication, scheduled replacement and retirement history retain actual actors',all(x['actorId'] in [admin['user']['id'],finance['user']['id']] for x in overview['events']) and any(x['kind']=='TARIFF_REPLACEMENT_SCHEDULED' for x in overview['events']))

    # Fresh product, same real accounts: checkout tests require Wholesale:DeliveryFee=0.
    products=call('/products?pageSize=4',A)['items'];pid=products[1]['id'];gift_id=products[2]['id']
    offer(100)
    address=call('/addresses',M)[0]
    def cart(quantity=3,token=M):return call('/cart/quote',token,{'items':[{'productId':pid,'quantity':quantity}]})
    def request(quantity=3):return call('/orders',M,{'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':pid,'quantity':quantity}],'mode':'REQUEST_QUOTE'})
    def quote(o,quantity=3,price=100,**extra):return call('/admin/orders/'+o['id']+'/quote',A,{'version':o['version'],'items':[{'productId':pid,'quantity':quantity,'unitPrice':price,'caseSize':1,'availableStock':None}],'deliveryFee':0,'validUntil':now(1),'campaignIds':[],**extra})
    def checkout(preview,quantity=3,**extra):return {'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':pid,'quantity':quantity}],'mode':'PLACE_ORDER','pricingToken':preview.get('pricingToken'),**extra}
    c=cart();assert c['canOrder'] and c['deliveryFee']==0,'Set Wholesale__DeliveryFee=0 for this suite'
    before_orders=call('/orders',M)['total'];before_stock=call('/products/'+pid,A)
    for token in [None,'invalid',cart(token=M2).get('pricingToken')]:
        error=call('/orders',M,checkout(c,pricingToken=token),expected=409)
        assert error['error']['code']=='PRICE_CONFIRMATION_REQUIRED',error
    call('/orders',M,checkout(c,quantity=4),expected=409)
    after_stock=call('/products/'+pid,A)
    ok('missing, altered, other-actor and changed-quantity consent cannot create orders or reserve stock',call('/orders',M)['total']==before_orders and after_stock['reserved']==before_stock['reserved'])
    call('/cart',M,{'items':[{'productId':pid,'quantity':3}]},method='PUT',headers={'X-Toushir-Actor':mid})
    error=call('/cart',M,{'items':[]},method='PUT',headers={'X-Toushir-Actor':mid2},expected=403)
    ok('queued cart operations bound to a different signed-in actor fail without replacing the basket',error['error']['code']=='ACTOR_CHANGED' and call('/cart',M)['items'][0]['quantity']==3)

    contract,_=rule('MERCHANT',mid,13000);contract=publish(contract)
    detail=call('/products/'+pid,M)
    call('/favorites/'+pid,M,{})
    favorite=next(x for x in call('/favorites',M) if x['id']==pid)
    ok('catalogue, product detail, favorites, saved cart and simulation resolve the same private contract',detail['price']==130 and favorite['price']==130 and call('/cart',M)['items'][0]['unitPrice']==130 and simulate(3)['unitPriceMinor']==13000 and call('/products/'+pid,M2)['price']==100)
    stale=call('/orders',M,checkout(c),expected=409)
    ok('a published price change invalidates the previously displayed basket before any commitment',stale['error']['code']=='PRICE_CONFIRMATION_REQUIRED' and call('/orders',M)['total']==before_orders)
    simulation=call('/pricing/simulate',A,{'merchantId':mid,'items':[{'productId':pid,'quantity':3}]})
    ok('staff simulation shares the actual checkout engine but never issues order consent',simulation['quote'].get('pricingToken') is None and simulation['items'][0]['unitPrice']==130 and simulation['quote']['canOrder'] is False)
    c=cart();body=checkout(c);direct=call('/orders',M,body)
    snap=direct['pricing']['items'][0]
    ok('an explicitly reviewed direct order commits exactly the displayed price with an immutable snapshot',direct['total']==c['total']==390 and snap['baseUnitPriceMinor']==13000 and snap['appliedUnitPriceMinor']==13000 and direct['reservationHeld'])
    call('/orders/'+direct['id']+'/pricing-history',M2,expected=404)
    call('/orders/'+direct['id']+'/pricing-history',X,expected=404)
    call('/orders/'+direct['id']+'/pricing-history',W,expected=403)
    public_history=call('/orders/'+direct['id']+'/pricing-history',M)
    private_history=call('/orders/'+direct['id']+'/pricing-history',A)
    ok('price history is scoped to the actual order and private rule terms stay restricted',not any(x in public_history['items'][0] for x in ['termsJson','actorId','overrideReason','ruleId']) and private_history['items'][0]['actorId']==mid and private_history['items'][0]['ruleId']==contract['id'])

    rfq=request();quoted=quote(rfq,useTariffPrices=True)
    ok('a wholesaler can explicitly apply the current tariff instead of submitted manual prices',quoted['items'][0]['unitPrice']==130 and quoted['pricing']['items'][0]['source']=='MERCHANT')
    changed,_=rule('MERCHANT',mid,14000,supersedesId=contract['id'],supersedesVersion=contract['version']);changed=publish(changed)
    accepted=call('/orders/'+quoted['id']+'/accept-quote',M,{'version':quoted['quoteVersion']})
    replay=call('/orders',M,{**body,'pricingToken':'expired-or-unavailable-proof'})
    ok('later tariff changes cannot reprice an existing quote, accepted order or idempotent direct-order retry',accepted['total']==390 and accepted['items'][0]['unitPrice']==130 and replay['id']==direct['id'] and replay['total']==390 and call('/orders/'+quoted['id']+'/pricing-history',A)['items'][0]['ruleId']==contract['id'])
    rfq=request()
    error=call('/admin/orders/'+rfq['id']+'/quote',A,{'version':rfq['version'],'items':[{'productId':pid,'quantity':3,'unitPrice':90,'caseSize':1}],'deliveryFee':0,'validUntil':now(1)},expected=409)
    assert error['error']['code']=='PRICE_OVERRIDE_REASON_REQUIRED',error
    negotiated=quote(rfq,price=90,pricingOverrideReason='EXEMPLE · accord commercial ponctuel')
    first_history=call('/orders/'+rfq['id']+'/pricing-history',A)['items'][0]
    ok('negotiated deviations require an explicit reason and preserve expected versus proposed prices',negotiated['total']==270 and first_history['source']=='MANUAL_QUOTE' and first_history['expectedUnitPriceMinor']==14000 and first_history['baseUnitPriceMinor']==9000 and first_history['overrideReason'].startswith('EXEMPLE'))
    revised=quote(negotiated,useTariffPrices=True)
    history=call('/orders/'+rfq['id']+'/pricing-history',A)
    ok('quote revisions append immutable provenance rather than replacing earlier commercial terms',history['total']==2 and any(x['quoteVersion']==1 and x['baseUnitPriceMinor']==9000 for x in history['items']) and revised['total']==420)

    def campaign(kind='DISCOUNT',**extra):
        result=call('/admin/campaigns',A,{'title':'EXEMPLE QA tarif et campagne','description':'Fictif','kind':kind,'productId':pid,'minimumQuantity':1,'discountPercent':5 if kind=='DISCOUNT' else 0,'giftProductId':gift_id if kind=='FREE_GOODS' else None,'giftQuantity':1,'goalQuantity':0,'unitFactor':1,'rewardId':None,'awardLimit':1,'startsAt':now(-1),'endsAt':now(1),'claimUntil':None,**extra})
        return call('/admin/campaigns/'+result['id']+'/publish',F,{'version':result['version']})
    discount=campaign(minimumQuantity=3)
    c=cart();eligibility=call('/campaigns/'+discount['id']+'/preview',M,{'quantity':3})
    ok('non-cumulative customer contracts suppress both basket and campaign discount previews',c['items'][0]['unitPrice']==140 and c['items'][0].get('promotion') is None and eligibility['discount']==0 and eligibility['tariffCompatible'] is False)
    rfq=request()
    error=call('/admin/orders/'+rfq['id']+'/quote',A,{'version':rfq['version'],'items':[{'productId':pid,'quantity':3,'unitPrice':140,'caseSize':1}],'deliveryFee':0,'validUntil':now(1),'useTariffPrices':True,'campaignIds':[discount['id']]},expected=409)
    ok('an explicitly selected incompatible campaign is rejected without quote or snapshot side effects',error['error']['code']=='TARIFF_CAMPAIGN_COMBINATION' and call('/orders/'+rfq['id'],M)['status']=='REQUESTED' and call('/orders/'+rfq['id']+'/pricing-history',A)['total']==0)
    cumulable,_=rule('MERCHANT',mid,12345,allowCampaigns=True,supersedesId=changed['id'],supersedesVersion=changed['version']);cumulable=publish(cumulable)
    c=cart();eligibility=call('/campaigns/'+discount['id']+'/preview',M,{'quantity':3})
    promoted=call('/orders',M,checkout(c))
    ok('allowed campaigns use identical per-unit centime rounding in preview, basket and committed order',eligibility['goodsAfter']==c['total']==promoted['total']==351.84 and promoted['items'][0]['unitPrice']==117.28 and promoted['pricing']['items'][0]['baseUnitPriceMinor']==12345 and promoted['pricing']['items'][0]['appliedUnitPriceMinor']==11728)

    gift=campaign('FREE_GOODS')
    unknown_gift=cart(2)
    ok('unknown gift stock blocks direct checkout while leaving an explicit RFQ possible',unknown_gift['needsQuote'] and unknown_gift.get('pricingToken') is None and 'CADEAU_A_CONFIRMER' in unknown_gift['items'][0]['issues'] and request(2)['status']=='REQUESTED')
    call('/admin/products/'+gift_id,A,{'stock':10,'caseSize':1,'minimumQuantity':1,'isActive':True},method='PATCH')
    c=cart(2);gift_order=call('/orders',M,checkout(c,quantity=2))
    actual=gift_order['promotions']['items'][0]
    ok('known free goods are displayed by product name and reserved through the existing custody chain',c['items'][0]['promotion']['giftProductName'] and actual['giftProductId']==gift_id and actual['giftStockState']=='RESERVED' and actual['giftQuantity']==1 and gift_order['total']==246.9)
    tiny,_=rule('MERCHANT',mid,1,allowCampaigns=True,supersedesId=cumulable['id'],supersedesVersion=cumulable['version']);publish(tiny)
    campaign(discountPercent=99)
    c=cart()
    ok('a rounded zero promotion never becomes a zero-price sale and does not prevent a quote request',c['needsQuote'] and c['items'][0]['unitPrice'] is None and c.get('pricingToken') is None and 'PROMOTION_PRICE_INVALID' in c['items'][0]['issues'] and request()['status']=='REQUESTED')
    pid=products[3]['id'];gift_id=pid
    call('/admin/products/'+pid,A,{'price':100,'stock':3,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
    campaign('FREE_GOODS')
    c=cart()
    ok('gift availability includes units already requested for sale from the same physical product',c['needsQuote'] and 'CADEAU_A_CONFIRMER' in c['items'][0]['issues'] and c.get('pricingToken') is None)
    success=True
finally:
    result={'at':now(),'base':BASE[:-4],'scope':'Customer pricing rules, protected price consent, actual checkout, immutable quote history and campaign integration. Browser acceptance remains separate.','passed':len(checks),'failed':0 if success else 1,'requests':requests,'checks':checks}
    with open(os.environ.get('PRICING_REPORT','pricing-verification.json'),'w') as file:json.dump(result,file,indent=2,ensure_ascii=False)
    print(json.dumps({'passed':len(checks),'requests':requests}),flush=True)
