# Toushir factual product catalog

This catalog imports product identity from **Open Food Facts contributors**. Data is not a wholesale stock feed. The source database is community maintained, not a manufacturer certification.

- Data: [Open Food Facts](https://world.openfoodfacts.org/), [ODbL 1.0](https://opendatacommons.org/licenses/odbl/1-0/), individual contents [DbCL 1.0](https://opendatacommons.org/licenses/dbcl/1-0/).
- Product photographs: Open Food Facts contributors, [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Photographs can depict trademarks/packaging; no ownership or brand partnership is claimed. No logo has been generated or replaced.
- `sourceUrl` identifies the original product by barcode. `imageSourceUrl` identifies the original selected packaging photograph. `imageBulkSourceUrl` identifies the same selected-front raw photo on the source's bulk-image mirror, when present.
- Product quantities describe the **consumer package** from OFF. They do not establish trade case sizes; `caseSize` remains null until the wholesaler publishes the true case configuration.
- Algeria country tags mean contributors have tagged the product as sold in Algeria. They do not prove Algerian manufacture or current sale, availability, wholesale price, or inventory in Oran.
- All wholesale `price` and `stock` values are null, `pricingMode` is `ON_REQUEST`, and `offerApproved` is false. There are no invented commercial terms. Price publication requires an authorized operator.
- The catalog has no personal customer records and no user credentials.

## Import

`extract_bulk.py` reads the Algeria subset from the official Open Food Facts Hugging Face Parquet snapshot. `build_catalog.py` filters valid GTINs, nonempty product names and brands, front images, nonobsolete records, and unique barcodes. Normalized French categories are an app convenience; source categories remain available. `cache_images.py` stores the exact selected-front 400px crop/revision, strictly serial over one public image connection. The official AWS bulk mirror was tested but contains raw uploads rather than the selected front crop/revision; raw images could have different framing/orientation, so they are not silently substituted. `cache_bulk_images.py` retains the initial bulk-mirror experiment for reproducibility. It checks image decoding and dimensions and records content hashes. It does not certify every package photograph or product claim.

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
