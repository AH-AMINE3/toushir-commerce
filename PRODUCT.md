# Toushir Gros

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Stack

Confirmed by the user: .NET 10 backend, React Native merchant application. Expo supports native iOS/Android and an accessible browser edition. A separate React wholesaler console shares the API and visual tokens. Deployment choices are delegated; paid services have not been authorized.

## Users

The user operates a wholesale business or recruits a wholesale partner in Oran. Independent grocery merchants request supplies from their phones. The wholesaler receives requests, sends commercial offers, prepares orders, assigns delivery and follows collections. In the implemented first version, one ADMIN account manages preparation, delivery and finances. Separate staff permissions and assignment are a future extension.

## Product Purpose

Give an Oran grocery merchant a fast way to find products, build a repeatable replenishment basket, obtain confirmed wholesale prices, order and follow delivery. Give the wholesaler one accountable queue from the original request through preparation, delivery and payment reconciliation.

## Operating Context

French-first interface, Algerian dinars, Africa/Algiers dates, Oran service area. Cash on delivery is the initial assumption, not a confirmed commercial policy. Each store has an address and contact. Requests may contain multiple brands, sizes and ordering units. Product identity and packaging must remain accurate.

## Capabilities and Constraints

- 2,500 real, distinct Algeria-tagged product references, sourced with provenance and matching product images.
- The user has no price list or stock feed. Public references cannot establish this wholesaler's selling price or inventory. Unapproved offers have no published price or stock claim.
- A complete request-for-quotation path must work: request, wholesaler offer with prices and availability, merchant approval, reservation, preparation, dispatch, delivery and collection.
- Approved price-book products may support direct ordering. Server-calculated totals, immutable line snapshots, idempotency and transactional stock reservation protect the workflow.
- Merchant accounts see their own data. An explicitly identified demo uses isolated fictional businesses; production accounts require real authentication and approval.
- Include catalog search, categories, favorites, cart, order guides/reorder, status history, commercial documents, admin analytics, catalog/stock management, audit and export.
- Do not claim native App Store publication, real supplier stock, carrier integration or verified wholesale prices without corresponding evidence.

## Brand Commitments

Retain the real Toushir logo. The user explicitly replaced the initial charter-heavy UI: green, black and white only for the core interface; no yellow, pistachio, acid lime or large dark forest surfaces. Use familiar Inter typography instead of charter display fonts. Merchant is a real React Native mobile ordering app, not an admin platform. Wholesaler is a separate restrained operating workspace. Aim for the clarity and polish of BEES, Sary, Uber and Lime; familiar category conventions are expressly preferred. Avoid visual overload. Cut+Dry, BEES and Sary are reference products for ordering and operating workflows, not assets or trademarks to copy.

## Evidence on Hand

The retained Toushir logo and the user's Charte Toushir PDF; public official competitor documentation; Open Food Facts reference data and individually associated packaging images. No private wholesaler price book, supplier agreement, live inventory, payment credentials, native signing identity or confirmed delivery fleet has been supplied.

## Product Principles

- A merchant should understand the next step without knowing wholesale software.
- One real product reference per barcode; an unknown price stays unknown until confirmed.
- An order is a commercial agreement, not merely a saved cart.
- Delivery, payment collection and accounting reconciliation are distinct events.
- Every operational change has an actor, timestamp and reversible or traceable consequence.

## Open Commercial Decisions

Owner-approved prices and stock, final service communes, delivery fees/cutoffs, return eligibility, tax and legal document configuration, production hosting with durable backups, and native store signing must be confirmed before live commercial launch. The software should support configuring these without rewriting the application.
