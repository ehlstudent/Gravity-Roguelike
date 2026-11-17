# Gravity-Roguelike

Ein 2D-Plattformer-Roguelike in Unity, in dem du die **Schwerkraft drehst**. Mit einem Tastendruck rotiert die Gravitation um 90°. Die Kamera dreht sich mit, sodass es aussieht, als würde sich der ganze Raum drehen. Böden werden zu Wänden, Wände zu Decken, und so erreichst du Stellen, an die du sonst nie kommen würdest.

Die Höhlen werden prozedural aus Seeds generiert und anschließend darauf geprüft, dass sie mit Gravitationswechseln auch wirklich schaffbar sind.

---

## Steuerung

| Aktion | Tastatur / Maus | Gamepad |
|---|---|---|
| Laufen | `A` / `D` oder `←` / `→` | Linker Stick |
| Springen | `Leertaste` | `A` / `×` (Süd-Taste) |
| Angreifen (Schwert) | Linke Maustaste oder `Enter` | `X` / `□` (West-Taste) |
| Zielen | Maus | – |
| Schwerkraft nach links drehen | `Q` | Linke Schultertaste |
| Schwerkraft nach rechts drehen | `E` | Rechte Schultertaste |
| Neue Welt generieren (Demo) | `R` | – |

Die Schwerkraft lässt sich **höchstens einmal pro Sekunde** drehen (Cooldown).

**Sprunggefühl:** Coyote Time (kurz nach dem Verlassen einer Kante darf man noch springen), Jump Buffer (ein Sprung kurz vor der Landung wird gemerkt) und variable Sprunghöhe (früh loslassen = kürzerer Sprung).

---

## Features

### Gravitation
- Drehung in 90°-Schritten, global über `Physics2D.gravity`
- Spieler richtet sich automatisch an der neuen Schwerkraft aus, die Kamera dreht sich weich mit
- Gegner reagieren auf Gravitationswechsel (Event `GravityRotator.GravityChanged`)
- Optional: Drehen nur am Boden erlaubt (`onlyWhenGrounded` im Inspector)

### Prozedurale Welt
- Seed-basierte Raumgenerierung: Noise → Cellular Automata → Region-Cleanup → Ränder
- Zufällige Raumplatzierung zu einer zusammenhängenden Welt
- **Gravity-Reachability-Check:** Ein Bewegungssimulator prüft, ob Start und Ziel mit Gravitationswechseln erreichbar sind. Wenn nicht, wird der Raum repariert oder neu generiert.
- Start, Ziel und Spawns werden auf festem Boden platziert
- Fester Seed möglich (`manualSeed` in `ProceduralGameplayDemo`)

### Kampf
- Schwert mit abwechselnden Schlägen, Zielen mit der Maus
- Schadenssystem mit Rückstoß (Knockback)
- Lebenspunkte für Spieler und Gegner

### Gegner
- **Walker:** läuft am Boden, verfolgt den Spieler in Sichtweite, springt über kleine Kanten
- **Fledermaus:** hängt ruhig, bis die Schwerkraft gedreht wird – dann wacht sie auf und jagt dich
- **Enemy Spawner** mit Seeds und einstellbaren Spawn-Regeln

### Items
| Item | Wirkung |
|---|---|
| Herz | +1 Leben (bleibt liegen, wenn du volle HP hast) |
| Max-HP-Kristall | +1 maximale Lebenspunkte |
| Schadens-Splitter | +1 Schwertschaden |
| Schnelle Stiefel | +15 % Laufgeschwindigkeit (maximal 1,6× Grundtempo) |

Items haben Seltenheitsstufen (Common, Uncommon, Rare) und werden über eine Loot-Tabelle verteilt.

---

## Projekt starten

**Voraussetzung:** Unity **6000.6.7f1** (Unity 6). Andere Versionen können funktionieren, sind aber nicht getestet.

1. Repository klonen:
   ```bash
   git clone https://github.com/ehlstudent/Gravity-Roguelike.git
   ```
2. Im Unity Hub über **Add → Add project from disk** den Ordner öffnen.
3. Eine Szene aus `Assets/Scenes/` öffnen und **Play** drücken.

### Szenen

| Szene | Zweck |
|---|---|
| `ProceduralGameplayDemo` | Hauptdemo: generierte Welt, Spieler, Gegner, Items, Ziel |
| `DemoShowcase` | Feste Showcase-Route zum Vorführen |
| `EnemyTestingGround` | Testgelände für Gegner und Spawner |
| `SwordCombatPreview` | Test des Schwertkampfs |
| `room0`, `room1` | Handgebaute Räume, über Portale verbunden |
| `TilemapPreview`, `BoundsPreview` | Debug-Ansichten für Tilemap und Weltgrenzen |

---

## Projektstruktur

```
Assets/
├── Scripts/
│   ├── GravityRotator.cs          # Dreht die Schwerkraft (Q/E), Cooldown
│   ├── movedulli_tiletester.cs    # PlayerController2D: Bewegung, Sprung, Wand-Handling
│   ├── cam_follow_player.cs       # Kamera folgt dem Spieler und dreht sich mit
│   ├── Portal.cs, SpawnPoint.cs   # Szenenwechsel
│   ├── Combat/                    # Schwert, Health, Schaden, Gegner-Basisklasse
│   ├── Enemies/                   # Walker, Fledermaus, Verfolgung, Pathfinding, Spawner
│   ├── Items/                     # Item-Typen, Effekte, Loot-Tabelle, Platzierung
│   └── World/                     # Prozedurale Generierung und Gravity-Erreichbarkeit
├── Tests/Editor/                  # EditMode-Tests
├── Prefabs/                       # Spieler (Dulli_roomtester), Gegner, Items
└── Scenes/
```

---

## Tests

Die Generierung und die Gravity-Logik sind mit EditMode-Tests abgesichert, u. a. für Bewegungssimulator, Erreichbarkeitssuche, Raum-Reparatur, Item-Platzierung und Schwertkampf.

In Unity: **Window → General → Test Runner → EditMode → Run All**.

---

## Entwicklung

- Neue Features auf eigenen Branches (`feature/...`) entwickeln und per Pull Request mergen
- Physik-Einstellungen des Spielers (Wall Handling, Ground Check, Sprung) sind im Inspector am Spieler-Prefab einstellbar
- Erstellt mit **C# / Unity 6** und dem **neuen Input System**

