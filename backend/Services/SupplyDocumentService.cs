using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Private supplier evidence and independently reviewed commercial confirmations.
/// Uploaded evidence is operator-recorded; it is not an authenticated supplier signature.</summary>
public sealed class SupplyDocumentService(WholesaleDb db, OrderService orders)
{
    private static readonly string[] Kinds = ["PURCHASE_ORDER", "SUPPLIER_CONFIRMATION", "RECEIPT", "INVOICE", "CREDIT_NOTE"];
    private static string Hash(object value) => Tokens.Hash(JsonSerializer.Serialize(value));
    private static void Version(int current, int expected) => Check.That(current == expected,
        "Le dossier a changé. Actualisez avant de continuer.", "VERSION_CONFLICT", 409);

    private async Task<SupplyOrder> Purchase(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "FINANCE");
        var order = await db.SupplyOrders.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(order != null, "Achat fournisseur introuvable.", "NOT_FOUND", 404);
        return order;
    }

    private void Event(Actor actor, string orderId, string kind, string referenceId, string details)
    {
        db.SupplyProcurementEvents.Add(new() { WorkspaceId = actor.WorkspaceId, SupplyOrderId = orderId,
            ActorId = actor.Id, Kind = kind, ReferenceId = referenceId, Details = details });
        orders.Audit(actor, kind, orderId, details);
        db.EngagementNotices.Add(new() { WorkspaceId = actor.WorkspaceId, Kind = kind, AggregateId = orderId });
    }

    private static object Metadata(SupplierDocument file) => new
    {
        file.Id, file.SupplyOrderId, file.ReceiptId, file.Kind, file.Reference, file.FileName,
        file.ByteLength, file.Sha256, file.Status, file.SupersedesId, file.CreatedBy,
        file.Version, file.DocumentDate, file.CreatedAt, file.VoidReason, file.VoidedBy, file.VoidedAt,
        contentUrl = $"/api/admin/purchases/{file.SupplyOrderId}/documents/{file.Id}/content"
    };

    private Task<List<SupplyOrderLine>> Lines(Actor actor, string id) => db.SupplyOrderLines
        .Where(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id).ToListAsync();

    private static string TermsHash(SupplyOrder order, IEnumerable<SupplyOrderLine> lines) => Hash(new
    {
        order.SupplierId,
        expectedAt = order.ExpectedAt.ToUniversalTime(),
        items = lines.OrderBy(x => x.ProductId).Select(x => new { x.ProductId, x.OrderedQuantity, x.UnitCostMinor })
    });

    public async Task<object> Dossier(Actor actor, string id)
    {
        // A production dossier must describe one database snapshot, including its evidence state.
        // MutationFilter already supplies the transaction for write responses.
        await using var snapshot = db.Database.IsNpgsql() && db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var result = await DossierCore(actor, id);
        if (snapshot != null) await snapshot.CommitAsync();
        return result;
    }

    private async Task<object> DossierCore(Actor actor, string id)
    {
        var order = await Purchase(actor, id);
        // Content bytes never enter a list/dossier response.
        var documents = await db.SupplierDocuments.AsNoTracking()
            .Where(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id)
            .Select(x => new { x.Id, x.SupplyOrderId, x.ReceiptId, x.Kind, x.Reference, x.FileName,
                x.ByteLength, x.Sha256, x.Status, x.SupersedesId, x.CreatedBy, x.Version,
                x.DocumentDate, x.CreatedAt, x.VoidReason, x.VoidedBy, x.VoidedAt }).ToListAsync();
        var confirmations = await db.SupplierConfirmations.AsNoTracking()
            .Where(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id).ToListAsync();
        var events = await db.SupplyProcurementEvents.AsNoTracking()
            .Where(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id).ToListAsync();
        var receiptIds = await db.SupplyReceipts.Where(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id).Select(x => x.Id).ToListAsync();
        bool Evidence(string kind) => documents.Any(x => x.Kind == kind && x.Status == "ACTIVE");
        var accepted = confirmations.Any(x => x.Status == "ACCEPTED" && documents.Any(d => d.Id == x.DocumentId && d.Status == "ACTIVE"));
        var receiptsDocumented = receiptIds.Count > 0 && receiptIds.All(receipt => documents.Any(d => d.Kind == "RECEIPT" && d.ReceiptId == receipt && d.Status == "ACTIVE"));
        var invoiceDocumented = documents.Any(x => x.Kind == "INVOICE" && x.Status == "ACTIVE" && x.Reference == order.InvoiceReference);
        var financialDocumentIds = documents.Where(x => x.Kind is "INVOICE" or "CREDIT_NOTE").Select(x => x.Id).ToHashSet();
        return new
        {
            orderId = order.Id,
            orderVersion = order.Version,
            orderStatus = order.Status,
            documents = documents.Where(x => actor.Role != "WAREHOUSE" || x.Kind is not ("INVOICE" or "CREDIT_NOTE")).OrderByDescending(x => x.CreatedAt),
            confirmations = confirmations.OrderByDescending(x => x.RecordedAt).Select(x => new
            {
                x.Id, x.DocumentId, x.SupplierReference, x.ExpectedAt, x.Status, x.RecordedBy,
                x.ReviewedBy, x.ReviewReason, x.Version, x.RecordedAt, x.ReviewedAt,
                evidenceOnly = events.Any(e => e.Kind == "SUPPLIER_CONFIRMATION_EVIDENCE_RECORDED" && e.ReferenceId == x.Id),
                items = JsonSerializer.Deserialize<List<SupplierConfirmedLineInput>>(x.ItemsJson)
            }),
            events = events.Where(x => actor.Role != "WAREHOUSE" || !financialDocumentIds.Contains(x.ReferenceId)).OrderByDescending(x => x.At).Take(200),
            evidence = new
            {
                purchaseOrder = Evidence("PURCHASE_ORDER"),
                acceptedSupplierConfirmation = accepted,
                goodsReceipts = receiptIds.Count,
                receiptDocument = receiptsDocumented,
                invoiceDocument = invoiceDocumented,
                amountComparison = order.InvoiceMatchStatus,
                completeDocumentChain = Evidence("PURCHASE_ORDER") && receiptsDocumented && invoiceDocumented
                    && accepted && order.InvoiceMatchStatus == "MATCHED",
                notice = "Pièces enregistrées par votre équipe. Une concordance de montants ne certifie ni l’authenticité des pièces ni leur validité fiscale."
            }
        };
    }

    public async Task<object> Upload(Actor actor, string id, UploadSupplierDocumentInput input)
    {
        var order = await Purchase(actor, id);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d’enregistrement");
        var kind = Check.Text(input.Kind, 40, "Type de pièce").ToUpperInvariant();
        Check.That(Kinds.Contains(kind), "Type de pièce non reconnu.");
        if (kind is "INVOICE" or "CREDIT_NOTE") actor.RequireRoles("ADMIN", "FINANCE");
        var reference = Check.Text(input.Reference, 120, "Référence");
        var fileName = Check.Text(input.FileName, 120, "Nom du fichier");
        Check.That(Regex.IsMatch(fileName, @"^[\p{L}\p{N} _().'-]+\.pdf$", RegexOptions.IgnoreCase),
            "Utilisez un nom de fichier PDF sans chemin ni caractères de contrôle.");
        Check.That(input.ContentBase64 is { Length: > 0 and <= 2800000 }, "Le PDF doit faire au maximum 2 Mo.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(input.ContentBase64); }
        catch (FormatException) { Check.That(false, "Le contenu du fichier est invalide."); throw; }
        Check.That(bytes.Length is > 8 and <= 2097152 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            "Seuls les fichiers PDF de 2 Mo maximum sont acceptés.");
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var hash = Hash(new { id, kind, reference, fileName, sha, documentDate = input.DocumentDate.ToUniversalTime(), input.ReceiptId, input.SupersedesId });
        var old = await db.SupplierDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.IdempotencyKey == key);
        if (old != null)
        {
            Check.That(old.RequestHash == hash, "Cette clé désigne déjà une autre pièce.", "IDEMPOTENCY_CONFLICT", 409);
            return new { document = Metadata(old), orderVersion = order.Version };
        }
        Version(order.Version, input.OrderVersion);
        Check.That(input.DocumentDate > DateTimeOffset.UtcNow.AddYears(-20) && input.DocumentDate < DateTimeOffset.UtcNow.AddYears(1),
            "Vérifiez la date de la pièce.");
        var demo = await db.Workspaces.AnyAsync(x => x.Id == actor.WorkspaceId && x.IsDemo);
        var limit = demo ? 2097152 : 52428800;
        if (demo) Check.That(bytes.Length <= 524288, "En démo, utilisez un PDF de 512 Ko maximum.");
        var occupied = await db.SupplierDocuments.Where(x => x.WorkspaceId == actor.WorkspaceId).SumAsync(x => (long)x.ByteLength);
        Check.That(occupied + bytes.Length <= limit, "L’espace réservé aux pièces est plein. Contactez votre administrateur.", "DOCUMENT_QUOTA", 409);
        var count = await db.SupplierDocuments.CountAsync(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id);
        Check.That(count < (demo ? 12 : 100), "La limite de pièces de ce dossier est atteinte.", "DOCUMENT_QUOTA", 409);
        var receiptId = input.ReceiptId?.Trim() ?? "";
        if (kind == "RECEIPT") Check.That(receiptId.Length > 0, "Associez le document à la réception physique correspondante.");
        if (receiptId.Length > 0)
            Check.That(kind == "RECEIPT" && await db.SupplyReceipts.AnyAsync(x => x.Id == receiptId && x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id), "Réception liée introuvable.");
        if (!string.IsNullOrWhiteSpace(input.SupersedesId))
        {
            var previous = await db.SupplierDocuments.FirstOrDefaultAsync(x => x.Id == input.SupersedesId && x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id);
            Check.That(previous != null && previous.Kind == kind && previous.Status == "ACTIVE", "La pièce à remplacer doit être active dans le même dossier.", "DOCUMENT_CONFLICT", 409);
            Version(previous.Version, input.SupersededVersion ?? -1);
            previous.Status = "SUPERSEDED";
            previous.Version++;
        }
        var document = new SupplierDocument { WorkspaceId = actor.WorkspaceId, SupplyOrderId = id,
            ReceiptId = receiptId, Kind = kind, Reference = reference, DocumentDate = input.DocumentDate.ToUniversalTime(),
            FileName = fileName, Content = bytes, ByteLength = bytes.Length, Sha256 = sha,
            SupersedesId = input.SupersedesId ?? "", CreatedBy = actor.Id, IdempotencyKey = key, RequestHash = hash };
        db.SupplierDocuments.Add(document);
        order.Version++;
        Event(actor, id, "SUPPLIER_DOCUMENT_RECORDED", document.Id, $"{kind} · {reference} · SHA256 {sha}");
        await db.SaveChangesAsync();
        return new { document = Metadata(document), orderVersion = order.Version };
    }

    public async Task<(byte[] Content, string FileName)> Content(Actor actor, string id, string documentId)
    {
        await Purchase(actor, id);
        var document = await db.SupplierDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId && x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id);
        Check.That(document != null, "Pièce introuvable.", "NOT_FOUND", 404);
        if (document.Kind is "INVOICE" or "CREDIT_NOTE") actor.RequireRoles("ADMIN", "FINANCE");
        return (document.Content, document.FileName);
    }

    public async Task<object> Void(Actor actor, string id, string documentId, VoidSupplierDocumentInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        var order = await Purchase(actor, id);
        var document = await db.SupplierDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id);
        Check.That(document != null, "Pièce introuvable.", "NOT_FOUND", 404);
        Version(document.Version, input.Version);
        Check.That(document.Status == "ACTIVE", "Cette pièce n’est plus active.", "INVALID_TRANSITION", 409);
        Check.That(!await db.SupplierConfirmations.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.DocumentId == documentId && x.Status == "ACCEPTED"),
            "Cette pièce étaye une confirmation acceptée. Conservez-la et enregistrez une nouvelle révision.", "DOCUMENT_IN_USE", 409);
        document.VoidReason = Check.Text(input.Reason, 1000, "Motif d’annulation");
        document.Status = "VOIDED"; document.VoidedBy = actor.Id; document.VoidedAt = DateTimeOffset.UtcNow; document.Version++;
        order.Version++;
        Event(actor, id, "SUPPLIER_DOCUMENT_VOIDED", document.Id, document.VoidReason);
        await db.SaveChangesAsync();
        return await Dossier(actor, id);
    }

    public async Task<object> RecordConfirmation(Actor actor, string id, RecordSupplierConfirmationInput input)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE");
        var order = await Purchase(actor, id);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d’enregistrement");
        Check.That(input.Items is { Count: > 0 and <= 100 } && input.Items.All(x => x is not null) && input.Items.Select(x => x.ProductId).Distinct().Count() == input.Items.Count,
            "Saisissez les lignes distinctes confirmées par le fournisseur.");
        Check.That(input.Items.All(x => x.Quantity is >= 0 and <= 1000000 && x.UnitCostMinor is >= 0 and <= 10000000000)
            && input.Items.Any(x => x.Quantity > 0), "Quantités ou coûts invalides.");
        var reference = Check.Text(input.SupplierReference, 120, "Référence fournisseur");
        var hash = Hash(new { id, input.DocumentId, reference, expectedAt = input.ExpectedAt.ToUniversalTime(), items = input.Items.OrderBy(x => x.ProductId) });
        var replay = await db.SupplierConfirmations.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.IdempotencyKey == key);
        if (replay != null) { Check.That(replay.RequestHash == hash, "Cette clé désigne une autre confirmation.", "IDEMPOTENCY_CONFLICT", 409); return await Dossier(actor, id); }
        Version(order.Version, input.OrderVersion);
        Check.That(order.Status is "APPROVED" or "PARTIAL" or "RECEIVED" or "SHORT_CLOSED",
            "L’achat doit avoir été approuvé et ne pas être annulé.", "INVALID_TRANSITION", 409);
        Check.That(await db.SupplierConfirmations.CountAsync(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id) < 50,
            "La limite de confirmations de ce dossier est atteinte.", "DOCUMENT_QUOTA", 409);
        Check.That(input.ExpectedAt > DateTimeOffset.UtcNow.AddYears(-1) && input.ExpectedAt < DateTimeOffset.UtcNow.AddYears(5), "Vérifiez la date attendue.");
        var document = await db.SupplierDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.DocumentId && x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id);
        Check.That(document is { Kind: "SUPPLIER_CONFIRMATION", Status: "ACTIVE" }, "Joignez une confirmation fournisseur active de ce dossier.");
        var lines = await Lines(actor, id);
        Check.That(lines.Count == input.Items.Count && lines.All(x => input.Items.Any(i => i.ProductId == x.ProductId)),
            "La confirmation doit identifier chaque ligne de l’achat, y compris une quantité nulle en cas d’indisponibilité.");
        var evidenceOnly = await db.SupplyReceipts.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id);
        if (evidenceOnly)
            Check.That(SameTerms(order, lines, input.Items, input.ExpectedAt),
                "Après réception, seules les pièces peuvent être complétées. Les quantités, coûts et dates de l’achat restent inchangés.", "INVALID_TRANSITION", 409);
        var confirmation = new SupplierConfirmation { WorkspaceId = actor.WorkspaceId, SupplyOrderId = id,
            DocumentId = document.Id, SupplierReference = reference, ExpectedAt = input.ExpectedAt.ToUniversalTime(),
            ItemsJson = JsonSerializer.Serialize(input.Items.OrderBy(x => x.ProductId).ToList()), OriginalTermsHash = TermsHash(order, lines),
            RecordedBy = actor.Id, IdempotencyKey = key, RequestHash = hash };
        db.SupplierConfirmations.Add(confirmation);
        order.Version++;
        Event(actor, id, evidenceOnly ? "SUPPLIER_CONFIRMATION_EVIDENCE_RECORDED" : "SUPPLIER_CONFIRMATION_RECORDED", confirmation.Id, reference);
        await db.SaveChangesAsync();
        return await Dossier(actor, id);
    }

    public async Task<object> ReviewConfirmation(Actor actor, string id, string confirmationId, ReviewSupplierConfirmationInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        var order = await Purchase(actor, id);
        var confirmation = await db.SupplierConfirmations.FirstOrDefaultAsync(x => x.Id == confirmationId && x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id);
        Check.That(confirmation != null, "Confirmation introuvable.", "NOT_FOUND", 404);
        Version(order.Version, input.OrderVersion); Version(confirmation.Version, input.Version);
        Check.That(confirmation.Status == "RECORDED", "Cette confirmation a déjà été examinée.", "INVALID_TRANSITION", 409);
        Check.That(confirmation.RecordedBy != actor.Id, "Une autre personne habilitée doit examiner la confirmation.", "INDEPENDENT_APPROVAL_REQUIRED", 409);
        var decision = input.Decision?.Trim().ToUpperInvariant();
        Check.That(decision is "ACCEPT" or "REJECT", "Choisissez Accepter ou Rejeter.");
        var reason = Check.Text(input.Reason, 1000, "Motif de la décision");
        if (decision == "ACCEPT")
        {
            Check.That(order.Status is "APPROVED" or "PARTIAL" or "RECEIVED" or "SHORT_CLOSED",
                "Cet achat n’est plus ouvert à une confirmation fournisseur.", "INVALID_TRANSITION", 409);
            Check.That(await db.SupplierDocuments.AnyAsync(x => x.Id == confirmation.DocumentId && x.WorkspaceId == actor.WorkspaceId && x.Status == "ACTIVE"),
                "La pièce justificative n’est plus active. Enregistrez une confirmation avec la pièce actuelle.", "DOCUMENT_CONFLICT", 409);
            var lines = await Lines(actor, id);
            Check.That(TermsHash(order, lines) == confirmation.OriginalTermsHash, "Les termes de l’achat ont changé. Enregistrez un nouvel accord fournisseur.", "VERSION_CONFLICT", 409);
            var items = JsonSerializer.Deserialize<List<SupplierConfirmedLineInput>>(confirmation.ItemsJson)!;
            if (await db.SupplyReceipts.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id))
                Check.That(SameTerms(order, lines, items, confirmation.ExpectedAt),
                    "Une réception a commencé. Les termes ne peuvent plus être réécrits.", "INVALID_TRANSITION", 409);
            foreach (var line in lines) { var item = items.Single(x => x.ProductId == line.ProductId); line.OrderedQuantity = item.Quantity; line.UnitCostMinor = item.UnitCostMinor; }
            order.ExpectedAt = confirmation.ExpectedAt;
            var prior = await db.SupplierConfirmations.Where(x => x.WorkspaceId == actor.WorkspaceId && x.SupplyOrderId == id && x.Status == "ACCEPTED").ToListAsync();
            foreach (var previous in prior) { previous.Status = "SUPERSEDED"; previous.Version++; }
            confirmation.Status = "ACCEPTED";
        }
        else confirmation.Status = "REJECTED";
        confirmation.ReviewedBy = actor.Id; confirmation.ReviewReason = reason; confirmation.ReviewedAt = DateTimeOffset.UtcNow; confirmation.Version++;
        order.Version++;
        Event(actor, id, "SUPPLIER_CONFIRMATION_" + confirmation.Status, confirmation.Id, reason);
        await db.SaveChangesAsync();
        return await Dossier(actor, id);
    }

    private static bool SameTerms(SupplyOrder order, List<SupplyOrderLine> lines, List<SupplierConfirmedLineInput> items, DateTimeOffset expectedAt)
        => order.ExpectedAt == expectedAt && lines.Count == items.Count && lines.All(line => items.Any(item =>
            item.ProductId == line.ProductId && item.Quantity == line.OrderedQuantity && item.UnitCostMinor == line.UnitCostMinor));

    public async Task<object> Close(Actor actor, string id, CloseSupplyOrderInput input, bool remainder)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        var order = await Purchase(actor, id);
        Version(order.Version, input.Version);
        var lines = await Lines(actor, id);
        var received = lines.Sum(x => x.ReceivedQuantity);
        if (remainder)
            Check.That(order.Status == "PARTIAL" && received > 0 && lines.Any(x => x.OrderedQuantity > x.ReceivedQuantity),
                "Seul le reliquat d’un achat partiellement réceptionné peut être clôturé.", "INVALID_TRANSITION", 409);
        else
            Check.That(order.Status is "DRAFT" or "APPROVED" && received == 0,
                "Un achat déjà réceptionné ne peut pas être annulé. Clôturez son reliquat.", "INVALID_TRANSITION", 409);
        if (order.Status != "DRAFT")
            Check.That(order.CreatedBy != actor.Id, "Une autre personne habilitée doit clôturer un achat approuvé.", "INDEPENDENT_APPROVAL_REQUIRED", 409);
        var reason = Check.Text(input.Reason, 1000, "Motif de clôture");
        order.Status = remainder ? "SHORT_CLOSED" : "CANCELLED";
        order.Version++;
        Event(actor, id, remainder ? "SUPPLY_REMAINDER_CLOSED" : "SUPPLY_CANCELLED", id, reason);
        await db.SaveChangesAsync();
        return await Dossier(actor, id);
    }
}
