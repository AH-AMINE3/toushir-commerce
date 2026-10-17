# Toushir factual product catalog

This catalog imports product identity from **Open Food Facts contributors**. Data is not a wholesale stock feed. The source database is community maintained, not a manufacturer certification.

- Data: [Open Food Facts](https://world.openfoodfacts.org/), [ODbL 1.0](https://opendatacommons.org/licenses/odbl/1-0/), individual contents [DbCL 1.0](https://opendatacommons.org/licenses/dbcl/1-0/).
- Product photographs: Open Food Facts contributors, [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Photographs can depict trademarks/packaging; no ownership or brand partnership is claimed. No logo has been generated or replaced.
- `sourceUrl` identifies the original product by barcode. `imageSourceUrl` identifies the original selected packaging photograph. `imageBulkSourceUrl` identifies the same selected-front raw photo on the source's bulk-image mirror, when present.
- Product quantities describe the **consumer package** from OFF. They do not establish trade case sizes; `caseSize` remains null until the wholesaler publishes the true case configuration.
- Algeria country tags mean contributors have tagged the product as sold in Algeria. They do not prove Algerian manufacture or current sale, availability, wholesale price, or inventory in Oran.
- All wholesale `price` and `stock` values are null, `pricingMode` is `ON_REQUEST`, and `offerApproved` is false. There are no invented commercial terms. Price publication requires an authorized operator.
- The catalog has no personal customer records and no user credentials.

The completed selection contains **2,500 distinct checksum-valid GTIN references and 2,500 decoded local photographs**. This is an identity/reference catalog with quotation requests, not 2,500 verified wholesale offers. See `final-audit.json` for the independent file, image, source-field and count checks.

`manual-source-holds.json` excludes 93 known source problems, including medicines, tobacco, cosmetics, household cleaners, unrelated accessories, rear/barcode-only pictures and explicit identity/quantity conflicts. Two additional byte-identical photo duplicates are excluded by the selection pass. These checks do not establish that every remaining community record or packaging detail is correct.

Forty-nine records had no brand in OFF but a clearly readable brand on the actual selected source photograph. `reviewed-brand-overrides.json` records the exact transcription, source revision, content hash and review method. Their original `sourceBrand` remains null and `brandOrigin` identifies the observation; manufacturer ownership and affiliations are not inferred. Records with conflicting existing brands are held instead. Raw `sourceName` and `sourceQuantity` are retained. Invalid display quantities and retail prices embedded in titles are suppressed; 856 display quantities remain unknown.

## Import

`extract_bulk.py` reads the Algeria subset from the official Open Food Facts Hugging Face Parquet snapshot. `build_catalog.py` filters valid GTINs, usable source names, source brands or documented visual brand transcriptions, selected front images, nonobsolete records, manual holds, and unique barcodes. Normalized French categories are an app convenience; source categories remain available. Taxonomy tags are matched exactly, so the broad `plant-based-foods-and-beverages` parent does not misclassify pasta, cereals or mushrooms as drinks. `cache_images.py` stores the exact selected-front 400px crop/revision, strictly serial over one public image connection. `finalize_catalog.py` checks photos, removes exact duplicates and refills from genuine qualified candidates. It reports a shortfall if a future source pool cannot supply 2,500 rather than padding the catalog. The official AWS bulk mirror was tested but contains raw uploads rather than the selected front crop/revision; raw images could have different framing/orientation, so they are not silently substituted. `cache_bulk_images.py` retains the initial bulk-mirror experiment for reproducibility. It checks image decoding and dimensions and records content hashes. It does not certify every package photograph or product claim.

Targeted visual review screened 605 generic-category records, 128 low-sharpness records and 64 missing-brand candidate fronts at their respective selection stages; these overlapping groups are not an exhaustive inspection of all 2,500 labels. `photo-audit/` retains a fresh 48-photo sample with presentation notes. Some community photographs remain dim, tightly cropped, rotated, or limited to an identifiable label. Improved owner photographs can replace them after identity checks; canonical source photographs have not been retouched and no AI-generated packaging is published.

`public-price-references.json` is a separate market-research file. These are dated public **retail** references from a third-party retailer, not Toushir wholesale offers. Some retailer SKUs resemble valid GTINs, but barcode checks found source-name inconsistencies and incomplete metadata; none are certified identity matches. They are not imported into offers, quotes, orders, or stock. Public prices can change and tax/transport terms may be unstated. Use them for operator research only.

## Public research sources

- [Official OFF API guidance](https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/): custom User-Agent and rate limits; bulk exports preferred for large imports.
- [Official OFF image download guidance](https://openfoodfacts.github.io/openfoodfacts-server/api/how-to-download-images/).
- [Official OFF bulk image dataset](https://openfoodfacts.github.io/openfoodfacts-server/api/aws-images-dataset/).
- [Official OFF bulk product dataset](https://huggingface.co/datasets/openfoodfacts/product-database).
- [Maghreb Distribution, Oran](https://maghrebdistrib.com/): public grocery distributor identity/catalog, no public price list found. Example Tchina orange apricot 1 L lists a six-bottle case, but does not publish our wholesaler's offer.
- [Ssuq Algeria](https://www.ssuq.pro/): public B2B catalog, prices reserved for professional accounts.
- [Oran Commerce Directorate price survey page](https://www.dcworan.dz/index.php/fr/releve-des-prix/prix-mensuel): public regional reference page, no current GTIN-level stock/wholesale feed verified.
- [Courses Net](https://www.coursesnet.dz/): public individual retail product pages independently checked on 2026-10-16; not a wholesale source for this business.

## Distribution

The extracted factual data and photograph attribution remain available in the app's source/provenance view. Distribute a copy of the extracted catalog under ODbL 1.0, preserve image attribution/source links and the CC BY-SA 3.0 notice, and keep your proprietary offer/stock database separate from the open catalog. Review source corrections before using the catalog for live purchasing or legally required product information.
