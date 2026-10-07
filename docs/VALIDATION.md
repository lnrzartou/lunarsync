# Validation de LunarSync 1.2.0 — 7 octobre 2026

- 39 tests automatisés passent, dont le chargement et la signature avec une clé Windows chiffrée, le refus d’écrasement et le rejet d’un fichier de clé altéré.
- Le canal est configuré pour les releases publiques de lnrzartou/lunarsync, avec une clé publique embarquée et une liste limitée de domaines HTTPS.
- Le script de publication construit, signe, vérifie puis publie les fichiers après téléversement complet d’une release en brouillon.
- La clé privée de production est hors du projet et n’entre pas dans les fichiers distribués.
- Les contrôles en ligne et le parcours complet de mise à jour doivent être relevés après la publication de la release.

## Validation précédente : 1.1.0

## Résultats

- Compilation Release Windows x64 de l’application, de l’installateur et de l’outil de publication : réussie.
- 38 tests automatisés du moteur, des profils, des compteurs et de la sécurité des mises à jour : réussis, aucun échec.
- Application 1.1 lancée depuis l’installation existante du PC de Lunar ; version affichée vérifiée.
- Dix emplacements visibles, passage 01 → 02 et enregistrement du schéma 2 avec exactement dix profils : vérifiés.
- Présentation des nouvelles fiches, bordures lumineuses, pictogrammes et descriptions : vérifiée visuellement.
- Dialogue Autoedit : parcours réel, recommandations 30 CPS et 3 ms, conversion 35 CPS → 28,57 ms : vérifiés dans l’interface.
- Changement provisoire F → G : le schéma devient G → clic gauche. Vérifié, puis annulé sans enregistrer ces valeurs temporaires.
- Démarrage Windows : entrée HKCU Run contrôlée ; elle pointe vers l’exécutable installé avec l’argument --startup.
- Les rafraîchissements du panneau principal et des statistiques sont suspendus lorsque leur interface est masquée. Le moteur et les sauvegardes continuent.

La fenêtre a ensuite été réduite par l’utilisateur ; l’outil de contrôle a signalé « window is minimized » puis une intervention utilisateur. Les essais visuels restants ont été laissés à l’utilisateur. L’onglet Statistiques, le menu de notification, le masquage avec la croix et la réouverture par une seconde instance sont implémentés et compilés, mais leur parcours complet n’a pas été testé visuellement dans cette session.

## Couverture automatisée

Le moteur est testé sur le maintien et le relâchement, l’autorépétition clavier, les maintiens simultanés, les modes indépendants, l’arrêt pendant un maintien, le vrai bouton gauche déjà enfoncé, la perte du clavier, les raccourcis partagés et l’ordre touche puis clic.

Les nouveaux tests couvrent la migration des réglages 1.0 vers le premier des dix profils, leur indépendance, les emplacements invalides, le changement vers un profil vide avec relâchement des entrées, les compteurs exacts d’un maintien, les répétitions, les appuis courts sans clic, les maintiens qui partagent un clic, les erreurs d’envoi et la conservation des totaux après sérialisation. Les frappes ordinaires et l’autorépétition ne gonflent pas les compteurs.

Les tests de sécurité couvrent les signatures valides et falsifiées, les mauvaises clés, l’expiration, les retours de version/séquence, le produit et le canal, HTTPS et les domaines, les fichiers altérés et les chemins ZIP sortant du dossier. La cadence irrégulière est mesurée sur 100 000 périodes.

## Vérifications à poursuivre

- Tester les touches physiques dans les applications prévues ; les tests automatisés du moteur utilisent une sortie simulée, pas une partie de jeu.
- Vérifier après une véritable ouverture de session Windows le lancement discret et la disponibilité de l’icône.
- Parcourir les statistiques avec des compteurs non nuls, puis vérifier la croix, la réouverture et « Quitter LunarSync ».
- Tester installation et désinstallation complètes sur un Windows propre.
- Après choix de l’hébergement : tester une mise à jour signée de bout en bout.
- Avant diffusion publique : certificat d’éditeur Windows et audit indépendant si nécessaire.

La mémoire est celle du processus LunarSync, pas celle du PC entier. Les chiffres d’actions correspondent aux entrées du moteur ; ils ne prouvent pas que le jeu les a acceptées. Les délais Windows et la compatibilité avec les applications ne sont pas garantis en temps réel.
