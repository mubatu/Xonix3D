# 05 - Level Design

## Level 1 Overview

It contains:

- One rectangular arena
- Claimed outer border
- Unclaimed center area
- One player starting at the bottom border
- Four normal balls and three EaterBalls moving inside the unclaimed area
- Three lives
- 60 second timer
- Level completion by capture percentage

## Level 1 Recommended Settings

```text
Grid width: 40
Grid height: 40
Cell size: 1
Initial lives: 3
Required capture percentage: 75%
Timer: 60 seconds
Normal ball count: 4
EaterBall count: 3
```

These numbers can be changed after testing.

## Initial Territory Layout

The outer border is claimed. The inside is unclaimed.

The claimed outer border counts toward captured percentage from the start of the level.

Example:

```text
C C C C C C C C C C
C U U U U U U U U C
C U U U U U U U U C
C U U U U U U U U C
C U U U U U U U U C
C C C C C C C C C C
```

## Player Spawn

Recommended Level 1 spawn:

```text
Bottom center of claimed border
```

Example:

```text
spawnCell = (width / 2, 0)
```

The player should respawn here after death.

## Ball Spawns

Level 1 currently contains seven balls: four normal balls and three EaterBalls.

Runtime balls should be spawned or configured from a shared `Ball` prefab. The level JSON defines how many balls exist and their per-level type, spawn, direction, speed, hit radius, player hit radius, and ground offset.

Suggested positions:

```text
Normal balls: distributed around the unclaimed center
EaterBalls: placed away from the player spawn so the player has a short opening route
```

Example:

```text
Normal spawn: (width * 0.25, height * 0.25)
Eater spawn: (width * 0.70, height * 0.45)
```

## Level 2 Overview

Level 2 keeps the rectangular arena but adds a claimed block in the center.

It contains:

- Claimed outer border
- Claimed center block
- Three normal balls
- 60 second timer
- Player starting at the bottom border
- Level completion by capture percentage

## Level 3 Overview

Level 3 introduces EaterBalls.

It contains:

- Claimed outer border
- Claimed center block
- Three EaterBalls
- 60 second timer
- Player starting at the bottom border
- Level completion by capture percentage

EaterBalls should be configured with:

```json
"ballType": "Eater"
```

Level 3 tests the player's ability to keep claiming territory while EaterBalls break claimed cells back into unclaimed territory. Each EaterBall contact with claimed territory destroys up to two claimed cells and then bounces away.

## Future Level Ideas

Possible future additions:

### More Balls

Increase the number of ball entries in the level JSON to make area capture more difficult.

### Faster Balls

Increase speed per level if needed. Ball speed should not increase dynamically as captured percentage rises.

### Shorter Timers

Lower `timerSeconds` in level JSON to create pressure without changing ball count or speed.

### Enemies in Claimed Territory

Later, special enemies can move inside claimed territory so the player is not fully safe.

### More Ball Types

Add additional `ballType` values in level JSON when a new ball uses the same prefab but different gameplay rules.

### Obstacles

Add blocks or walls inside the unclaimed area.

### Different Arena Shapes

Instead of only rectangles, use levels with blocked cells and different starting safe zones.

### Power-Ups

Possible power-ups:

- Slow balls
- Freeze balls
- Extra life
- Temporary shield
- Instant small capture
