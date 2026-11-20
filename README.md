# NightFall Sync

Plugin Dalamud du **NightFall**, cyberpunk bar de Neo-Alexandrie (Moogle · La Coupe · secteur 12 · parcelle 41).

Quand vous êtes au NightFall et qu'un lien **Watch2Gether** (w2g.tv) ou **Twitch** passe dans le chat, le plugin le met en ligne comme salon musical sur la carte du site [neo-nightfall.net](https://www.neo-nightfall.net/carte/).

## Installation

1. Dans le jeu, tapez `/xlsettings`, puis ouvrez l'onglet **Expérimental**.
2. Dans **Custom Plugin Repositories**, collez ce lien, cochez la case de la ligne, puis cliquez sur **+** et enregistrez :

   ```
   https://raw.githubusercontent.com/ixnkz/NightfallSync/main/repo.json
   ```

3. Tapez `/xlplugins`, cherchez **NightFall Sync** et installez-le.

## Utilisation

- `/nightfall` ouvre la fenêtre du plugin.
- Entrez le **code organisateur** du site. Il reste uniquement sur votre PC.
- En haut de la fenêtre, « ♥ Tu es au NightFall » s'affiche quand vous êtes dans la maison.
- Un lien w2g.tv ou twitch.tv posté dans le chat met alors le salon à jour. Le plugin le confirme dans le chat : « [NightFall] Salon du site mis à jour ».
- Vous pouvez limiter les personnes dont les liens comptent, changer l'adresse du bar ou envoyer un lien à la main.

## Compiler

Il faut le SDK .NET 10 et Dalamud installé (XIVLauncher) :

```
dotnet build NightfallSync/NightfallSync.csproj -c Release
```

Le paquet est créé dans `NightfallSync/bin/Release/NightfallSync/latest.zip`.
