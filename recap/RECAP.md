# Récap — Le score et les services en ligne

*King's Optimization est construit en trois parties. Voici, en clair, la partie qui calcule la note de
l'ordinateur et qui fait tourner les services en ligne.*

## À quoi sert cette partie

C'est le « cerveau » qui note un ordinateur et le serveur en ligne du logiciel. Elle calcule une note de
performance, décide quels réglages sûrs proposer, et fait tourner les services en ligne (classement,
comptes, mises à jour). Elle ne touche jamais l'ordinateur de l'utilisateur : elle calcule et fournit des
informations ; c'est une autre partie du logiciel qui applique réellement les réglages.

## Ce que ça fait concrètement

- Donne à un ordinateur une note de performance sur 100, détaillée par domaine (carte graphique, processeur, mémoire, etc.).
- Montre l'écart entre la note actuelle et la note « bien réglé », pour voir la marge de progression.
- Choisit une liste de réglages sûrs à proposer, sans combinaisons qui se contredisent.
- Prépare, en option, une proposition d'« overclocking » (pousser un peu le matériel pour gagner en performance) — uniquement quand le risque est quasi nul ; sinon, rien.
- Tient un classement en ligne entre joueurs.
- Recalcule toujours la note côté serveur pour empêcher la triche : impossible de s'inventer un meilleur score.
- Gère les comptes et les licences (version gratuite / version payante).
- Distribue les « paquets de réglages » mis à jour, de façon vérifiable.

## Ce qui est fini et fonctionne

- Le calcul de la note marche, et donne **toujours le même résultat** pour le même ordinateur — indispensable pour un classement juste.
- La note est **graduée** : un ordinateur négligé tombe bas, un ordinateur bien réglé monte haut, et la note de 100 reste rare (il faut que tout soit optimal).
- Le choix des réglages et la proposition d'overclocking prudent fonctionnent.
- Le serveur en ligne fonctionne : comptes, licences, classement anti-triche, distribution des paquets.
- Le tout est couvert par plus de quarante vérifications automatiques, toutes réussies. Une relecture critique a été menée pour chasser les bugs ; ceux qui ont été trouvés ont été corrigés.

## Ce qui reste à faire ou à décider

- La vraie liste des réglages reste à compléter et à valider par l'équipe sur de vrais ordinateurs. Pour l'instant, on a travaillé avec quelques exemples sûrs.
- Les notes exactes ne sont pas encore figées : elles le seront une fois cette vraie liste prête.
- Le format précis des réglages doit être confirmé avec la partie du logiciel qui les applique (les deux doivent s'accorder avant de le verrouiller).
- La mise en ligne sur un vrai serveur et la signature officielle des mises à jour restent des gestes à faire par une personne, au moment du lancement.

## Les choses importantes à savoir

- Cette partie **ne modifie jamais** l'ordinateur de l'utilisateur. Elle calcule et fournit des informations ; l'application des réglages se fait ailleurs, avec une sauvegarde et la possibilité de tout annuler.
- La note du classement est **toujours recalculée côté serveur** : on ne peut pas tricher en envoyant un faux score.
- L'overclocking proposé reste **volontairement très prudent** : seulement quand le risque est quasi nul, sinon rien.
- Les informations sensibles (identités, clés) ne sont **jamais conservées en clair**.
