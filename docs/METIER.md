# Plateforme Toushir de commande en gros

Version 1.0 du 16 octobre 2026 • Périmètre initial Oran • Application commerçant React Native et plateforme grossiste avec API .NET 10

### La décision de fonctionnement

Toushir doit permettre à un commerçant de trouver ses produits, de demander un approvisionnement et de suivre sa commande. Le grossiste reçoit les demandes dans une seule plateforme, confirme ses conditions commerciales, prépare les marchandises, organise la livraison et rapproche les encaissements.

Le catalogue peut être constitué à partir de références publiques réelles. En revanche, aucun tarif de vente, conditionnement de gros ou stock Toushir n’a été fourni. Le parcours initial fonctionne donc sur devis. Un produit sans offre validée affiche « Sur devis » et ne présente aucun prix ni stock inventé.

### Le parcours retenu

Catalogue réel → panier de demande → demande envoyée → devis du grossiste → acceptation du commerçant → commande confirmée et stock réservé → préparation → livraison → rapprochement du paiement.

### Les limites commerciales à respecter

- Un grossiste au démarrage et des zones de livraison configurées autour d’Oran. Aucune couverture nationale ou livraison à une heure garantie n’est déduite du catalogue.

- Dinar algérien et paiement à la livraison comme configuration initiale. Le crédit, le paiement en ligne et les intégrations comptables nécessitent une décision et une connexion réelles.

- Objectif de 2 500 références distinctes et traçables. Le nombre de produits importés, leur preuve de commercialisation en Algérie et la couverture des images doivent être mesurés séparément.

- Une commande passée dans l’application reste visible dans le même historique pour le commerçant, le grossiste et les intervenants autorisés.

### Ce que ce document fixe

Les règles ci-après servent de contrat fonctionnel et de grille de recette. Elles précèdent le code. Les preuves d’exécution, les volumes réellement importés et les éléments restant à connecter doivent être consignés dans un rapport de livraison séparé.

## Les utilisateurs et leurs parcours

La V1 comporte deux rôles et deux espaces : le commerçant et l’administrateur du grossiste. L’application commerçant privilégie le réapprovisionnement. La plateforme grossiste centralise le chiffrage, le stock, la préparation, la livraison et la caisse. Les deux surfaces partagent les mêmes identifiants.

| Rôle | Responsabilité | Accès attendu |
| --- | --- | --- |
| Commerçant | Rechercher, demander, accepter, suivre | Son commerce, ses adresses et ses documents uniquement |
| Administrateur grossiste | Chiffrer, confirmer, préparer, suivre les livraisons et contrôler les encaissements | Données de son grossiste, catalogue et journal des opérations |

### Le commerçant

Il renseigne le nom du commerce, son téléphone, son adresse et sa zone. Il cherche par nom, marque, catégorie ou code-barres, sauvegarde des favoris et constitue une demande. Le panier distingue les produits et leurs quantités demandées sans annoncer de total lorsque les prix sont inconnus. Après réception du devis, il compare le conditionnement, les quantités proposées, les frais, le total et la date de validité avant d’accepter ou de refuser.

Il suit ensuite les étapes de traitement et consulte les quantités réellement reçues, les sommes dues et les paiements enregistrés. Une nouvelle demande depuis une ancienne commande reprend les produits et quantités, puis sollicite les conditions en vigueur.

### Le grossiste

La file « À chiffrer » rassemble les demandes nouvelles. L’équipe confirme les références, conditionnements, quantités disponibles et prix, puis envoie une proposition datée. Une demande incomplète reçoit un motif lisible. Les commandes acceptées passent dans les files de préparation et de livraison. Les retards, ruptures, refus et écarts de caisse restent visibles jusqu’à résolution.

### La relation entre les acteurs

Une note opérationnelle horodatée accompagne la demande ou la commande. Les commentaires internes ne sont jamais publiés au commerçant par défaut. L’ouverture d’un appel ou de WhatsApp ne prouve pas qu’une conversation a eu lieu. Aucun message externe ne part sans intégration et action clairement prévues.

### Les extensions de rôles

Des comptes commerciaux, préparateurs, livreurs et financiers avec affectations et droits limités pourront être ajoutés ensuite. Ils ne font pas partie de la recette V1. Dans cette version, l’administrateur renseigne et contrôle ces opérations depuis son espace, en restant identifié dans le journal.

## Le catalogue et la qualité des données

Une référence produit décrit ce qui est vendu. Une offre décrit ce que le grossiste est réellement disposé à fournir. Cette séparation évite de transformer une donnée publique en engagement commercial.

| Ensemble | Données essentielles | Autorité |
| --- | --- | --- |
| Référence | Identifiant stable, code-barres, nom, marque, quantité nette, catégorie, source, image | Source publique et correction tracée |
| Offre grossiste | Unité vendue, unités par colis, minimum et multiple, prix, disponibilité, statut | Grossiste |
| Stock | Physique, réservé, disponible, quarantaine, mouvements et date de comptage | Entrepôt du grossiste |
| Preuve média | URL source, licence, date de récupération, identifiant de l’image, contrôle | Import et validation humaine |

### L’objectif des 2 500 produits

Le catalogue doit compter des références uniques, pas des copies de la même fiche pour atteindre un chiffre. Une preuve « vendu en Algérie » n’est pas une preuve « fabriqué en Algérie », ni une garantie de disponibilité à Oran. Les produits présentant cette preuve sont identifiés. Un éventuel complément régional ou international reste signalé et n’est pas présenté comme algérien.

Open Food Facts fournit des informations collaboratives de produit et des photos. La base est sous ODbL et les images sous CC BY-SA selon sa documentation officielle. L’import conserve la provenance et les mentions d’attribution applicables. La fiche et les exports permettent de remonter à la source. [S4]

### Les contrôles avant publication

- Rejeter les fiches sans identité exploitable et dédupliquer par code-barres normalisé. Ne jamais fusionner deux formats ou parfums seulement parce que le nom se ressemble.

- Conserver la graphie de la marque. Ne pas générer, redessiner ou remplacer son logo par un logo approximatif. L’image du produit doit correspondre au code-barres et au format de la fiche.

- Vérifier les URLs, le type d’image et le décodage. Une image absente utilise un emplacement neutre qui dit « Photo à vérifier », sans emballage ou marque inventés.

- Séparer les statuts « source présente », « image accessible » et « image vérifiée ». Un téléchargement réussi ne prouve pas l’exactitude visuelle de 2 500 emballages.

- Afficher l’indisponibilité à la commande lorsque l’offre est suspendue. Ne pas faire disparaître les références déjà présentes dans un historique.

### Les prix publics trouvés en ligne

Un prix public peut être conservé comme référence datée, avec vendeur, format et URL. Il ne devient pas le tarif Toushir. Seul un devis ou une offre validée par le grossiste peut engager la commande.

## La demande et le devis

Le parcours sur devis est une capacité opérationnelle complète. Il permet de vendre avec un catalogue réel lorsque le grossiste n’a pas encore chargé son tarif et son inventaire.

| Étape | Action et garde métier |
| --- | --- |
| Brouillon | Le commerçant modifie ses quantités et son adresse. Aucune réserve de stock et aucun montant dû. |
| Demandée | Le serveur crée une demande unique et son numéro. La demande contient les produits et les quantités souhaitées. |
| Chiffrée | Le grossiste indique prix, unité vendue, colisage, quantité proposée, frais et échéance. Une nouvelle version remplace la proposition précédente. |
| Acceptée | Le commerçant accepte la version exacte encore valable. Les conditions sont figées pour la commande. |
| Confirmée | Le stock confirmé est réservé de façon atomique. La commande reçoit ses lignes et ses totaux définitifs. |
| Refusée ou expirée | Le motif ou l’échéance est conservé. Il faut une nouvelle proposition ou demande pour poursuivre. |

### Les règles de chiffrage

Chaque ligne porte une quantité, une unité de vente compréhensible, le nombre d’unités dans le colis et un prix en DZD. La même définition est présentée au commerçant et au préparateur. Exemple de règle de format : « carton de 12 bouteilles de 1 L » ; ce libellé ne doit jamais être déduit d’une quantité nette « 1 L » trouvée dans une source publique.

Le serveur recalcule tous les montants en .NET decimal, avec validation de deux décimales maximum. Les échanges et l’affichage utilisent des DZD ; les calculs monétaires n’utilisent pas de flottants binaires côté serveur. Les remises et frais sont explicites et figés à l’acceptation. La ventilation fiscale dépend du paramétrage validé par le grossiste et sa comptabilité ; aucune taxe n’est inventée par l’import produit.

### La validité et les modifications

Le devis précise sa date limite et la zone ou adresse concernée. L’acceptation d’une version expirée ou modifiée échoue avec une explication et la dernière proposition disponible. Une réduction de quantité, un nouveau conditionnement ou un remplacement exige une nouvelle proposition. Aucune substitution silencieuse n’est permise.

### La réservation et la concurrence

Le disponible est le physique vendable moins les réservations. Deux acceptations concurrentes ne doivent pas réserver la même dernière unité. Si l’équipe ne connaît pas le stock, elle doit le confirmer avant la réservation. Un échec libère l’opération entière et ne crée pas une demi-commande. Le renouvellement d’une requête après coupure réseau retourne le même résultat avec la même clé d’idempotence.

## La préparation et la livraison

Une commande confirmée devient une tâche d’entrepôt. Son exécution conserve les quantités prévues, préparées, expédiées, acceptées, refusées et retournées. Le statut seul ne remplace jamais ces quantités.

| Transition | Responsable et condition |
| --- | --- |
| Confirmée → En préparation | Administrateur identifié ; réservation existante et commande active. |
| En préparation → Prête | Chaque ligne est contrôlée. Une rupture provoque une exception et une décision tracée. |
| Prête → En livraison | Le grossiste confirme le départ réel, les quantités expédiées et le document disponible. |
| En livraison → Livrée | Toutes les quantités expédiées sont acceptées ; réception datée. |
| En livraison → Livraison partielle | Au moins une quantité acceptée et une quantité non acceptée ; motif obligatoire. |
| En livraison → Échec de livraison | Aucune quantité acceptée ; motif et suite à donner explicites. |

### Les annulations

Le commerçant peut demander l’annulation avant la préparation. Le serveur vérifie l’état actuel. Une annulation ou un rejet libère exactement les réservations concernées. Une commande déjà expédiée ne peut pas être annulée comme si les marchandises n’avaient pas quitté l’entrepôt : elle suit la réception, le refus ou le retour.

### La réception et les retours

Dans la V1, l’administrateur enregistre le compte rendu du livreur avec les quantités acceptées et refusées, sans dépasser l’expédié. Chaque refus reçoit un motif. Les produits refusés ne redeviennent pas immédiatement disponibles : le retour physique doit être reçu et contrôlé. Les quantités abîmées restent en quarantaine ; seules les quantités déclarées revendables réintègrent le stock.

Une réclamation après livraison est liée à la commande et aux lignes. L’équipe l’accepte ou la refuse avec motif, enregistre les quantités reçues et crée si nécessaire un avoir ou un remboursement distinct. Un retour ne réécrit pas les ventes et paiements historiques.

### Le paiement à la livraison

Le montant à encaisser est calculé à partir des quantités acceptées et de la règle de frais figée. L’administrateur enregistre la somme réellement reçue et sa référence ; le solde reste visible en cas de paiement partiel. Il rapproche ensuite la remise de caisse après contrôle. Une commande « Livrée » n’est jamais automatiquement « Payée » ou « Rapprochée ».

Les états du paiement sont séparés : non encaissé, partiellement encaissé, encaissé et rapproché. Une correction de caisse ajoute une écriture avec motif et auteur, sans effacer l’écriture d’origine.

## Le modèle de données et les transactions

Les identifiants internes sont immuables. Les libellés affichés restent modifiables dans le catalogue mais sont figés dans les documents commerciaux déjà acceptés.

| Objet | Contenu ou invariant |
| --- | --- |
| Commerce et utilisateur | Compte de commerce, utilisateurs, rôles, zone, adresses et statut d’approbation. |
| Produit et source | Identité, variantes, code-barres, marque, source publique et preuves média. |
| Offre et inventaire | Conditions commerciales autorisées, unité, colisage, mouvements et réservations. |
| Demande et version de devis | Produits demandés, quantités, auteur, proposition datée et échéance. |
| Commande et ligne figée | Version acceptée, prix, unité, quantités, remises, frais et adresse copiés. |
| Préparation et livraison | Quantités exécutées, auteur, événements, motifs et réception. |
| Encaissement et rapprochement | Écritures monétaires, acteur, date, référence, solde et contrôle financier. |
| Retour et avoir | Lignes concernées, quantités, décision, contrôle physique et valeur associée. |
| Événement et audit | Objet, action, acteur, date serveur, motif et avant/après appropriés. |

### Les opérations qui doivent rester atomiques

- Accepter le devis et confirmer les quantités vendables ne produit qu’une seule commande. Toute divergence de version ou de stock annule la transaction.

- Créer la réservation et déduire le disponible se font dans la même transaction. Une répétition de confirmation ne réserve pas une seconde fois.

- Enregistrer une livraison et ses quantités produit un historique cohérent. Le même événement de livraison ne peut pas être compté deux fois.

- Une même clé d’idempotence et une même charge renvoient le résultat initial. La même clé avec des données différentes est refusée.

### Le contrat des échanges

L’API vérifie les droits, la propriété des objets, les transitions, les unités et les montants. Elle répond avec une erreur structurée et lisible pour une donnée invalide, un conflit de version ou une interdiction. Elle ne fait jamais confiance au rôle, au prix ou au commerce transmis librement par l’application.

Les listes sont paginées et filtrables ; le catalogue ne charge pas 2 500 photos en une fois. Les dates sont enregistrées en UTC et affichées selon Africa/Algiers. Les exports utilisent le même périmètre d’accès que l’écran.

## L’expérience commerçant et grossiste

La qualité visuelle doit faciliter les achats répétés et le travail quotidien. Les marques de référence inspirent des principes d’usage ; l’interface et l’identité restent Toushir.

### La direction graphique Toushir

La direction retenue utilise le blanc pour les surfaces, le noir #141816 pour les textes et le vert #087A41 pour les actions et états utiles. Les fonds gris neutres séparent le contenu sans grands blocs sombres. Le jaune, le citron et les tons pistache sont exclus de l’interface. La typographie est Inter ou la police système. Les photographies produits conservent leurs couleurs réelles et le logo Toushir ses proportions d’origine.

La hiérarchie distingue le produit, le format vendu et l’action. Les chiffres monétaires sont alignés et faciles à comparer. Les cartes évitent les grands espaces décoratifs et l’accumulation de badges. Les états d’urgence utilisent du texte et un pictogramme, pas uniquement une couleur.

### Les écrans du commerçant

- Accueil utile : commerce et zone visibles, recherche, catégories, favoris et demandes récentes. Aucun prix promotionnel fictif.

- Catalogue : photo nette, nom complet, marque, quantité nette et « Sur devis » ou offre validée. Ajout rapide avec unité et quantité explicites.

- Demande : panier persistant, contrôle des quantités et de l’adresse, note facultative, confirmation avant envoi.

- Devis : comparaison simple entre demandé et proposé, coût livré, validité, acceptation ou refus sans ambiguïté.

- Commandes : étapes, date de mise à jour, détail des quantités, réception, paiement, réclamation et nouvelle demande.

### Les écrans du grossiste

Le bureau grossiste reste une plateforme distincte de l’application commerçant. Il ouvre sur les demandes à chiffrer et les commandes à traiter. Les tables proposent recherche, filtres et une action principale par ligne. Le détail garde le contexte pendant le chiffrage ou la préparation. Les tableaux de bord séparent activité commerciale, exécution et caisse.

### La qualité d’usage à vérifier

L’application doit fonctionner au tactile et au clavier sur le web, annoncer les contrôles aux lecteurs d’écran, préserver le focus et afficher les erreurs près du champ concerné. Les écrans étroits ne coupent ni le total ni l’action principale. Les listes ont des états de chargement, d’absence de résultat et de perte réseau. Une coupure ne transforme jamais une demande enregistrée en commande dupliquée.

## Le pilotage et les droits

Un indicateur doit préciser son périmètre et son événement de référence. Le grossiste doit pouvoir distinguer ce qui a été demandé, accepté, livré et réellement encaissé.

| Indicateur | Définition métier |
| --- | --- |
| Demandes nouvelles | Demandes créées dans la période, hors brouillons. |
| Délai de devis | Temps entre demande reçue et première proposition envoyée. |
| Acceptation des devis | Versions acceptées rapportées aux propositions envoyées selon la période définie. |
| Valeur confirmée | Montant des commandes confirmées, annulations présentées séparément. |
| Valeur livrée | Valeur des quantités acceptées et frais applicables ; différente du cash reçu. |
| Service des lignes | Quantités acceptées comparées aux quantités confirmées, par unité identique. |
| Encaissement et solde | Montants déclarés reçus, rapprochés et restant dus ; périodes explicites. |
| Réachat et assortiment | Commerces ayant recommandé, références commandées et produits sans demande. |
| Stock et exceptions | Disponible actuel, réservations, ruptures et quarantaine ; photographie actuelle. |

### Les règles d’accès

Un commerçant ne peut ni ouvrir une commande d’un autre commerce en modifiant son URL, ni exporter les données d’un tiers. Il ne peut pas chiffrer, modifier une offre, saisir un stock ou rapprocher un paiement. L’administrateur agit uniquement dans l’espace de son grossiste. Ses décisions de prix, de stock, de livraison et de caisse restent tracées.

Les mots de passe sont hachés ; les sessions expirent et peuvent être révoquées. Les accès d’essai ne doivent pas ouvrir la base réelle. Les secrets restent hors du dépôt et du navigateur. Les interfaces vérifient les droits mais la décision finale appartient toujours au serveur.

### Le journal et la récupération

Les changements de devis, stock, statut, livraison, paiement, rôle et retour sont horodatés avec l’acteur. Le journal ne contient pas de mot de passe ou de secret. Les suppressions de fiches commerciales sont remplacées par une désactivation lorsqu’un historique les référence.

La sauvegarde comprend les données et les médias métier nécessaires. Une restauration sur une base distincte doit être testée avant le lancement. Le redémarrage du service ne doit pas faire disparaître commandes, comptes et écritures. Les erreurs de service sont surveillées sans publier les données personnelles dans les journaux.

## La recette du parcours de commande

Chaque critère est vérifié sur la base persistante et par l’API, puis sur les écrans. Une capture prouve un affichage ; elle ne remplace pas la vérification du résultat métier.

| ID | Scénario | Résultat attendu |
| --- | --- | --- |
| R01 | Ouvrir un produit sans offre | « Sur devis », aucun prix ni stock inventés ; provenance accessible. |
| R02 | Rechercher nom, marque et code-barres | Références pertinentes ; formats distincts conservés ; pagination. |
| R03 | Revenir après fermeture de l’application | Panier de demande conservé pour le bon utilisateur. |
| R04 | Envoyer deux fois la même demande | Une seule demande et un seul numéro avec la même clé. |
| R05 | Modifier la charge avec la même clé | Conflit explicite ; la demande initiale reste intacte. |
| R06 | Établir un devis complet | Unité, colisage, quantités, prix, frais et validité visibles. |
| R07 | Accepter un devis expiré ou révisé | Refus serveur ; dernière version présentée sans acceptation implicite. |
| R08 | Altérer un prix dans la requête client | Le serveur utilise la version du devis ; aucune remise frauduleuse. |
| R09 | Deux achats de la dernière quantité | Une seule réservation possible ; stock jamais négatif. |
| R10 | Rejouer une acceptation après coupure | Même commande retournée ; aucune double réservation. |
| R11 | Demander une quantité hors multiple | Erreur claire sur le conditionnement, sans arrondi silencieux. |
| R12 | Annuler avant préparation | Transition autorisée et réservation libérée exactement une fois. |
| R13 | Annuler après expédition | Refus de l’annulation simple ; traitement par réception ou retour. |
| R14 | Recommander une ancienne commande | Nouvelle demande ; aucune réutilisation silencieuse d’un ancien prix. |

### La règle de preuve

Le rapport de recette doit indiquer la version testée, l’environnement, la date et les résultats. Les données créées pour les tests portent des identifiants reconnaissables et restent distinctes de l’exploitation réelle. Les échecs sont consignés avec leur conséquence pratique ; ils ne sont pas masqués par une belle maquette.

## La recette opérationnelle et le lancement

| ID | Scénario | Résultat attendu |
| --- | --- | --- |
| R15 | Préparer et expédier une commande | Quantités contrôlées et administrateur identifié ; historique complet. |
| R16 | Recevoir partiellement une ligne | Accepté + refusé ne dépasse pas expédié ; motif obligatoire. |
| R17 | Déclarer un refus de livraison | Pas de retour automatique au disponible ; quarantaine contrôlée. |
| R18 | Marquer une commande livrée | Aucun paiement créé automatiquement. |
| R19 | Encaisser partiellement puis rapprocher | Solde exact, écritures distinctes et autorisation financière. |
| R20 | Rejouer une réception ou un paiement | Une seule opération ; pas de double valeur ou mouvement. |
| R21 | Créer une réclamation et un retour | Ligne liée, décision, réception physique et avoir traçables. |
| R22 | Accéder au commerce d’un autre compte | Refus pour détail, mutation et export ; aucune fuite de données. |
| R23 | Agir avec un rôle insuffisant | Refus serveur même si le contrôle de l’écran est contourné. |
| R24 | Redémarrer et restaurer une sauvegarde | Comptes, commandes et écritures conservés ; restauration vérifiée. |
| R25 | Tester mobile, clavier et réseau faible | Actions lisibles, focus cohérent, erreurs utiles et absence de doublon. |
| R26 | Contrôler le catalogue importé | Compte unique, origine, licence et couverture des images mesurés. |

### Les conditions du premier usage réel

Avant de recevoir les commandes de commerçants réels, le grossiste valide son identité commerciale, ses zones autour d’Oran, les frais, les délais annoncés, les règles de devis et les personnes habilitées. Le service sur devis permet de commencer sans liste tarifaire globale, mais chaque proposition envoyée doit contenir de vraies conditions et un stock confirmé.

L’hébergement doit fournir HTTPS, une base persistante, des sauvegardes, une restauration testée et des comptes nominatifs. Les coordonnées de contact, les documents commerciaux et les informations de confidentialité sont validés pour l’entreprise concernée. L’application mobile nécessite une configuration de signature et de distribution réelle ; un aperçu web ne constitue pas une publication sur les stores.

Les connexions de paiement, SMS, WhatsApp, transporteur ou comptabilité sont considérées actives uniquement après configuration de leurs comptes, autorisations et tests réels. En leur absence, l’interface propose un suivi manuel explicite.

## Les références et les décisions de produit

Recherche consultée le 16 octobre 2026. Les descriptions suivantes concernent les principes de parcours publiés par les éditeurs. Elles ne constituent ni une analyse de leur code privé, ni une promesse de performance pour Toushir.

### Les enseignements utiles

Cut+Dry présente la commande auprès de distributeurs habituels, les listes d’achat, les brouillons, la recommandation et le contrôle à réception. Son centre d’aide distingue aussi plusieurs unités de commande. Toushir en retient une recherche claire, un réapprovisionnement rapide et des unités explicites. [S1, S2]

BEES publie un parcours allant de l’envoi de commande à la livraison, avec des états d’annulation et de refus. Toushir en retient un suivi lisible et des transitions contrôlées. Le chiffrage et le paiement restent des objets distincts dans le métier Toushir. [S3]

Sary présente un approvisionnement B2B regroupant les catégories et les marques avec suivi de livraison. Toushir en retient la navigation adaptée au commerce et la centralisation des demandes, sans reprendre une promesse de délai ou de financement. [S5]

### Les sources officielles

S1 Cut+Dry pour les commerçants
https://cutanddry.com/restaurants/

S2 Cut+Dry centre aide commande
https://help.cutanddry.com/en_US/order

S3 BEES statuts de commande
https://support.mybees.com/hc/es-us/articles/4411269072532-How-to-check-the-status-of-an-order

S4 Open Food Facts licences de réutilisation
https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/tutorials/license-be-on-the-legal-side/

S5 Sary site officiel
https://www.sary.com/

### Les décisions de ce périmètre

Oran est le territoire initial. Le propriétaire n’a pas de liste de prix ou de stock à importer. Le catalogue public sert donc de référentiel et le devis sert d’engagement commercial. Les tarifs, stocks et marques ne sont pas synthétisés pour donner l’apparence d’un magasin déjà approvisionné.

Les produits absents d’une source fiable ne sont pas inventés. La commande au tarif direct repose sur des offres publiées et un inventaire tenu à jour. Les rôles spécialisés avec affectations, le crédit, le paiement en ligne, les livraisons réfrigérées et le suivi des lots ou dates limites sont des extensions à définir avant activation.
