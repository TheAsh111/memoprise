# MémoPrise — Windows

MémoPrise est une application gratuite pour Windows 10 et 11 qui aide à organiser les prises de médicaments. Elle propose des rappels aux heures prévues, un suivi des prises validées et un calendrier pour consulter l’historique. Les horaires et les quantités peuvent rester fixes ou évoluer par périodes.

Les données restent sur votre ordinateur : aucun compte ni connexion Internet ne sont nécessaires pour utiliser le programme.

**[Télécharger l’installateur MémoPrise 1.4.1](https://github.com/TheAsh111/memoprise/raw/refs/heads/main/distribution/Installer-MemoPrise.exe)**

Après le téléchargement, lancez `Installer-MemoPrise.exe`. L’application ne nécessite ni installation séparée de .NET ni droits administrateur. L’exécutable n’est pas signé numériquement.

Application C# / WPF (.NET 8), données locales SQLite. Windows 10/11 x64.

## Installer

Double-cliquez sur `distribution/Installer-MemoPrise.exe`. Aucune installation de .NET ni droit administrateur ne sont nécessaires. L'installation ajoute un raccourci sur le Bureau et dans le menu Démarrer. Le lancement avec Windows est activé par défaut, désactivable dans Paramètres.

Une version portable est également disponible : `distribution/MemoPrise/MemoPrise.exe`. Avant de lancer la mise à jour, utilisez **Quitter l’application** dans l’ancienne version (fermer sa fenêtre laisse les rappels actifs). Les traitements et l’historique existants sont conservés.

## Utiliser

1. Dans **Boîte à pharmacie**, cliquez sur **Ajouter un médicament**.
2. L’assistant demande le nom et le nombre de prises par jour. Pour chaque prise, choisissez l’heure avec les rouleaux (flèches, molette ou touches haut/bas), puis sa quantité. Exemple : **1 cachet à 12:00**, puis **2 cachets à 20:00** pour le même médicament. Choisissez ensuite les jours et les dates, vérifiez le récapitulatif, puis enregistrez. Le bouton Précédent permet de corriger une étape sans perdre la saisie.
3. Dans **Aujourd’hui**, validez chaque prise avec **Valider la prise**. Une validation erronée peut être annulée.
4. À l'heure prévue, le rappel apparaît au-dessus des fenêtres, avec un son désactivable. Les rappels du même moment sont regroupés, avec validation individuelle. Le report est de dix minutes. Fermer un rappel sans valider n'enregistre aucune prise ; il réapparaît au prochain rappel, environ dix minutes plus tard.
5. Dans **Calendrier**, cliquez sur une date pour voir les prises et l'heure de validation. Une validation faite après coup indique l'heure réelle de validation.
6. Le bouton **Réduire l’application** minimise la fenêtre dans la barre des tâches et conserve les rappels. Fermer la fenêtre principale conserve aussi les rappels. L'icône près de l'horloge permet de rouvrir l'application. **Quitter l’application** arrête les rappels.
7. Dans **Paramètres**, agrandissez les textes ou sauvegardez vos données.

Les nouveaux traitements et modifications s'appliquent à partir de leur enregistrement. Aucun historique antérieur n'est inventé. Les anciennes prises conservent leur nom, quantité, horaire et état. Suspendre arrête les nouveaux rappels du traitement ; l'historique reste consultable.

## Posologie fixe ou par périodes (version 1.2)

L’assistant demande **La posologie reste-t-elle la même ?** Choisissez une posologie fixe, ou **Elle change à une ou plusieurs dates**. Dans ce dernier cas, les premières prises correspondent à la période 1. Indiquez sa durée en jours : la période 2 commence automatiquement le lendemain. Vous pouvez aussi choisir sa date de début, ce qui recalcule la durée précédente.

Exemple : **1 cachet à 08:00 et 2 cachets à 20:00 pendant 14 jours**, puis **2 cachets à 08:00 et 2 cachets à 20:00**. Les durées comptent des jours calendaires, du premier au dernier jour inclus. Les jours de semaine cochés s’appliquent à toutes les périodes.

Chaque nouvelle période peut changer les quantités, horaires et le nombre de prises par jour. **Ajouter une autre période** permet de prévoir plusieurs changements. Seule la dernière période peut être sans date de fin. Les dates sont contiguës, sans chevauchement. Le récapitulatif affiche toutes les dates et quantités avant l’enregistrement. Les rappels et le calendrier utilisent automatiquement la période applicable à la date concernée.

Un ordinateur éteint, en veille ou verrouillé ne permet pas une interaction normale avec le rappel. Les prises du jour non confirmées sont signalées au retour de la session ; les jours précédents restent dans l'historique. Les rappels ne constituent pas une confirmation de prise.

## Données et sauvegardes

Base : `%LOCALAPPDATA%\MemoPrise\memoprise.db`. Aucun compte, aucun service Internet, aucune synchronisation. Les sauvegardes JSON contiennent les traitements et l'historique en clair. Avant une restauration, une sauvegarde de sécurité est créée dans le même dossier que la base.

Pour désinstaller : désactiver le démarrage automatique, quitter l'application, supprimer `%LOCALAPPDATA%\Programs\MemoPrise` et les raccourcis. Les données restent dans `%LOCALAPPDATA%\MemoPrise` pour une réinstallation ; supprimer ce dossier seulement pour effacer l'historique.

## Développement et vérification

- `dotnet build MemoPrise/MemoPrise.csproj -c Release`
- `dotnet run --project tests/MemoPrise.Tests.csproj -c Release`
- `./Build.ps1` : vérifications, compilation autonome et construction de l’installateur dans les dossiers principaux, sans copie par version.
- `dotnet publish MemoPrise/MemoPrise.csproj -c Release -r win-x64 --self-contained true -o distribution/MemoPrise -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
- `MemoPrise.exe --screenshot` : base temporaire isolée, captures des écrans et vérification des boutons du rappel. Ne modifie pas le démarrage Windows.

Le programme et l'installateur ne sont pas signés numériquement. La vérification automatisée couvre la planification, l'historique, la persistance, sauvegarde/restauration, les actions du rappel et le parcours complet de l’assistant, avec quantités différentes et plusieurs changements de période. Les anciens traitements et sauvegardes de versions 1.0 et 1.1 restent lisibles. Les nouvelles sauvegardes de version 1.2 nécessitent cette version. La veille réelle, une session verrouillée et le démarrage après redémarrage Windows nécessitent une vérification sur le PC d'utilisation.

## Interface et suppression (version 1.3)

Le thème utilise des tons bleus pastels, des dégradés et des boutons arrondis. L’écran actif est indiqué dans le menu. Le bouton **Supprimer** de la boîte à pharmacie retire le médicament après confirmation et arrête ses prochains rappels. L’historique des prises passées et les validations déjà enregistrées sont conservés dans le calendrier.
La distribution de cette application est prévue gratuitement. Les libellés de son interface restent fonctionnels, sans slogans promotionnels.

Version 1.3.2 : pour une posologie par périodes, aucune date de fin globale n’est affichée dans la saisie commune. Les périodes intermédiaires ont une durée ; seule la dernière propose de continuer sans limite ou de choisir la date de fin du traitement. Pour une posologie fixe, la date de fin facultative reste disponible.

Version 1.3.3 : après une courte pause dans la saisie de la durée ou le choix d’une date, la page défile pour montrer le nombre de prises à renseigner. Le champ actif est conservé et aucun défilement ne se produit pour une durée invalide ou pendant que le calendrier est ouvert.

Version 1.3.4 : un clic hors du champ de durée déclenche immédiatement le défilement, même sur une zone vide ou un texte, et même si le nombre était déjà renseigné. Le numéro de version est affiché dans le titre de la fenêtre principale.

Version 1.3.5 : défilement automatique doux sur 1,2 seconde ; boutons adaptés à leur texte et champs de quantité compacts, même avec une fenêtre maximisée.

Version 1.3.6 : le défilement après une durée se limite au début de la période suivante, titre et dates visibles. Le choix d’une date conserve le début de la période concernée.

Version 1.3.7 : icône de caducée bleue intégrée à l’exécutable, aux fenêtres, à la barre des tâches, à la zone près de l’horloge et aux raccourcis. Source vectorielle dans MemoPrise/Assets/Caducee.xaml ; régénération via BuildIcon.ps1.

Version 1.3.8 : dans Aujourd’hui et Calendrier, les médicaments sont regroupés par horaire sous un bandeau bleu indiquant l’heure et le nombre de prises validées. Chaque médicament garde sa quantité, son état et ses boutons individuels.

Version 1.3.9 : nom MémoPrise aligné sur la même ligne que le caducée dans le menu ; suppression du sous-titre.

Version 1.3.10 : le rappel affiche « À prendre maintenant : » au-dessus des médicaments à prendre.

Version 1.3.11 : bouton « Ajouter une prise » dans chaque période suivante. Chaque période peut prévoir un nombre de prises différent (par exemple une, puis deux, puis trois), avec ses horaires et quantités. Le sélecteur permet aussi de réduire ce nombre.

Version 1.3.12 : poubelle à côté des prises modifiables des périodes, avec confirmation et bouton Annuler. La suppression conserve les autres horaires et quantités. Une période conserve au moins une prise.

Version 1.4.0 : agencement et ergonomie revus. Prises plus compactes, quantités et états distincts, actions principales en bleu, progression de la journée, calendrier à côté des détails dans une fenêtre large, paramètres en sections et assistant de largeur limitée avec progression. Les couleurs bleues pastels, la validation individuelle et le défilement doux sont conservés.

Version 1.4.1 : suppression du bouton Tester le rappel dans Paramètres. Toutes les compilations utilisent distribution/MemoPrise et distribution/Installer-MemoPrise.exe, sans dossiers de versions ni dossiers de préparation dupliqués.

Nettoyage des anciens exemplaires : double-cliquer sur Nettoyer-anciennes-versions.cmd. Le script conserve le dossier principal, les sources et les données locales. Pour une prévisualisation sans suppression, utiliser Nettoyer-anciennes-versions.ps1 -WhatIf.
