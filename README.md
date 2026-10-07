# LunarSync

<p><img src="assets/lunarsync-logo.png" alt="Logo LunarSync" width="100"></p>

**[Télécharger l’installateur Windows 1.2](https://github.com/lnrzartou/lunarsync/releases/download/v1.2.0/LunarSync-Setup-1.2.0-win-x64.exe)** · **[Dernière version](https://github.com/lnrzartou/lunarsync/releases/latest)**

Un panneau de macros préparées par Lunar : dix configurations, aperçu du clavier, statistiques et suivi sur un deuxième écran.

Application Windows de macros prédéfinies, créée pour Lunar. Version 1.2.0.

## Utiliser et partager

Le fichier à envoyer aux amis est `distribution/LunarSync-Setup-1.2.0-win-x64.exe`. Il contient le logiciel et son environnement .NET : aucun AutoHotkey ni SDK n’est nécessaire chez eux. Cible de cette version : Windows 10/11 sur processeur x64. Une compilation ARM64 est prévue par le script de construction, mais cette livraison x64 n’a pas été testée sur ARM64.

L’installation se fait pour l’utilisateur courant, dans `%LOCALAPPDATA%\Programs\LunarSync`, avec un raccourci dans le menu Démarrer et, au choix, sur le Bureau. L’option « Lancer LunarSync avec Windows » est cochée par défaut dans l’installateur et peut être modifiée dans Système. L’application démarre discrètement dans la zone de notification, avec les modes désactivés. Aucun service ni modification du firmware clavier. La désinstallation retire aussi le démarrage automatique ; les préférences et compteurs sont conservés.

Le ZIP `distribution/LunarSync-1.2.0-win-x64.zip` est la version portable et le paquet utilisé pour préparer les mises à jour. Il faut extraire tout le ZIP avant de lancer `LunarSync.exe`. Les mises à jour automatiques s’appliquent aux installations faites par l’installateur.

## Premiers réglages

1. Ouvrir l’onglet **Clavier**. Un clavier unique est sélectionné automatiquement. Si plusieurs périphériques sont reconnus, il faut en sélectionner un.
2. Ajuster l’aperçu 60 %, 65 %, TKL ou complet et AZERTY/QWERTY si nécessaire. Windows ne donne pas toujours le nom commercial ni le format exact ; le dessin est schématique. Le choix du clavier sert à l’aperçu, les raccourcis sont globaux dans Windows.
3. Dans **Macros**, choisir l’une des **dix configurations**, puis ouvrir **Configurer** et enregistrer les préréglages voulus. Ils restent non configurés tant qu’ils ne sont pas enregistrés. Tous les modes sont désactivés au lancement.
4. Appuyer une fois sur le raccourci pour activer le mode, une seconde fois pour l’arrêter. Plusieurs macros peuvent partager le même raccourci et s’activer ensemble.
5. Le **Panneau en jeu** ouvre une fenêtre séparée, déplaçable sur un second écran, avec l’état en direct de chaque macro.

**Ctrl + Alt + F12** ou **Tout désactiver** coupe tous les modes. Le verrouillage de session et **Quitter LunarSync** arrêtent aussi les macros. La croix ferme seulement la fenêtre : le moteur reste actif en arrière-plan. Double-cliquer sur son icône près de l’horloge ou relancer le raccourci retrouve l’instance existante. Fermer l’ancien script AutoHotkey avant d’activer les mêmes raccourcis dans LunarSync, pour éviter les doublons.

## Dix configurations fixes

Les emplacements 01 à 10 existent toujours : aucun ajout n’est possible. Chacun possède ses macros, touches, cadences et délais. Le sélecteur est commun aux onglets Macros et Clavier ; le panneau en jeu affiche aussi la configuration courante. Tout changement coupe les modes et relâche les entrées synthétiques en cours. Une configuration vide n’exécute ni n’intercepte aucun raccourci de macro.

Les réglages de la version 1.0 sont migrés dans la configuration 01. Les neuf autres commencent vides. Les boutons **Vider** et **Retirer de cette configuration** permettent de retrouver cet état. Les macros du catalogue restent visibles pour pouvoir les configurer.

## Statistiques

Les compteurs sont cumulatifs, par configuration ou toutes configurations confondues : presses par touche/bouton, clics gauches envoyés et utilisations par macro. Un appui sur F dans Drag macro compte +1 F puis +1 clic gauche envoyé ; le relâchement ne compte pas. Les frappes sans macro active, les raccourcis d’activation et l’autorépétition du clavier sont exclus. Deux maintiens qui partagent déjà le clic ne créent pas de clic supplémentaire.

Une utilisation correspond à un maintien déclenché ou à un cycle de répétition terminé. Les compteurs mesurent les entrées envoyées par le moteur, pas les actions reconnues par le jeu. Ils sont sauvegardés toutes les 30 secondes et à la sortie : un arrêt brutal peut perdre les dernières secondes.

La RAM affichée est la mémoire physique du processus LunarSync (moteur compris), en Mo. Le graphique conserve les 60 derniers échantillons de l’onglet ; son rafraîchissement s’arrête lorsque la fenêtre est masquée. Ce n’est pas la mémoire totale du PC.

## Catalogue

| Préréglage | Touche de base | Activation par défaut | Fonction |
|---|---|---|---|
| Autoedit / Edit | F | Page ↑ | Touche puis clic gauche, 30 cycles/s |
| Ramassage automatique | E | Page ↓ | 35 appuis/s |
| Autobuild mur | Souris 4 | ↓ | Maintien du clic gauche |
| Autobuild escalier | Souris 5 | ↓ | Maintien du clic gauche |
| Autobuild sol | X | ↓ | Maintien du clic gauche |
| Autobuild cône | C | ↓ | Maintien du clic gauche |
| Drag macro | F | ↑ | Maintien du clic gauche |

Le bouton ou la touche d’origine garde son fonctionnement dans les modes de maintien. Le clic gauche se relâche quand le dernier déclencheur actif est relâché. Un maintien physique du vrai bouton gauche est respecté. Les touches d’activation sont capturées par LunarSync lorsqu’une macro configurée leur est attribuée.

Les noms de construction supposent les associations correspondantes dans le jeu. Les macros d’édition envoient une touche et un clic ; la confirmation automatique dépend du réglage de confirmation au relâchement dans le jeu. Le parcours exact s’affiche dans chaque configuration et s’adapte à la touche choisie. Les recommandations sont 30 CPS pour Autoedit, 35 appuis/s pour Ramassage, des appuis et pauses de 3 ms pour les répétitions et un délai de 3 ms pour les maintiens. Ce sont des points de départ, pas une mesure garantie pour tous les jeux. L’avertissement du ramassage parle de règles de Fortnite, pas d’une interdiction légale.

Les utilisateurs peuvent changer la touche de base, le raccourci, les délais et la cadence des répétitions. Ils ne disposent d’aucun éditeur de scénario ni de script. Les nouveaux préréglages sont publiés par Lunar dans une nouvelle version du catalogue.

La cadence réglable va de 1 à 100 cycles/s, sous réserve que les délais choisis tiennent dans la période. L’option irrégulière modifie les intervalles autour de la période moyenne, avec une variation de 1 à 40 %. Par exemple, 35 CPS correspond à environ 28,57 ms par cycle. Les CPS sont une cible, pas une garantie de temps réel de Windows ou du jeu. Pour une macro touche + clic, un cycle comprend les deux actions.

## À quoi servait AutoHotkey ?

[AutoHotkey](https://github.com/AutoHotkey/AutoHotkey) est un outil d’automatisation Windows qui exécute des scripts et raccourcis. Il servait aux premiers prototypes. LunarSync intègre maintenant son propre moteur natif et ne nécessite pas AutoHotkey. L’onglet Système explique cette différence.

## Mises à jour et publication

Le canal public utilise **GitHub Releases**. L’application lit [le manifeste signé de la dernière version](https://github.com/lnrzartou/lunarsync/releases/latest/download/release.json), vérifie la clé publique intégrée puis télécharge le paquet de cette version. Aucun serveur n’est lancé sur le PC. La clé privée reste hors du dépôt, protégée par le compte Windows de Lunar. Voir [le guide de publication](docs/PUBLICATION-SECURISEE.md).

Le logiciel vérifie au démarrage, y compris en arrière-plan, puis toutes les six heures. Une version disponible est annoncée dans l’onglet Mises à jour et par une notification Windows. Le bouton **Mettre à jour et redémarrer** télécharge, vérifie, arrête les macros et relance la nouvelle version.

L’installateur initial n’a pas de certificat de signature de code Windows. La signature cryptographique des mises à jour et la signature de code de l’exécutable sont deux mécanismes différents. Pour une diffusion publique, prévoir un certificat de signature de code ou une distribution via une plateforme appropriée. Ne pas demander aux utilisateurs de désactiver leur antivirus.

Pour les versions 1.0 et 1.1 distribuées avec le canal désactivé, installer une fois la version 1.2 pour activer le canal de confiance. Les mises à jour suivantes se font dans l’application.

## Sources et construction

Prérequis de compilation : SDK .NET 10 pour Windows, PowerShell. Aucun paquet tiers n’est nécessaire au moteur de macros ou à la vérification des signatures.

```powershell
.\build.ps1 -Version 1.2.0 -Runtime win-x64
```

Le script exécute les tests, publie l’application autonome, prépare le ZIP, produit l’installateur et l’outil de signature `tools/publisher/LunarSync.Release.exe`.

- `src/LunarSync.Core` : moteur, configuration, intégrité des mises à jour.
- `src/LunarSync` : interface, périphériques, entrées Windows, panneau et mise à jour.
- `src/LunarSync.Installer` : installateur par utilisateur.
- `src/LunarSync.Release` : outil privé de publication pour Lunar.
- `tests/LunarSync.Tests` : tests déterministes des entrées et de la sécurité.
- `assets` : logo original PNG transparent et icône Windows.

Les préférences sont enregistrées dans `%LOCALAPPDATA%\LunarSync\settings.json`. Les compteurs agrégés sont enregistrés à côté dans `statistics.json`. Aucun texte saisi, ordre de frappe ni historique de navigation n’est conservé ou envoyé. L’application n’ouvre pas de serveur réseau ; ses requêtes réseau concernent uniquement le canal HTTPS de mise à jour configuré.
