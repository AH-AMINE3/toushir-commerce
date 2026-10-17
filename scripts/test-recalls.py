#!/usr/bin/env python3
"""Actual API regression for batch recall custody, safety blocks and honest closure.

Run against an isolated demo-enabled server. Creates fictional fixtures in a fresh
workspace and never sends external messages. No direct database edits or mocks.
READY/partial-scan reservations require explicit administrator recall cancellation
before their real depot units can be isolated. Fictional prepayment stays recorded.
"""
import datetime as dt
import json
import os
import pathlib
import sys
import time
import urllib.error
import urllib.request
import uuid

BASE = (sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:5883').rstrip('/') + '/api'
checks, requests, success = [], 0, False


def key():
    return uuid.uuid4().hex


def at(days=0):
    return (dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=days)).isoformat()


def call(method, path, body=None, token=None, expected=200, code=None, retries=0):
    global requests
    retry_after = 60
    requests += 1
    req = urllib.request.Request(
        BASE + path, method=method,
        data=json.dumps(body).encode() if body is not None else None,
        headers={'Content-Type': 'application/json', **({'Authorization': 'Bearer ' + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=90) as response:
            status, data = response.status, json.load(response)
    except urllib.error.HTTPError as error:
        status = error.code
        if status == 429:
            retry_after = max(1, min(60, int(error.headers.get('Retry-After', '60'))))
        try:
            data = json.load(error)
        except Exception:
            data = {}
    if status == 429 and retries < 3:
        time.sleep(retry_after)
        return call(method, path, body, token, expected, code, retries + 1)
    allowed = expected if isinstance(expected, tuple) else (expected,)
    assert status in allowed, (method, path, status, expected, data)
    if code:
        assert data.get('error', {}).get('code') == code, (method, path, data, code)
    return data


def ok(name, condition=True):
    assert condition, name
    checks.append(name)
    print('PASS ' + name, flush=True)


def same_stock(before, after):
    return before['stock'] == after['stock']


def physical(view):
    return view['stock']['onHand'] + view['stock']['quarantined']


try:
    admin = call('POST', '/demo/session', {'role': 'ADMIN'})
    A, workspace = admin['token'], admin['workspaceToken']

    def persona(role):
        return call('POST', '/demo/session', {'role': role, 'workspaceToken': workspace})

    merchant, warehouse, driver = persona('MERCHANT'), persona('WAREHOUSE'), persona('DRIVER')
    M, W, D = merchant['token'], warehouse['token'], driver['token']
    F, dispatcher = persona('FINANCE')['token'], persona('DISPATCHER')['token']
    outsider = call('POST', '/demo/session', {'role': 'ADMIN'})
    X = outsider['token']
    outsider_merchant = call('POST', '/demo/session', {'role': 'MERCHANT', 'workspaceToken': outsider['workspaceToken']})['token']
    products = call('GET', '/products?pageSize=4', token=M)['items']
    assert len(products) >= 3, 'Recall regression needs three catalog products'
    p1, p2, p3 = [p['id'] for p in products[:3]]
    address = call('GET', '/addresses', token=M)[0]

    def offer(product, quantity):
        return call('PATCH', '/admin/products/' + product, {
            'price': 1200, 'stock': quantity, 'caseSize': 1, 'minimumQuantity': 1,
            'offerApproved': True, 'isActive': True}, A)

    def quoted(product, quantity):
        order = call('POST', '/orders', {
            'idempotencyKey': key(), 'addressId': address['id'],
            'items': [{'productId': product, 'quantity': quantity}],
            'mode': 'REQUEST_QUOTE', 'note': 'EXEMPLE · recall API verification'}, M)
        return call('POST', '/admin/orders/' + order['id'] + '/quote', {
            'version': order['version'], 'campaignIds': [],
            'items': [{'productId': product, 'quantity': quantity, 'unitPrice': 1200,
                       'caseSize': 1, 'availableStock': None}],
            'deliveryFee': 0, 'validUntil': at(1), 'note': 'EXEMPLE · recall fixture'}, A)

    def confirmed(product, quantity):
        order = quoted(product, quantity)
        return call('POST', '/orders/' + order['id'] + '/accept-quote', {'version': order['quoteVersion']}, M)

    def wave(order, scan=True):
        result = call('POST', '/admin/warehouse/waves', {
            'name': 'EXEMPLE · recall scan fixture', 'orderIds': [order['id']], 'idempotencyKey': key()}, W)
        if scan:
            for task in result['items']:
                result = call('POST', '/admin/warehouse/tasks/' + task['id'] + '/scan', {
                    'version': task['version'], 'barcode': task['barcode'],
                    'lotNumber': task['lotNumber'], 'quantity': task['quantity'], 'idempotencyKey': key()}, W)
        return result

    def delivered(product, quantity, routed=False):
        order = confirmed(product, quantity)
        wave(order)
        if not routed:
            call('POST', '/admin/orders/' + order['id'] + '/transition', {
                'status': 'OUT_FOR_DELIVERY', 'reason': 'EXEMPLE · physical departure'}, A)
            return call('POST', '/admin/orders/' + order['id'] + '/delivery', {
                'items': [{'productId': product, 'acceptedQuantity': quantity, 'refusedQuantity': 0}],
                'note': 'EXEMPLE · fictional delivered goods'}, A)
        route = call('POST', '/operations/routes', {
            'name': 'EXEMPLE · recall driver custody', 'driverId': driver['user']['id'],
            'orderIds': [order['id']], 'openingFloat': 0, 'capacityUnits': 20, 'vehicleReference': 'EXEMPLE'}, A)
        challenge = call('POST', '/operations/routes/' + route['id'] + '/manifest-challenge', {'version': route['version']}, W)
        call('POST', '/courier/routes/' + route['id'] + '/accept-custody', {
            'version': route['version'], 'challenge': challenge['challenge']}, D)
        call('POST', '/courier/orders/' + order['id'] + '/status', {'status': 'EN_ROUTE'}, D)
        tracking = call('POST', '/courier/orders/' + order['id'] + '/status', {'status': 'ARRIVED'}, D)
        items = [{'productId': product, 'acceptedQuantity': quantity, 'refusedQuantity': 0}]
        receipt = call('POST', '/orders/' + order['id'] + '/receipt-challenge', {'version': tracking['version'], 'items': items}, M)
        call('POST', '/courier/orders/' + order['id'] + '/complete', {'challenge': receipt['challenge'], 'items': items}, D)
        return call('GET', '/orders/' + order['id'], token=M)

    def lots(product=p1):
        return call('GET', '/admin/warehouse/lots?productId=' + product, token=W)

    def activate(product, quantity, number):
        offer(product, quantity)
        result = call('POST', '/admin/warehouse/activate', {
            'productId': product, 'expectedPhysicalStock': quantity,
            'reason': 'EXEMPLE · actual counted lot', 'proofReference': 'EXEMPLE-COUNT-' + key(),
            'idempotencyKey': key(), 'lots': [{'lotNumber': number, 'expiresAt': at(30),
                                             'quantity': quantity, 'quarantined': 0}]}, W)
        return result['items'][0]

    def trace(recall_id, token=W):
        return call('GET', '/admin/recalls/' + recall_id, token=token)

    def position(detail, kind, order_id=None):
        matches = [p for p in detail['positions'] if p['kind'] == kind and (order_id is None or p['orderId'] == order_id)]
        assert len(matches) == 1, (kind, order_id, matches)
        return matches[0]

    def recovery(detail, pos, quantity, **extra):
        return {'version': detail['recall']['version'], 'positionId': pos['id'], 'quantity': quantity,
                'proofReference': 'EXEMPLE · physical custody verification', 'idempotencyKey': key(), **extra}

    def recover(recall_id, body, expected=200, token=W, code=None):
        return call('POST', '/admin/recalls/' + recall_id + '/recover', body, token, expected, code)

    def close(detail, expected=200, **extra):
        return call('POST', '/admin/recalls/' + detail['recall']['id'] + '/close', {
            'version': detail['recall']['version'], 'note': 'EXEMPLE · evidence reviewed', 'idempotencyKey': key(), **extra}, A, expected)

    call('GET', '/admin/recalls', expected=401)
    for token in [M, D, F, dispatcher]:
        call('GET', '/admin/recalls', token=token, expected=403)
    for token in [A, W]:
        call('GET', '/admin/recalls', token=token)
    call('POST', '/admin/recalls', {
        'lotId': p3, 'version': 1, 'reason': 'EXEMPLE · untracked product is not a batch',
        'safetyInstruction': 'EXEMPLE · isolate the exact batch', 'idempotencyKey': key()}, W, (404, 409))
    ok('anonymous and unauthorized roles cannot trace recalls; an untracked product cannot masquerade as a lot',
       call('GET', '/admin/recalls', token=W)['items'] == [])

    root_lot = activate(p1, 20, 'EXEMPLE-RECALL-A')
    shipped = delivered(p1, 4, routed=True)
    pending = confirmed(p1, 2)
    future = quoted(p1, 1)
    before_recall = lots()
    root_lot = next(lot for lot in before_recall['items'] if lot['id'] == root_lot['id'])
    create = {'lotId': root_lot['id'], 'version': root_lot['version'],
              'reason': 'EXEMPLE · manufacturer safety recall',
              'safetyInstruction': 'EXEMPLE · stop sale; retain batch in marked quarantine', 'idempotencyKey': key()}
    call('POST', '/admin/recalls', {**create, 'version': root_lot['version'] - 1, 'idempotencyKey': key()}, W, 409)
    for token in [M, D, F]:
        call('POST', '/admin/recalls', create, token, 403)
    detail = call('POST', '/admin/recalls', create, W)
    recall_id = detail['recall']['id']
    replay = call('POST', '/admin/recalls', create, W)
    call('POST', '/admin/recalls', {**create, 'reason': 'EXEMPLE · changed request'}, W, 409, 'IDEMPOTENCY_CONFLICT')
    call('POST', '/admin/recalls', {**create, 'idempotencyKey': key()}, W, 409)
    depot, outbound = position(detail, 'DEPOT'), position(detail, 'OUTBOUND', shipped['id'])
    ok('recall creation is versioned and idempotent without inventing a physical movement',
       replay['recall']['id'] == recall_id and same_stock(before_recall, lots()) and depot['quantity'] == 16
       and outbound['quantity'] == 4 and outbound['recoveredQuantity'] == 0)
    ok('preparation is traceable without double-counting the depot custody',
       any(p['kind'] == 'PREPARATION' and p['orderId'] == pending['id'] and not p['requiresRecovery'] for p in detail['positions']))
    shipped_now = call('GET', '/orders/' + shipped['id'], token=M)
    call('POST', '/admin/recalls/' + recall_id + '/cancel-preparation', {
        'version': detail['recall']['version'], 'orderId': shipped['id'], 'orderVersion': shipped_now['version'],
        'reason': 'EXEMPLE · goods already under driver custody cannot be canceled back into depot stock',
        'idempotencyKey': key()}, A, 409)
    call('GET', '/admin/recalls/' + recall_id, token=X, expected=404)
    call('POST', '/admin/recalls/' + recall_id + '/recover', recovery(detail, depot, 1), X, 404)
    ok('foreign workspace cannot inspect or mutate another recall',
       call('GET', '/admin/recalls', token=X)['items'] == [] and
       call('GET', '/recalls/inbox', token=outsider_merchant)['items'] == [])

    call('POST', '/orders/' + future['id'] + '/accept-quote', {'version': future['quoteVersion']}, M, 409, 'LOT_RECALLED')
    pending_now = call('GET', '/orders/' + pending['id'], token=M)
    call('PATCH', '/operations/orders/' + pending['id'] + '/preparation', {
        'version': pending_now['version'], 'items': [{'productId': p1, 'preparedQuantity': 2}]}, W, 409, 'LOT_RECALLED')
    call('POST', '/admin/warehouse/lots/' + root_lot['id'] + '/count', {
        'version': root_lot['version'], 'quantity': 17, 'reason': 'EXEMPLE · cannot relabel recalled goods as sellable',
        'proofReference': 'EXEMPLE-COUNT-BLOCK', 'idempotencyKey': key()}, W, 409, 'LOT_RECALLED')
    ok('new allocation, reserved preparation and recount all fail with LOT_RECALLED', same_stock(before_recall, lots()))

    before_ack = trace(recall_id)
    for token, person in [(M, merchant), (D, driver)]:
        inbox = call('GET', '/recalls/inbox', token=token)['items']
        item = next(item for item in inbox if item['recall']['id'] == recall_id)
        notice = item['notice']
        body = {'version': notice['version'], 'idempotencyKey': key()}
        call('POST', '/recalls/notices/' + notice['id'] + '/ack', {**body, 'version': notice['version'] - 1, 'idempotencyKey': key()}, token, 409)
        call('POST', '/recalls/notices/' + notice['id'] + '/ack', body, outsider_merchant, 404)
        call('POST', '/recalls/notices/' + notice['id'] + '/ack', body, token)
        call('POST', '/recalls/notices/' + notice['id'] + '/ack', body, token)
        call('POST', '/recalls/notices/' + notice['id'] + '/ack', {**body, 'version': notice['version'] + 1}, token, 409, 'IDEMPOTENCY_CONFLICT')
        acknowledged = next(item['notice'] for item in call('GET', '/recalls/inbox', token=token)['items'] if item['recall']['id'] == recall_id)
        assert acknowledged['receivedAt'] and acknowledged['receivedBy'] == person['user']['id'] and acknowledged['version'] == notice['version'] + 1, acknowledged
    after_ack = trace(recall_id)
    ok('merchant and assigned driver receive scoped versioned notices; acknowledgements never count as physical recovery',
       sorted((p['id'], p['recoveredQuantity'], p['exceptionQuantity']) for p in before_ack['positions']) ==
       sorted((p['id'], p['recoveredQuantity'], p['exceptionQuantity']) for p in after_ack['positions']) and same_stock(before_recall, lots()))

    detail = after_ack
    depot = position(detail, 'DEPOT')
    for token in [M, D, F]:
        recover(recall_id, recovery(detail, depot, 1), 403, token)
    recover(recall_id, recovery(detail, depot, 15), 409)
    close(detail, 409)
    ok('depot recovery requires staff, cannot consume reserved units and unresolved depot stock blocks closure')
    body = recovery(detail, depot, 5)
    before = lots()
    first = recover(recall_id, body)
    replay = recover(recall_id, body)
    recover(recall_id, {**body, 'quantity': 6}, 409, code='IDEMPOTENCY_CONFLICT')
    recover(recall_id, {**body, 'idempotencyKey': key()}, 409)
    after = lots()
    ok('depot recovery moves exactly five existing units into quarantine, with replay and stale-write protection',
       before['stock']['onHand'] - after['stock']['onHand'] == 5 and
       after['stock']['quarantined'] - before['stock']['quarantined'] == 5 and physical(before) == physical(after) and
       position(first, 'DEPOT')['recoveredQuantity'] == 5 and position(replay, 'DEPOT')['recoveredQuantity'] == 5 and
       len(first['events']) == len(replay['events']))
    detail = trace(recall_id)
    detail = recover(recall_id, recovery(detail, position(detail, 'DEPOT'), 9))
    close(detail, 409)
    call('POST', '/orders/' + pending['id'] + '/cancel', {'reason': 'EXEMPLE · recall cancellation releases only the existing reservation'}, M)
    detail = trace(recall_id)
    detail = recover(recall_id, recovery(detail, position(detail, 'DEPOT'), 2))
    ok('reserved units become recoverable only after the real reservation is released',
       lots()['stock'] == {'onHand': 0, 'reserved': 0, 'quarantined': 16} and position(detail, 'DEPOT')['recoveredQuantity'] == 16)
    isolation = next(lot for lot in lots()['items'] if lot['quarantined'] > 0)
    call('POST', '/admin/warehouse/lots/' + isolation['id'] + '/quality', {
        'version': isolation['version'], 'quantity': 1, 'disposition': 'RELEASE',
        'reason': 'EXEMPLE · forbidden recalled quarantine release', 'idempotencyKey': key()}, W, 409, 'LOT_RECALLED')
    ok('recall quarantine cannot be released through ordinary lot quality controls')

    outbound = position(detail, 'OUTBOUND', shipped['id'])
    recover(recall_id, recovery(detail, outbound, 1), 409)
    recover(recall_id, recovery(detail, outbound, 1, quarantineLotId=isolation['id']), 409)
    claim = call('POST', '/orders/' + shipped['id'] + '/claims', {
        'productId': p1, 'quantity': 2, 'reason': 'EXEMPLE · counted recall return'}, M)
    call('POST', '/admin/claims/' + claim['id'] + '/resolve', {'status': 'ACCEPTED', 'note': 'EXEMPLE · authorized physical return'}, A)
    identity = {'sourceType': 'CLAIM', 'sourceId': claim['id'], 'quantity': 2,
                'lotNumber': root_lot['lotNumber'], 'expiresAt': root_lot['expiresAt'],
                'proofReference': 'EXEMPLE · exact shipped batch physically checked', 'idempotencyKey': key()}
    call('POST', '/admin/warehouse/returns/identify', identity, W, 409)
    call('POST', '/admin/claims/' + claim['id'] + '/receive', {'quantity': 2, 'note': 'EXEMPLE · two units physically received at depot'}, A)
    returned_view = call('POST', '/admin/warehouse/returns/identify', identity, W)
    returned = next(lot for lot in returned_view['items'] if lot['sourceRef'] == 'CLAIM:' + claim['id'])
    call('POST', '/admin/claims/' + claim['id'] + '/inspect', {
        'quantity': 1, 'disposition': 'RESTOCK', 'reason': 'EXEMPLE · recalled return must remain held'}, A, 409, 'LOT_RECALLED')
    before = lots()
    detail = trace(recall_id)
    body = recovery(detail, position(detail, 'OUTBOUND', shipped['id']), 2, quarantineLotId=returned['id'])
    detail = recover(recall_id, body)
    replay = recover(recall_id, body)
    recover(recall_id, {**body, 'quantity': 1}, 409, code='IDEMPOTENCY_CONFLICT')
    recover(recall_id, recovery(detail, position(detail, 'OUTBOUND', shipped['id']), 1, quarantineLotId=returned['id']), 409)
    ok('outbound recovery requires an exact physically received RETURN lot; linking and replay add no stock',
       same_stock(before, lots()) and before['stock']['quarantined'] == 18 and
       position(detail, 'OUTBOUND', shipped['id'])['recoveredQuantity'] == 2 and
       position(replay, 'OUTBOUND', shipped['id'])['recoveredQuantity'] == 2)

    exception = {'version': detail['recall']['version'], 'positionId': position(detail, 'OUTBOUND', shipped['id'])['id'],
                 'quantity': 2, 'reason': 'EXEMPLE · two delivered units confirmed unavailable after documented contact',
                 'proofReference': 'EXEMPLE · residual custody evidence', 'idempotencyKey': key()}
    path = '/admin/recalls/' + recall_id + '/exceptions'
    call('POST', path, exception, W, 403)
    call('POST', path, {**exception, 'reason': '', 'idempotencyKey': key()}, A, 400)
    call('POST', path, {**exception, 'positionId': position(detail, 'DEPOT')['id'], 'idempotencyKey': key()}, A, (400, 409))
    detail = call('POST', path, exception, A)
    call('POST', path, exception, A)
    call('POST', path, {**exception, 'reason': 'EXEMPLE · changed reason'}, A, 409, 'IDEMPOTENCY_CONFLICT')
    ok('only ADMIN can record a reasoned residual outbound exception; it never becomes recovered stock',
       position(detail, 'OUTBOUND', shipped['id'])['exceptionQuantity'] == 2 and
       position(detail, 'OUTBOUND', shipped['id'])['recoveredQuantity'] == 2 and same_stock(before, lots()))
    detail = trace(recall_id)
    closed = close(detail)
    ok('closure follows reconciled depot custody and documented outbound residuals', closed['recall']['status'] == 'CLOSED')
    returned_now = next(lot for lot in lots()['items'] if lot['id'] == returned['id'])
    call('POST', '/admin/warehouse/lots/' + returned_now['id'] + '/count', {
        'version': returned_now['version'], 'quantity': 1, 'reason': 'EXEMPLE · closed recall is still unsafe',
        'proofReference': 'EXEMPLE-CLOSED-RECOUNT', 'idempotencyKey': key()}, W, 409, 'LOT_RECALLED')
    call('POST', '/admin/claims/' + claim['id'] + '/inspect', {
        'quantity': 1, 'disposition': 'RESTOCK', 'reason': 'EXEMPLE · closed recall still cannot restock'}, A, 409, 'LOT_RECALLED')
    ok('CLOSED records retain permanent batch safety blocks', same_stock(before, lots()))

    # Recall cancellation releases a contract reservation, then physical recovery
    # remains a separate operation. A planned manifest must lose its old QR.
    ready_lot = activate(p3, 3, 'EXEMPLE-RECALL-READY')
    ready = confirmed(p3, 1)
    wave(ready)
    paid_ready = call('POST', '/admin/orders/' + ready['id'] + '/payments', {
        'amount': 100, 'method': 'CASH', 'reference': 'EXEMPLE · fictional recall prepayment', 'idempotencyKey': key()}, A)
    ready_route = call('POST', '/operations/routes', {
        'name': 'EXEMPLE · planned recalled preparation', 'driverId': driver['user']['id'],
        'orderIds': [ready['id']], 'openingFloat': 0, 'capacityUnits': 3, 'vehicleReference': 'EXEMPLE'}, A)
    ready_qr = call('POST', '/operations/routes/' + ready_route['id'] + '/manifest-challenge',
                    {'version': ready_route['version']}, W)
    partial = confirmed(p3, 2)
    partial_wave = wave(partial, scan=False)
    partial_task = partial_wave['items'][0]
    partial_wave = call('POST', '/admin/warehouse/tasks/' + partial_task['id'] + '/scan', {
        'version': partial_task['version'], 'barcode': partial_task['barcode'],
        'lotNumber': partial_task['lotNumber'], 'quantity': 1, 'idempotencyKey': key()}, W)
    partial_task = partial_wave['items'][0]
    ready_lot = next(lot for lot in lots(p3)['items'] if lot['id'] == ready_lot['id'])
    ready_recall = call('POST', '/admin/recalls', {
        'lotId': ready_lot['id'], 'version': ready_lot['version'], 'reason': 'EXEMPLE · ready shipment recall',
        'safetyInstruction': 'EXEMPLE · hold the prepared batch before departure', 'idempotencyKey': key()}, W)
    before_ready = lots(p3)
    call('POST', '/admin/orders/' + ready['id'] + '/transition', {
        'status': 'OUT_FOR_DELIVERY', 'reason': 'EXEMPLE · forbidden recalled departure'}, A, 409, 'LOT_RECALLED')
    call('POST', '/admin/warehouse/tasks/' + partial_task['id'] + '/scan', {
        'version': partial_task['version'], 'barcode': partial_task['barcode'],
        'lotNumber': partial_task['lotNumber'], 'quantity': 1, 'idempotencyKey': key()}, W, 409, 'LOT_RECALLED')
    recover(ready_recall['recall']['id'], recovery(ready_recall, position(ready_recall, 'DEPOT'), 1), 409)
    close(ready_recall, 409)
    ok('recalled reserved goods cannot depart, complete remaining scans or be falsely recovered',
       same_stock(before_ready, lots(p3)) and call('GET', '/orders/' + ready['id'], token=M)['status'] == 'READY' and
       call('GET', '/orders/' + partial['id'], token=M)['status'] == 'PREPARING')
    ready_now = call('GET', '/orders/' + ready['id'], token=M)
    cancel_body = {
        'version': ready_recall['recall']['version'], 'orderId': ready['id'], 'orderVersion': ready_now['version'],
        'reason': 'EXEMPLE · administrator stops recalled preparation before any physical driver custody',
        'idempotencyKey': key()}
    cancel_path = '/admin/recalls/' + ready_recall['recall']['id'] + '/cancel-preparation'
    for token in [W, M, D, F]:
        call('POST', cancel_path, cancel_body, token, 403)
    call('POST', cancel_path, cancel_body, X, 404)
    call('POST', cancel_path, {**cancel_body, 'reason': 'short', 'idempotencyKey': key()}, A, 400)
    call('POST', cancel_path, {**cancel_body, 'version': cancel_body['version'] - 1, 'idempotencyKey': key()}, A, 409)
    call('POST', cancel_path, {**cancel_body, 'orderVersion': cancel_body['orderVersion'] - 1, 'idempotencyKey': key()}, A, 409)
    canceled_detail = call('POST', cancel_path, cancel_body, A)
    canceled_replay = call('POST', cancel_path, cancel_body, A)
    call('POST', cancel_path, {**cancel_body, 'reason': 'EXEMPLE · changed cancellation evidence'}, A, 409, 'IDEMPOTENCY_CONFLICT')
    call('POST', cancel_path, {**cancel_body, 'idempotencyKey': key()}, A, 409)
    ready_canceled = call('GET', '/orders/' + ready['id'], token=M)
    after_cancel = lots(p3)
    ok('explicit recall cancellation is ADMIN-only, versioned and idempotent; it releases only the actual reservation',
       ready_canceled['status'] == 'CANCELLED' and after_cancel['stock'] == {'onHand': 3, 'reserved': 2, 'quarantined': 0} and
       position(canceled_detail, 'DEPOT')['recoveredQuantity'] == 0 and
       len(canceled_detail['events']) == len(canceled_replay['events']))
    ok('canceling a paid recalled preparation voids the contract while preserving the recorded payment and refund due',
       ready_canceled['total'] == 0 and ready_canceled['paymentStatus'] == 'REFUND_DUE' and
       ready_canceled['balance']['collected'] == 100 and ready_canceled['balance']['refundDue'] == 100 and
       ready_canceled['payments'] == paid_ready['payments'] and not any(payment['isRefund'] for payment in ready_canceled['payments']) and
       any(event['action'] == 'CONTRACT_VOIDED' for event in ready_canceled['timeline']))
    route_after = call('GET', '/operations/routes/' + ready_route['id'], token=W)
    tracking_after = call('GET', '/orders/' + ready['id'] + '/tracking', token=W)
    assignment_after = tracking_after['assignment']
    call('POST', '/courier/routes/' + ready_route['id'] + '/accept-custody', {
        'version': route_after['version'], 'challenge': ready_qr['challenge']}, D, 409, 'CUSTODY_INVALID')
    ok('recall cancellation detaches the unpicked route stop and invalidates its old loading QR',
       route_after['version'] > ready_route['version'] and
       not any(stop['id'] == ready['id'] for stop in route_after['stops']) and
       (assignment_after is None or (assignment_after['routeId'] == '' and assignment_after['status'] == 'CANCELLED')) and
       route_after['custody'] is None)
    ready_recall = recover(ready_recall['recall']['id'], recovery(canceled_detail, position(canceled_detail, 'DEPOT'), 1))
    close(ready_recall, 409)
    partial_now = call('GET', '/orders/' + partial['id'], token=M)
    partial_cancel = {
        'version': ready_recall['recall']['version'], 'orderId': partial['id'], 'orderVersion': partial_now['version'],
        'reason': 'EXEMPLE · administrator stops partially scanned recalled preparation before driver custody',
        'idempotencyKey': key()}
    ready_recall = call('POST', cancel_path, partial_cancel, A)
    partial_canceled = call('GET', '/orders/' + partial['id'], token=M)
    ok('partially scanned recall cancellation releases the remaining reservation without fabricating a physical movement',
       partial_canceled['status'] == 'CANCELLED' and lots(p3)['stock'] == {'onHand': 2, 'reserved': 0, 'quarantined': 1} and
       position(ready_recall, 'DEPOT')['recoveredQuantity'] == 1)
    ready_recall = recover(ready_recall['recall']['id'], recovery(ready_recall, position(ready_recall, 'DEPOT'), 2))
    ready_recall = close(ready_recall)
    ok('prepared recalled stock can close only after explicit cancellation and separate complete physical isolation',
       ready_recall['recall']['status'] == 'CLOSED' and lots(p3)['stock'] == {'onHand': 0, 'reserved': 0, 'quarantined': 3} and
       position(ready_recall, 'DEPOT')['recoveredQuantity'] == 3)

    # Legacy outbound units have no manufacturer-lot identity. Count only the
    # stock physically present today and keep historical exposure explicitly unknown.
    offer(p2, 5)
    legacy = delivered(p2, 2)
    unknown_lot = activate(p2, 3, 'EXEMPLE-POST-LEGACY-COUNT')
    unknown = call('POST', '/admin/recalls', {
        'lotId': unknown_lot['id'], 'version': unknown_lot['version'],
        'reason': 'EXEMPLE · recall with historical identity gap',
        'safetyInstruction': 'EXEMPLE · isolate identified stock; investigate untracked shipments', 'idempotencyKey': key()}, A)
    ok('historical untracked shipments remain unknown rather than being assigned the newly counted batch',
       unknown['unknownScope']['historicalOrderCount'] >= 1 and unknown['unknownScope']['historicalQuantity'] >= 2
       and unknown['unknownScope']['note'] and not any(p['kind'] == 'OUTBOUND' and p['orderId'] == legacy['id'] for p in unknown['positions']))
    unknown = recover(unknown['recall']['id'], recovery(unknown, position(unknown, 'DEPOT'), 3))
    close(unknown, 409)
    unknown = close(unknown, unknownScopeReason='EXEMPLE · legacy shipment batch identity cannot be reconstructed; gap retained in record')
    ok('unknown scope needs an explicit closure reason and remains visible after closure',
       unknown['recall']['status'] == 'CLOSED' and unknown['unknownScope']['historicalQuantity'] >= 2)
    success = True
finally:
    report = {'suite': 'batch-recalls', 'success': success, 'passed': len(checks), 'checks': checks,
              'httpRequests': requests, 'executedAt': at(), 'base': BASE,
              'scope': 'Actual API mutations and fictional payment records in isolated demo workspaces; no database edits, external messages or real payments.'}
    report_path = pathlib.Path(os.environ.get('TOUSHIR_RECALL_RESULTS', 'work/recalls-verification.json'))
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')
print(f'{len(checks)} checks passed; {requests} HTTP requests.')
