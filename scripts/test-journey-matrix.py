#!/usr/bin/env python3
"""Connected role-play acceptance matrix, with at least 50 business cases per side.

This suite creates its own fictional demo workspace. It never resets existing data.
It verifies HTTP business behavior, not native runtime or visual screen acceptance.
Every named scenario records its own actual requests and required assertions.
"""
import concurrent.futures
import contextlib
import datetime as dt
import json
import os
import pathlib
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = (sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:5864').rstrip('/') + '/api'
REPORT = pathlib.Path(os.environ.get('TOUSHIR_TEST_RESULTS', str(pathlib.Path(__file__).resolve().parents[1] / 'verification/journey/api-matrix.json')))
checks = []
requests = []
active = None
started = dt.datetime.now(dt.timezone.utc).isoformat()

def key(): return uuid.uuid4().hex
def at(seconds=0): return (dt.datetime.now(dt.timezone.utc) + dt.timedelta(seconds=seconds)).isoformat()
def query(value): return urllib.parse.quote(str(value), safe='')
def require(condition, message='Required business outcome did not hold'):
    if not condition: raise AssertionError(message)

def call(path, token=None, body=None, expected=200, method=None):
    method = method or ('POST' if body is not None else 'GET')
    for attempt in range(3):
        request = urllib.request.Request(BASE + path, method=method,
            data=json.dumps(body).encode() if body is not None else None,
            headers={'Content-Type': 'application/json', **({'Authorization': 'Bearer ' + token} if token else {})})
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                status = response.status
                data = json.loads(response.read())
        except urllib.error.HTTPError as error:
            status = error.code
            try: data = json.loads(error.read())
            except (ValueError, UnicodeDecodeError): data = {}
            if status == 429 and attempt < 2:
                delay = min(60, max(1, int(error.headers.get('Retry-After', '10'))))
                requests.append({'method': method, 'path': path, 'status': status, 'retryAfterSeconds': delay})
                time.sleep(delay)
                continue
        record = {'method': method, 'path': path, 'status': status}
        if isinstance(data, dict) and data.get('error'): record['errorCode'] = data['error'].get('code')
        requests.append(record)
        expected_values = expected if isinstance(expected, tuple) else (expected,)
        require(status in expected_values, f'{method} {path}: expected {expected_values}, got {status}, code {record.get("errorCode", "none")}')
        return data
    raise AssertionError('Rate limit did not clear after the published retry delay')

@contextlib.contextmanager
def scenario(role, name):
    global active
    case = {'id': ('W' if role == 'WHOLESALER' else 'M') + str(1 + sum(x['role'] == role for x in checks)).zfill(2),
            'role': role, 'name': name, 'pass': False}
    offset = len(requests)
    active = case
    try:
        yield
        case['pass'] = True
        print('PASS ' + case['id'] + ' ' + name, flush=True)
    except Exception as error:
        case['error'] = str(error)
        print('FAIL ' + case['id'] + ' ' + name + ': ' + str(error), flush=True)
        raise
    finally:
        case['requests'] = requests[offset:]
        checks.append(case)
        active = None

def offer(pid, price=100, stock=500, **extra):
    return call('/admin/products/' + pid, A, {'price': price, 'stock': stock, 'caseSize': 1,
        'minimumQuantity': 1, 'offerApproved': True, 'isActive': True, **extra}, method='PATCH')
def preview(pid, quantity, token=None):
    return call('/cart/quote', token or M, {'items': [{'productId': pid, 'quantity': quantity}]})
def order_payload(pid, quantity, consent=None):
    return {'idempotencyKey': key(), 'addressId': address['id'], 'items': [{'productId': pid, 'quantity': quantity}],
            'mode': 'PLACE_ORDER', 'pricingToken': consent}
def tracking(): return call('/orders/' + oid + '/tracking', M)
def campaign(kind='DISCOUNT', **extra):
    return call('/admin/campaigns', A, {'title': 'EXEMPLE QA ' + kind, 'description': 'Conditions fictives pour le parcours connecté',
        'kind': kind, 'productId': bpid, 'minimumQuantity': 5, 'discountPercent': 10 if kind == 'DISCOUNT' else 0,
        'giftProductId': gpid if kind == 'FREE_GOODS' else None, 'giftQuantity': 1, 'goalQuantity': 0,
        'unitFactor': 1, 'rewardId': None, 'awardLimit': 1, 'startsAt': at(-5), 'endsAt': at(3600), 'claimUntil': None, **extra})
def publish(c): return call('/admin/campaigns/' + c['id'] + '/publish', F, {'version': c['version']})

success = False
try:
    # Ownership capabilities are confined to this new test workspace. No passwords are needed.
    admin = call('/demo/session', body={'role': 'ADMIN'}); A = admin['token']
    def persona(role): return call('/demo/session', body={'role': role, 'workspaceToken': admin['workspaceToken']})
    merchant = persona('MERCHANT'); M = merchant['token']; mid = merchant['user']['id']
    warehouse = persona('WAREHOUSE'); W = warehouse['token']
    dispatcher = persona('DISPATCHER'); P = dispatcher['token']
    finance = persona('FINANCE'); F = finance['token']
    driver = persona('DRIVER'); D = driver['token']
    outsider = call('/demo/session', body={'role': 'DRIVER'}); X = outsider['token']
    invitation = call('/admin/demo/invitations', A, {'label': 'EXEMPLE QA deuxième magasin', 'hours': 24, 'maxUses': 1})
    second = call('/demo/join', body={'inviteToken': invitation['inviteToken'], 'joinKey': key(), 'name': 'EXEMPLE second propriétaire',
        'shopName': 'EXEMPLE boutique région Est', 'phone': '0555123456', 'commune': 'Bir El Djir', 'street': 'EXEMPLE rue QA'})
    M2 = second['token']; mid2 = second['user']['id']
    products = call('/products?pageSize=5', M)['items']
    pid, bpid, gpid = [p['id'] for p in products[:3]]
    address = call('/addresses', M)[0]

    with scenario('MERCHANT', 'Browse every nonempty catalogue category'):
        categories = call('/categories', M); require(len(categories) > 5 and all(x['name'] and x['count'] > 0 for x in categories))
    with scenario('MERCHANT', 'Browse available brands'):
        brands = call('/brands', M); require(len(brands) > 10 and all(x['name'] for x in brands))
    with scenario('WHOLESALER', 'Publish a confirmed positive offer with physical stock'):
        offered = offer(pid); require(offered['price'] == 100 and offered['physicalStock'] == 500 and offered['offerApproved'])
        offer(bpid, 200); offer(gpid, 50)
    with scenario('WHOLESALER', 'Reject a zero price on a published sellable offer'):
        call('/admin/products/' + pid, A, {'price': 0, 'stock': 500, 'caseSize': 1, 'minimumQuantity': 1, 'offerApproved': True}, expected=400, method='PATCH')
        require(call('/products/' + pid, M)['price'] == 100)
    with scenario('WHOLESALER', 'Reject inconsistent case size and minimum order quantity'):
        call('/admin/products/' + pid, A, {'price': 100, 'stock': 500, 'caseSize': 6, 'minimumQuantity': 5, 'offerApproved': True}, expected=400, method='PATCH')
    with scenario('MERCHANT', 'Read the same published price in product details'):
        require(call('/products/' + pid, M)['price'] == 100)
    with scenario('MERCHANT', 'Search a product by its actual barcode'):
        rows = call('/products?q=' + query(products[0]['barcode']), M)['items']; require(any(x['id'] == pid and x['price'] == 100 for x in rows))
    with scenario('MERCHANT', 'Filter products by a real category'):
        rows = call('/products?category=' + query(products[0]['category']), M)['items']; require(rows and all(x['category'] == products[0]['category'] for x in rows))
    with scenario('MERCHANT', 'Filter products by a real brand'):
        brand = next(p['brand'] for p in products if p['brand']); rows = call('/products?brand=' + query(brand), M)['items']; require(rows and all(x['brand'].casefold() == brand.casefold() for x in rows))
    with scenario('MERCHANT', 'Save a favorite with its effective price'):
        call('/favorites/' + pid, M, {}); require(any(x['id'] == pid and x['price'] == 100 for x in call('/favorites', M)))
    with scenario('MERCHANT', 'Remove a favorite without touching the offer'):
        call('/favorites/' + pid, M, method='DELETE'); require(not any(x['id'] == pid for x in call('/favorites', M)))
    with scenario('MERCHANT', 'Persist the intended basket quantities'):
        call('/cart', M, {'items': [{'productId': pid, 'quantity': 10}]}, method='PUT'); require(call('/cart', M)['items'][0]['quantity'] == 10)
    with scenario('MERCHANT', 'Review a directly orderable exact total and price consent'):
        cart = preview(pid, 10); require(cart['canOrder'] and cart['total'] == 1000 and cart.get('pricingToken'), 'Requires a known zero delivery fee for this acceptance fixture')
    with scenario('MERCHANT', 'Reject direct checkout without consent to the displayed price'):
        bad = call('/orders', M, order_payload(pid, 10), expected=409); require(bad['error']['code'] == 'PRICE_CONFIRMATION_REQUIRED')
    with scenario('MERCHANT', 'Reject checkout when quantity differs from reviewed price consent'):
        call('/orders', M, order_payload(pid, 11, cart['pricingToken']), expected=409)

    with scenario('WHOLESALER', 'Create a stock-backed points reward catalogue item'):
        reward = call('/admin/rewards', A, {'name': 'EXEMPLE boîte cadeau QA', 'description': 'Cadeau physique de test', 'imageUrl': '', 'pointsCost': 5, 'availableStock': 10, 'isActive': True}); require(reward['availableStock'] == 10)
    with scenario('WHOLESALER', 'Publish a challenge before the qualifying purchase'):
        challenge_end = at(15)
        challenge = campaign('CHALLENGE', productId=pid, goalQuantity=8, minimumQuantity=1, rewardId=reward['id'], endsAt=challenge_end, claimUntil=at(3600))
        challenge = publish(challenge); require(challenge['status'] == 'PUBLISHED' and challenge['approvedBy'] == finance['user']['id'])
    with scenario('WHOLESALER', 'Refuse a challenge award before the claim period opens'):
        call('/admin/campaigns/' + challenge['id'] + '/award', F, {'merchantId': mid}, expected=409)
    with scenario('MERCHANT', 'Place a priced direct order without an RFQ detour'):
        payload = order_payload(pid, 10, cart['pricingToken']); order = call('/orders', M, payload); oid = order['id']
        require(order['status'] == 'SUBMITTED' and order['total'] == 1000 and order['reservationHeld'])
    with scenario('MERCHANT', 'Retry direct checkout without creating a duplicate order'):
        require(call('/orders', M, payload)['id'] == oid and call('/orders', M)['total'] == 1)
    with scenario('MERCHANT', 'Reject a changed payload reusing the successful order key'):
        call('/orders', M, {**payload, 'note': 'A changed contractual request'}, expected=409)
    with scenario('MERCHANT', 'Read the submitted order in own order history'):
        require(call('/orders?status=SUBMITTED', M)['items'][0]['id'] == oid)
    with scenario('MERCHANT', 'Read the immutable agreed price in the order details'):
        require(call('/orders/' + oid, M)['items'][0]['unitPrice'] == 100)
    with scenario('MERCHANT', 'Keep another shop from reading an order'):
        call('/orders/' + oid, M2, expected=404)
    with scenario('MERCHANT', 'Keep the merchant from publishing wholesaler offers'):
        call('/admin/products/' + pid, M, {'price': 1, 'stock': 500, 'caseSize': 1, 'minimumQuantity': 1, 'offerApproved': True}, expected=403, method='PATCH')
    with scenario('WHOLESALER', 'Receive the mobile order in the operational queue'):
        require(any(x['id'] == oid for x in call('/operations/orders', W)['items']))
    with scenario('WHOLESALER', 'Confirm a submitted priced order for warehouse preparation'):
        order = call('/admin/orders/' + oid + '/transition', A, {'status': 'CONFIRMED', 'reason': 'EXEMPLE service client confirme la commande'}); require(order['status'] == 'CONFIRMED')
    with scenario('MERCHANT', 'Follow actual confirmation from the wholesaler'):
        t = tracking(); require(t['order']['status'] == 'CONFIRMED' and t['preparation']['percentage'] == 0)
    with scenario('WHOLESALER', 'Protect stock already reserved by an accepted order'):
        call('/admin/inventory/adjust', A, {'productId': pid, 'delta': -495, 'reason': 'EXEMPLE réservation protégée'}, expected=409)
    with scenario('WHOLESALER', 'Reject dispatcher preparation outside its team responsibility'):
        call('/operations/orders/' + oid + '/preparation', P, {'version': t['version'], 'items': [{'productId': pid, 'preparedQuantity': 6}]}, expected=403, method='PATCH')
    with scenario('WHOLESALER', 'Reject stale warehouse preparation versions'):
        call('/operations/orders/' + oid + '/preparation', W, {'version': t['version'] - 1, 'items': [{'productId': pid, 'preparedQuantity': 6}]}, expected=409, method='PATCH')
    with scenario('WHOLESALER', 'Reject preparing more units than the order contains'):
        call('/operations/orders/' + oid + '/preparation', W, {'version': t['version'], 'items': [{'productId': pid, 'preparedQuantity': 11}]}, expected=400, method='PATCH')
    with scenario('WHOLESALER', 'Record a real partial preparation by the warehouse team'):
        t = call('/operations/orders/' + oid + '/preparation', W, {'version': t['version'], 'items': [{'productId': pid, 'preparedQuantity': 6}]}, method='PATCH'); require(t['preparation']['percentage'] == 60)
    with scenario('MERCHANT', 'Observe the warehouse preparation progress from the app API'):
        require(tracking()['preparation']['percentage'] == 60 and tracking()['order']['status'] == 'PREPARING')
    with scenario('WHOLESALER', 'Reject a driver assignment crossing workspace boundaries'):
        call('/operations/orders/' + oid + '/assign', P, {'driverId': outsider['user']['id'], 'version': t['version']}, expected=404)
    with scenario('WHOLESALER', 'Assign the delivery to an actual approved driver'):
        t = call('/operations/orders/' + oid + '/assign', P, {'driverId': driver['user']['id'], 'version': t['version']}); require(t['assignment']['driverId'] == driver['user']['id'])
    with scenario('WHOLESALER', 'Block pickup until every ordered unit is prepared'):
        call('/courier/orders/' + oid + '/pickup', D, {'version': t['version']}, expected=409)
    with scenario('WHOLESALER', 'Finish preparation and send the order to the ready queue'):
        t = call('/operations/orders/' + oid + '/preparation', W, {'version': t['version'], 'items': [{'productId': pid, 'preparedQuantity': 10}]}, method='PATCH'); require(t['order']['status'] == 'READY')
    with scenario('MERCHANT', 'See actual readiness and full preparation'):
        require(tracking()['order']['status'] == 'READY' and tracking()['preparation']['percentage'] == 100)
    with scenario('WHOLESALER', 'Transfer physical custody and debit stock at departure'):
        t = call('/courier/orders/' + oid + '/pickup', D, {'version': t['version']}); stock = call('/products/' + pid, A)
        require(t['order']['status'] == 'OUT_FOR_DELIVERY' and stock['physicalStock'] == 490 and stock['reserved'] == 0)
    with scenario('MERCHANT', 'Follow the dispatched order and named assigned driver'):
        current = tracking(); require(current['order']['status'] == 'OUT_FOR_DELIVERY' and current['assignment']['driverName'])
    receipt_items = [{'productId': pid, 'acceptedQuantity': 8, 'refusedQuantity': 2}]
    with scenario('MERCHANT', 'Block receipt confirmation before the driver arrives'):
        call('/orders/' + oid + '/receipt-challenge', M, {'version': t['version'], 'items': receipt_items}, expected=409)
    with scenario('MERCHANT', 'Block cancellation after physical departure'):
        call('/orders/' + oid + '/cancel', M, {'reason': 'EXEMPLE trop tard'}, expected=409)
    with scenario('WHOLESALER', 'Reject stale location fixes instead of presenting them as live GPS'):
        call('/courier/location', D, {'latitude': 35.7, 'longitude': -0.6, 'accuracy': 10, 'consent': True, 'capturedAt': at(-3600)}, expected=409)
    with scenario('WHOLESALER', 'Share a fresh driver position during the assigned delivery'):
        captured = at(); fix = {'latitude': 35.705, 'longitude': -0.621, 'accuracy': 10, 'consent': True, 'capturedAt': captured}
        require(call('/courier/location', D, fix)['sharing'])
    with scenario('MERCHANT', 'See a timestamped real submitted driver fix'):
        location = tracking()['location']; require(location['source'] == 'DEVICE_GPS' and location['isFresh'] and location['latitude'] == 35.705)
    with scenario('WHOLESALER', 'Reject repeated GPS timestamps that could overwrite newer state'):
        call('/courier/location', D, fix, expected=409)
    with scenario('WHOLESALER', 'Revoke driver location sharing explicitly'):
        require(call('/courier/location', D, {**fix, 'consent': False})['sharing'] is False)
    with scenario('MERCHANT', 'Stop showing driver coordinates after consent is withdrawn'):
        require(tracking()['location'] is None)
    with scenario('WHOLESALER', 'Move the assigned courier through en route and arrived states'):
        call('/courier/orders/' + oid + '/status', D, {'status': 'EN_ROUTE'}); t = call('/courier/orders/' + oid + '/status', D, {'status': 'ARRIVED'}); require(t['assignment']['status'] == 'ARRIVED')
    with scenario('MERCHANT', 'Reject a receipt created from an outdated order version'):
        call('/orders/' + oid + '/receipt-challenge', M, {'version': t['version'] - 1, 'items': receipt_items}, expected=409)
    with scenario('MERCHANT', 'Issue a QR receipt bound to accepted and refused quantities'):
        proof = call('/orders/' + oid + '/receipt-challenge', M, {'version': t['version'], 'items': receipt_items}); require(proof['driverId'] == driver['user']['id'] and proof['qrPayload'].startswith('toushir://receipt/'))
    with scenario('WHOLESALER', 'Reject driver alteration of merchant-confirmed receipt quantities'):
        call('/courier/orders/' + oid + '/complete', D, {'challenge': proof['challenge'], 'items': [{'productId': pid, 'acceptedQuantity': 10, 'refusedQuantity': 0}]}, expected=409)
    with scenario('WHOLESALER', 'Complete the physical receipt using the merchant QR proof'):
        t = call('/courier/orders/' + oid + '/complete', D, {'challenge': proof['challenge'], 'items': receipt_items, 'note': 'EXEMPLE réception physique 8 acceptés, 2 refusés'}); require(t['order']['status'] == 'PARTIALLY_DELIVERED' and t['order']['total'] == 800)
    with scenario('MERCHANT', 'See accepted quantities and the corrected delivered amount'):
        delivered = call('/orders/' + oid, M); require(delivered['total'] == 800 and delivered['items'][0]['acceptedQuantity'] == 8 and delivered['items'][0]['refusedQuantity'] == 2)
    with scenario('WHOLESALER', 'Reject replay of a consumed delivery receipt proof'):
        call('/courier/orders/' + oid + '/complete', D, {'challenge': proof['challenge'], 'items': receipt_items}, expected=409)
    with scenario('MERCHANT', 'Keep physical receipt separate from money actually collected'):
        require(call('/orders/' + oid, M)['balance']['collected'] == 0)
    with scenario('MERCHANT', 'Earn no spendable points from unpaid delivered goods'):
        require(call('/loyalty/sync', M, {})['available'] == 0)
    with scenario('MERCHANT', 'Prevent a merchant from fabricating a courier payment'):
        call('/courier/orders/' + oid + '/collect', M, {'amount': 800, 'tendered': 800, 'change': 0, 'method': 'CASH', 'idempotencyKey': key()}, expected=403)
    payment = {'amount': 800, 'tendered': 1000, 'change': 200, 'method': 'CASH', 'reference': 'EXEMPLE-CASH-QA', 'idempotencyKey': key()}
    with scenario('WHOLESALER', 'Reject a cash collection whose tender and change do not reconcile'):
        call('/courier/orders/' + oid + '/collect', D, {**payment, 'change': 100}, expected=409)
    with scenario('WHOLESALER', 'Record actual cash tender and change after receipt'):
        t = call('/courier/orders/' + oid + '/collect', D, payment); require(t['order']['balance']['collected'] == 800)
    with scenario('WHOLESALER', 'Retry a cash collection without taking payment twice'):
        call('/courier/orders/' + oid + '/collect', D, payment); require(len(call('/orders/' + oid, M)['payments']) == 1)
    with scenario('WHOLESALER', 'Reject collection beyond the actual remaining balance'):
        call('/courier/orders/' + oid + '/collect', D, {**payment, 'amount': 1, 'tendered': 1, 'change': 0, 'idempotencyKey': key()}, expected=409)
    with scenario('MERCHANT', 'See the actual collection method and zero remaining balance'):
        paid = call('/orders/' + oid, M); require(paid['balance']['remaining'] == 0 and paid['payments'][0]['method'] == 'CASH')
    with scenario('WHOLESALER', 'Keep warehouse staff from reconciling financial receipts'):
        call('/operations/payments/' + paid['payments'][0]['id'] + '/reconcile', W, {}, expected=403)
    with scenario('WHOLESALER', 'Reconcile the actual cash receipt through the finance role'):
        reconciled = call('/operations/payments/' + paid['payments'][0]['id'] + '/reconcile', F, {}); require(reconciled['paymentStatus'] == 'RECONCILED')
    with scenario('WHOLESALER', 'Keep refused stock quarantined until a quality decision'):
        stock = call('/products/' + pid, A); require(stock['quarantined'] == 2 and stock['physicalStock'] == 490)
    with scenario('WHOLESALER', 'Release physically inspected intact refused units'):
        call('/operations/orders/' + oid + '/returns/inspect', W, {'productId': pid, 'quantity': 2, 'disposition': 'RESTOCK', 'reason': 'EXEMPLE contrôle qualité physique'}); stock = call('/products/' + pid, A); require(stock['quarantined'] == 0 and stock['physicalStock'] == 492)
    with scenario('WHOLESALER', 'Prevent duplicate return inspection from increasing stock twice'):
        call('/operations/orders/' + oid + '/returns/inspect', W, {'productId': pid, 'quantity': 2, 'disposition': 'RESTOCK', 'reason': 'EXEMPLE doublon'}, expected=400)
    with scenario('MERCHANT', 'Accrue points from accepted paid goods only'):
        wallet = call('/loyalty/sync', M, {}); require(wallet['available'] == 8 and wallet['reserved'] == 0)
    with scenario('MERCHANT', 'Sync points repeatedly without double earning'):
        repeat = call('/loyalty/sync', M, {}); require(repeat['available'] == 8 and len(repeat['entries']) == len(wallet['entries']))
    with scenario('MERCHANT', 'Discover the wholesaler-created stock-backed gift'):
        require(any(x['id'] == reward['id'] and x['pointsCost'] == 5 for x in call('/rewards', M)['items']))
    with scenario('MERCHANT', 'Reserve an affordable gift using earned points'):
        gift_body = {'idempotencyKey': key(), 'grantId': None}; gift = call('/rewards/' + reward['id'] + '/reserve', M, gift_body); require(gift['pointsCost'] == 5)
    with scenario('MERCHANT', 'Retry a gift reservation without spending points twice'):
        require(call('/rewards/' + reward['id'] + '/reserve', M, gift_body)['id'] == gift['id'])
    with scenario('MERCHANT', 'Show points reserved separately from spendable points'):
        wallet = call('/loyalty', M); require(wallet['available'] == 3 and wallet['reserved'] == 5 and wallet['spent'] == 0)
    with scenario('MERCHANT', 'Prevent reward reservations from overdrawing the points wallet'):
        call('/rewards/' + reward['id'] + '/reserve', M, {'idempotencyKey': key(), 'grantId': None}, expected=409)
    with scenario('WHOLESALER', 'Prepare a merchant reward for handover'):
        gift = call('/admin/gift-requests/' + gift['id'] + '/transition', A, {'version': gift['version'], 'status': 'READY'}); require(gift['status'] == 'READY')
    with scenario('WHOLESALER', 'Fulfill the physical gift with a beneficiary proof reference'):
        gift = call('/admin/gift-requests/' + gift['id'] + '/transition', W, {'version': gift['version'], 'status': 'FULFILLED', 'proofReference': 'EXEMPLE-BENEFICIARY-QA'}); require(gift['status'] == 'FULFILLED')
    with scenario('MERCHANT', 'See gift handover consume the held points exactly once'):
        wallet = call('/loyalty', M); require(wallet['available'] == 3 and wallet['reserved'] == 0 and wallet['spent'] == 5)
    with scenario('MERCHANT', 'Keep loyalty balances confined to the signed-in shop'):
        call('/loyalty?merchantId=' + mid, M2, expected=404); require(call('/loyalty', M2)['available'] == 0)

    with scenario('MERCHANT', 'Open support directly against the actual order context'):
        thread = call('/conversations', M, {'contextType': 'ORDER', 'contextId': oid, 'subject': 'EXEMPLE suivi de commande'}); tid = thread['id']; require(thread['contextId'] == oid)
    with scenario('MERCHANT', 'Send an actual contextual support message'):
        message_body = {'text': 'EXEMPLE merci de confirmer les deux unités refusées', 'internal': False, 'idempotencyKey': key()}; message = call('/conversations/' + tid + '/messages', M, message_body); require(message['senderId'] == mid)
    with scenario('MERCHANT', 'Retry a message without duplicating it'):
        require(call('/conversations/' + tid + '/messages', M, message_body)['id'] == message['id'])
    with scenario('MERCHANT', 'Reject changing message text under an already-used message key'):
        call('/conversations/' + tid + '/messages', M, {**message_body, 'text': 'Changed'}, expected=409)
    with scenario('WHOLESALER', 'Reply as customer service in the same order conversation'):
        answer = call('/conversations/' + tid + '/messages', A, {'text': 'EXEMPLE réception de votre retour confirmée par le dépôt', 'internal': False, 'idempotencyKey': key()}); require(answer['senderId'] == admin['user']['id'])
    with scenario('MERCHANT', 'Read the customer-service response in the order thread'):
        require(any(x['id'] == answer['id'] for x in call('/conversations/' + tid + '/messages', M)['items']))
    with scenario('WHOLESALER', 'Add an internal warehouse coordination note'):
        call('/conversations/' + tid + '/messages', W, {'text': 'EXEMPLE note interne qualité du dépôt', 'internal': True, 'idempotencyKey': key()}); require(len(call('/conversations/' + tid + '/messages', W)['items']) == 3)
    with scenario('MERCHANT', 'Keep private team notes out of merchant messages'):
        visible = call('/conversations/' + tid + '/messages', M)['items']; require(len(visible) == 2 and all(not x['internal'] for x in visible))
    with scenario('WHOLESALER', 'Hand the support dossier to a real warehouse team member'):
        current_thread = call('/conversations/' + tid + '/messages', A)['conversation']; handed = call('/conversations/' + tid, A, {'version': current_thread['version'], 'status': 'OPEN', 'assignedTo': warehouse['user']['id']}, method='PATCH'); require(handed['assignedTo'] == warehouse['user']['id'])
    with scenario('WHOLESALER', 'Reject a stale customer-service handoff instead of overwriting it'):
        call('/conversations/' + tid, A, {'version': current_thread['version'], 'status': 'OPEN', 'assignedTo': finance['user']['id']}, expected=409, method='PATCH')
    with scenario('WHOLESALER', 'Close a resolved contextual support dossier'):
        closed = call('/conversations/' + tid, W, {'version': handed['version'], 'status': 'CLOSED', 'assignedTo': warehouse['user']['id']}, method='PATCH'); require(closed['status'] == 'CLOSED')
    with scenario('MERCHANT', 'Respect a closed support dossier and preserve its message history'):
        call('/conversations/' + tid + '/messages', M, {'text': 'EXEMPLE message après clôture', 'internal': False, 'idempotencyKey': key()}, expected=409)
        require(len(call('/conversations/' + tid + '/messages', M)['items']) == 2)
    with scenario('WHOLESALER', 'Reopen a support dossier for a new customer-service follow-up'):
        reopened = call('/conversations/' + tid, A, {'version': closed['version'], 'status': 'OPEN', 'assignedTo': finance['user']['id']}, method='PATCH'); require(reopened['status'] == 'OPEN')

    with scenario('WHOLESALER', 'Create a quantity-threshold discount campaign'):
        discount = campaign(); require(discount['status'] == 'DRAFT' and discount['discountPercent'] == 10)
    with scenario('WHOLESALER', 'Require an independent reviewer for campaign publication'):
        call('/admin/campaigns/' + discount['id'] + '/publish', A, {'version': discount['version']}, expected=409)
    with scenario('WHOLESALER', 'Publish the verified campaign through finance'):
        discount = publish(discount); require(discount['status'] == 'PUBLISHED' and discount['approvedBy'] == finance['user']['id'])
    with scenario('MERCHANT', 'Discover an actual published promotion created by the wholesaler'):
        require(any(x['id'] == discount['id'] for x in call('/campaigns', M)['items']))
    with scenario('MERCHANT', 'Calculate the promised discount at the qualifying threshold'):
        d = call('/campaigns/' + discount['id'] + '/preview', M, {'quantity': 5}); require(d['discount'] == 100 and d['goodsAfter'] == 900)
    with scenario('MERCHANT', 'Apply no discount below the published campaign threshold'):
        require(call('/campaigns/' + discount['id'] + '/preview', M, {'quantity': 4})['discount'] == 0)
    with scenario('MERCHANT', 'Use the same campaign discount in the real checkout basket'):
        promotion_cart = preview(bpid, 5); require(promotion_cart['canOrder'] and promotion_cart['total'] == 900 and promotion_cart['items'][0]['unitPrice'] == 180)
    with scenario('MERCHANT', 'Commit the actual advertised discount as immutable order terms'):
        promo_order = call('/orders', M, order_payload(bpid, 5, promotion_cart['pricingToken'])); require(promo_order['total'] == 900 and promo_order['items'][0]['unitPrice'] == 180)
    with scenario('WHOLESALER', 'Reject invalid campaign percentages before publication'):
        invalid = {'title': 'EXEMPLE invalid', 'description': 'QA', 'kind': 'DISCOUNT', 'productId': bpid, 'minimumQuantity': 1, 'discountPercent': 101, 'giftQuantity': 1, 'goalQuantity': 0, 'unitFactor': 1, 'awardLimit': 1, 'startsAt': at(-1), 'endsAt': at(3600)}
        call('/admin/campaigns', A, invalid, expected=400)
    with scenario('WHOLESALER', 'Publish a distinct free-goods promotion with a named stocked gift'):
        free = publish(campaign('FREE_GOODS', productId=gpid, giftProductId=pid, minimumQuantity=3)); require(free['giftProductId'] == pid and free['status'] == 'PUBLISHED')
    with scenario('MERCHANT', 'Preview the named free-goods quantity at the threshold'):
        free_preview = call('/campaigns/' + free['id'] + '/preview', M, {'quantity': 3}); require(free_preview['freeQuantity'] == 1)
    with scenario('MERCHANT', 'Reserve real free-gift stock with the paid-goods checkout'):
        gift_cart = preview(gpid, 3); gifted_order = call('/orders', M, order_payload(gpid, 3, gift_cart['pricingToken']))
        require(gifted_order['total'] == 150 and gifted_order['promotions']['items'][0]['giftStockState'] == 'RESERVED')
    with scenario('MERCHANT', 'Show challenge progress from actual accepted paid units'):
        progress = call('/campaigns/' + challenge['id'] + '/progress', M); require(progress['eligibleQuantity'] == 8)
    while dt.datetime.now(dt.timezone.utc) < dt.datetime.fromisoformat(challenge_end): time.sleep(.2)
    with scenario('WHOLESALER', 'Award the completed challenge from the actual paid delivery'):
        grant = call('/admin/campaigns/' + challenge['id'] + '/award', F, {'merchantId': mid}); require(grant['eligibleQuantity'] == 8)
    with scenario('WHOLESALER', 'Retry a challenge award without granting the same shop twice'):
        require(call('/admin/campaigns/' + challenge['id'] + '/award', F, {'merchantId': mid})['id'] == grant['id'])
    with scenario('MERCHANT', 'Claim a completed challenge reward without spending wallet points'):
        challenge_body = {'idempotencyKey': key(), 'grantId': grant['id']}; challenge_gift = call('/rewards/' + reward['id'] + '/reserve', M, challenge_body); require(challenge_gift['pointsCost'] == 0 and call('/loyalty', M)['available'] == 3)
    with scenario('MERCHANT', 'Reject a second independent claim of the consumed challenge grant'):
        call('/rewards/' + reward['id'] + '/reserve', M, {'idempotencyKey': key(), 'grantId': grant['id']}, expected=409)

    with scenario('MERCHANT', 'Start with marketing disabled while service messaging remains available'):
        pref = call('/preferences/marketing', M); require(pref['optIn'] is False and pref['serviceMessages'])
    def broadcast(purpose):
        b = call('/admin/broadcasts', A, {'purpose': purpose, 'title': 'EXEMPLE message QA', 'body': 'EXEMPLE contenu de test', 'merchantIds': [mid], 'idempotencyKey': key()})
        return call('/admin/broadcasts/' + b['id'] + '/dispatch', A, {})
    with scenario('WHOLESALER', 'Exclude a merchant who declined promotional messages'):
        b = broadcast('PROMOTIONAL'); require(b['broadcast']['sent'] == 0 and b['broadcast']['excluded'] == 1)
    with scenario('MERCHANT', 'Explicitly opt into commercial messages'):
        require(call('/preferences/marketing', M, {'optIn': True}, method='PUT')['optIn'])
    with scenario('WHOLESALER', 'Deliver an opted-in promotional message in the shared inbox'):
        b = broadcast('PROMOTIONAL'); require(b['broadcast']['sent'] == 1)
    with scenario('MERCHANT', 'Withdraw marketing consent without losing service communications'):
        require(call('/preferences/marketing', M, {'optIn': False}, method='PUT')['optIn'] is False)
    with scenario('WHOLESALER', 'Deliver a service message independently of marketing preference'):
        b = broadcast('SERVICE'); require(b['broadcast']['sent'] == 1)

    with scenario('WHOLESALER', 'Search actual merchants by shop name'):
        merchants = call('/admin/merchants?q=' + query(second['user']['shopName']), A); require(any(x['id'] == mid2 for x in merchants['items']))
    with scenario('WHOLESALER', 'Search actual merchants by telephone number'):
        merchants = call('/admin/merchants?q=0555123456', A); require(merchants['total'] == 1 and merchants['items'][0]['id'] == mid2)
    with scenario('WHOLESALER', 'Filter the order queue by the actual order number'):
        found = call('/admin/orders?q=' + query(order['number']), A); require(found['total'] == 1 and found['items'][0]['id'] == oid)
    with scenario('WHOLESALER', 'Calculate dashboard sales and collections from the completed journey'):
        dashboard = call('/admin/dashboard?days=7', A); require(dashboard['summary']['sales'] == 800 and dashboard['summary']['collected'] == 800 and len(dashboard['daily']) == 7)
    with scenario('WHOLESALER', 'Keep actual actors and handoffs in the audit history'):
        audit = call('/admin/audit?pageSize=100', A); require(any(x['action'] == 'CONVERSATION_UPDATED' for x in audit['items']) and any(x['action'] == 'PREPARATION_UPDATED' for x in audit['items']))
    with scenario('MERCHANT', 'Calculate own account totals from recorded payment facts'):
        summary = call('/profile/summary', M); require(summary['partiallyDeliveredOrders'] == 1 and summary['netPaidThisMonth'] == 800 and summary['orderCount'] == 3)
    with scenario('MERCHANT', 'Reorder a previous receipt into a fresh basket without duplicating its payment'):
        reordered = call('/orders/' + oid + '/reorder', M, {}); require(any(x['product']['id'] == pid and x['quantity'] == 10 for x in reordered['items']))
        require(len(call('/orders/' + oid, M)['payments']) == 1)
    with scenario('WHOLESALER', 'Change a catalogue offer without rewriting existing agreed order prices'):
        offer(pid, 125, 492); require(call('/orders/' + oid, A)['items'][0]['unitPrice'] == 100)
    with scenario('MERCHANT', 'Display the new current price while preserving the paid order history'):
        require(call('/products/' + pid, M)['price'] == 125 and call('/orders/' + oid, M)['items'][0]['unitPrice'] == 100)
    with scenario('WHOLESALER', 'Reject a warehouse attempt to publish a campaign'):
        call('/admin/campaigns', W, invalid, expected=403)
    with scenario('WHOLESALER', 'Suspend a shop and revoke its effective access'):
        call('/admin/merchants/' + mid2, A, {'approvalStatus': 'SUSPENDED'}, method='PATCH'); call('/orders', M2, expected=401)
    with scenario('WHOLESALER', 'Restore a suspended shop without erasing its history'):
        call('/admin/merchants/' + mid2, A, {'approvalStatus': 'APPROVED'}, method='PATCH'); require(call('/orders', M2)['total'] == 0)
    with scenario('MERCHANT', 'Protect contextual support against another shop opening the same order'):
        call('/conversations', M2, {'contextType': 'ORDER', 'contextId': oid, 'subject': 'EXEMPLE accès interdit'}, expected=404)
    with scenario('MERCHANT', 'Keep another workspace from following a delivery'):
        call('/orders/' + oid + '/tracking', X, expected=404)
    # Two simultaneous accepted checkouts compete for one physically available unit.
    race_pid = products[3]['id']; offer(race_pid, 10, 1)
    c1 = preview(race_pid, 1); c2 = preview(race_pid, 1, M2)
    second_address = call('/addresses', M2)[0]
    with scenario('WHOLESALER', 'Prevent overselling when two shops commit the last unit concurrently'):
        b1 = order_payload(race_pid, 1, c1['pricingToken']); b2 = {**order_payload(race_pid, 1, c2['pricingToken']), 'addressId': second_address['id']}
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            outcomes = list(pool.map(lambda pair: call('/orders', pair[0], pair[1], expected=(200, 409)), [(M, b1), (M2, b2)]))
        require(sum('id' in x for x in outcomes) == 1 and call('/products/' + race_pid, A)['reserved'] == 1)
    with scenario('MERCHANT', 'See a clear sold-out checkout after the last available unit is reserved'):
        sold = preview(race_pid, 1); require(not sold['canOrder'] and sold.get('pricingToken') is None)
    counts = {role: sum(x['pass'] and x['role'] == role for x in checks) for role in ('WHOLESALER', 'MERCHANT')}
    require(counts['WHOLESALER'] >= 50 and counts['MERCHANT'] >= 50, 'The requested 50+50 distinct business cases were not reached')
    success = True
finally:
    counts = {role: sum(x['pass'] and x['role'] == role for x in checks) for role in ('WHOLESALER', 'MERCHANT')}
    result = {'success': success, 'base': BASE, 'startedAt': started, 'finishedAt': at(), 'passed': sum(x['pass'] for x in checks),
        'failed': sum(not x['pass'] for x in checks), 'passedByRole': counts, 'httpRequests': len(requests),
        'scope': 'Fresh isolated fictional workspace; actual HTTP business effects and authorization. No browser, native runtime, market prices, physical GPS device or external messaging acceptance is claimed.',
        'checks': checks}
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({k: result[k] for k in ('success', 'passed', 'failed', 'passedByRole', 'httpRequests')}, ensure_ascii=False), flush=True)
    if not success: sys.exit(1)
