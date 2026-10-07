# Publier les mises à jour de LunarSync

Le dépôt public est [lnrzartou/lunarsync](https://github.com/lnrzartou/lunarsync). Le service de mise à jour utilise des fichiers signés dans **GitHub Releases** : aucun serveur, port entrant ou API d’administration n’est installé sur le PC de Lunar.

## Canal de mise à jour

- URL stable : https://github.com/lnrzartou/lunarsync/releases/latest/download/release.json
- Paquets : URL versionnée dans chaque release.
- Domaines autorisés : github.com et release-assets.githubusercontent.com.
- Confiance : clé publique ECDSA P-256 incluse dans src/LunarSync/update-channel.json.
- Le manifeste contient un payload signé : produit, canal, version, séquence croissante, date de publication, expiration, taille, SHA-256, URL et notes.
- Le client refuse les signatures invalides, les anciennes versions, les métadonnées expirées et les téléchargements altérés.
- Les utilisateurs ne reçoivent aucun jeton GitHub ni clé privée.

LunarSync vérifie au lancement puis toutes les six heures. Une nouvelle version produit une notification et un bouton « Mettre à jour et redémarrer ». Les macros sont arrêtées avant le remplacement ; les configurations et statistiques sont conservées hors du dossier de l’application.

## Où est la clé privée ?

Sur le PC de publication uniquement :

```text
%LOCALAPPDATA%\LunarSyncPublisher\lunarsync-private.dpapi
%LOCALAPPDATA%\LunarSyncPublisher\lunarsync-public.pem
```

La première clé est chiffrée avec DPAPI pour le **compte Windows courant**. Elle n’est pas dans le dépôt, le ZIP ou l’installateur. Le fichier public peut être distribué. Ne crée pas une nouvelle clé pour une publication ordinaire : les clients font confiance à celle qu’ils possèdent déjà.

DPAPI évite de conserver la clé privée en clair ou une phrase secrète dans un script. Il ne protège pas d’un logiciel malveillant exécuté avec les mêmes droits Windows. Protéger le compte GitHub avec une authentification forte reste nécessaire.

## Sauvegarde de la clé

Une copie du fichier DPAPI ne suffit pas pour garantir une restauration sur un autre PC. Faire une sauvegarde chiffrée indépendante sur un support privé :

```powershell
.\tools\publisher\LunarSync.Release.exe export-windows `
  "$env:LOCALAPPDATA\LunarSyncPublisher\lunarsync-private.dpapi" `
  "E:\Lunar-prive\lunarsync-backup-private.pem"
```

L’outil demande une phrase secrète d’au moins 16 caractères sans l’afficher, puis sa confirmation. Il ne remplace jamais un fichier existant. Le PEM est chiffré avec AES-256 et PBKDF2-HMAC-SHA256 (600 000 itérations). Garder cette sauvegarde et sa phrase secrète séparément, hors GitHub. Une sauvegarde indépendante n’est pas créée automatiquement.

## Publier la version suivante

Prérequis : Git, GitHub CLI connecté à lnrzartou, SDK .NET 10, et la clé DPAPI sur le compte Windows de publication.

1. Modifier le code ou le catalogue des macros.
2. Augmenter Version dans Directory.Build.props, par exemple 1.3.0.
3. Écrire les notes dans docs/releases/1.3.0.md.
4. Enregistrer les modifications et pousser le commit sur main.
5. Depuis la racine du dépôt :

```powershell
.\scripts\Publish-GitHub.ps1 -Version 1.3.0
```

Le script teste, compile, contrôle la publication précédente, signe le nouveau paquet, vérifie sa signature et son SHA-256, prépare une release en brouillon et téléverse les fichiers. Il rend la release publique **seulement après** l’envoi de tous les fichiers. Il refuse les fichiers privés suivis par Git, une version déjà utilisée ou un commit local différent de main sur GitHub.

La publication réutilise la même clé. Les utilisateurs n’ont qu’à accepter la mise à jour dans LunarSync. Une erreur de téléversement laisse éventuellement un brouillon à terminer ; ne pas publier un brouillon incomplet ni remplacer un ZIP déjà distribué.

Le paramètre -Dotnet accepte le chemin d’un SDK qui n’est pas dans le PATH. Le paramètre -SigningKey accepte une autre localisation de la même clé DPAPI.

## Expiration et renouvellement

Un manifeste est valable **45 jours**. Après expiration, les applications déjà installées restent utilisables, mais la vérification indique un problème de fraîcheur jusqu’à la publication d’un nouveau manifeste valide. Toute nouvelle version publiée par le script reçoit de nouvelles dates et une séquence supérieure.

Si aucune évolution n’est prévue avant 45 jours, publier une version de maintenance, avec un nouveau numéro, en suivant la procédure ci-dessus. Le script sait vérifier la signature historique d’un ancien manifeste expiré avant d’avancer la séquence ; **le client continue de refuser un manifeste expiré**.

## Vérifications

```powershell
.\tools\publisher\LunarSync.Release.exe verify `
  .\src\LunarSync\update-channel.json `
  .\distribution\release.json `
  1.2.0 `
  .\distribution\LunarSync-1.2.0-win-x64.zip
```

La sortie confirme la signature, la version, la séquence, la taille et le SHA-256. Les tests incluent la corruption de signatures, les retours de version, les URL non autorisées, les archives qui sortent du dossier et l’altération de la clé chiffrée.

Le remplacement complet doit aussi être essayé sur une machine de test à partir de la version précédente. Les anciennes versions dont le canal était désactivé nécessitent l’installation initiale de confiance de la version 1.2.

## Limites

La signature de mise à jour et la signature d’éditeur Windows sont distinctes. L’installateur n’a pas encore de certificat Authenticode. Ne pas désactiver l’antivirus. Aucun mécanisme ne garantit un PC inviolable ou un code sans bug.

La sécurité dépend de la protection de la clé privée et du poste qui construit le logiciel. En cas de compromission de cette clé, suspendre la publication et organiser une nouvelle distribution de confiance. La rotation multi-clés de type TUF n’est pas implémentée. La signature ne protège pas une première installation téléchargée depuis un compte compromis : une signature d’éditeur Windows est à prévoir pour une diffusion publique plus large.

## Références

- [GitHub : lien vers le fichier de la dernière release](https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases)
- [GitHub : API des fichiers de release](https://docs.github.com/en/rest/releases/assets)
- [Microsoft : protection DPAPI](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata)
