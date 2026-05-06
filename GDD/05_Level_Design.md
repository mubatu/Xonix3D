# 05 - Level Design

## Level 1 Overview

It contains:

- One rectangular arena
- Claimed outer border
- Unclaimed center area
- One player starting at the bottom border
- Two balls moving inside the unclaimed area
- Three lives
- Level completion by capture percentage

## Level 1 Recommended Settings

```text
Grid width: 40
Grid height: 40
Cell size: 1
Initial lives: 3
Required capture percentage: 75%
Ball count: 2
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

Level 1 contains two balls.

Suggested positions:

```text
Ball 1: left-middle area
Ball 2: right-upper area
```

Example:

```text
Ball 1 spawn: (width * 0.30, height * 0.60)
Ball 2 spawn: (width * 0.70, height * 0.70)
```

## Future Level Ideas

Possible future additions:

### More Balls

Increase ball count to make area capture more difficult.

### Faster Balls

Increase speed per level if needed. Ball speed should not increase dynamically as captured percentage rises.

### Enemies in Claimed Territory

Later, special enemies can move inside claimed territory so the player is not fully safe.

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
