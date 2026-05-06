# 06 - Art, UI, and Audio

## Visual Direction

The first version should prioritize clarity over beauty.

The visual style can later move toward a simple 3D arcade look inspired by AirXonix.

## Ground Visuals

The ground has three visual states:

| Territory | Visual |
|---|---|
| Claimed | Smooth green area |
| Unclaimed | Smooth gray area |
| Temporary path | Visible square tiles |

## Development Visual Strategy

### Prototype Stage

Use individual square tiles for everything.

Advantages:

- Easy to debug
- Easy to see grid state
- Fast to implement

### Improved Stage

Use a smooth single mesh for the ground.

Suggested approach:

- Claimed area mesh
- Unclaimed area mesh
- Temporary path tiles remain separate and visible

### Polish Stage

Add:

- Glow effect on temporary path
- Smooth capture animation
- Ball trail effects
- Small explosion when player dies
- Particle effect when territory is captured

For the first implementation, captured territory can switch state instantly. Smooth capture animation is polish, not a prototype requirement.

## Player Visual

Prototype:

- Capsule

The player should visually feel like it moves above the ground, not like a rolling ball.

## Ball Visual

Prototype:

- Sphere mesh with colored material

Later:

- Rolling animation
- Trail effect
- Reflection or shine
- Distinct colors for different ball types

## Camera

Initial camera:

- Fixed camera
- Angled top-down view
- Similar to classic AirXonix

Possible camera modes later:

- Orthographic fixed camera
- Perspective fixed camera
- Smooth follow camera
- Slight cinematic tilt

## UI Requirements

Initial UI should show:

- Lives
- Rounded captured percentage
- Required capture percentage
- Level number

Example UI:

```text
Lives: 3
Level: 1
Captured: 24% / 75%
```

## Game State UI

### Level Complete

Show:

```text
Level Complete!
```

Possible buttons later:

- Next Level
- Restart
- Main Menu

### Game Over

Show:

```text
Game Over
```

Possible buttons later:

- Retry
- Main Menu

## Audio

Audio is skipped in the earliest prototype.
