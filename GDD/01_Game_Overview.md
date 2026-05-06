# 01 - Game Overview

## Inspiration

The game is inspired by classic **Xonix** and its 3D-style variant **AirXonix**. The player moves around a rectangular arena, draws paths through dangerous unclaimed territory, and captures areas while avoiding moving balls.

## Project Goal

Create a playable Unity prototype of a 3D Xonix-style game with:

- A rectangular arena
- Claimed and unclaimed territory
- A player moving smoothly in four directions while snapping to grid lines
- Two scripted bouncing balls
- Temporary path drawing
- Area capture using grid/flood-fill logic
- Lives and respawn system
- Level completion based on captured percentage

Graphics, animations, menus, and advanced visual polish are not the first priority. The first goal is to make the gameplay system work correctly.

## Target Platform

Initial target:

- PC build
- Unity Editor play mode

## Core Gameplay Loop

1. Player starts on claimed safe territory.
2. Player enters unclaimed territory and begins drawing a temporary path.
3. Balls continue moving inside unclaimed territory.
4. If a ball touches the temporary path, the player loses one life and the ball bounces away.
5. If a ball touches the player while the player is vulnerable, the player loses one life.
6. If the player returns to claimed territory, the temporary path closes.
7. The game calculates which region can be captured.
8. Captured territory becomes safe territory.
9. When the required capture percentage is reached, the level is completed.

## Design Pillars

### 1. Simple Controls

The player moves only in four directions: up, down, left, and right.

Movement should feel smooth, while the gameplay logic remains snapped to grid lines/cell centers.

### 2. Clear Risk and Reward

Leaving safe territory is risky because the temporary path can be hit by balls. Capturing larger areas gives faster progress but increases danger.

### 3. Grid-Based Gameplay, 3D Presentation

The logic is grid-based, but the game is displayed in a 3D scene.

### 4. Readability First

During development, gameplay clarity is more important than visual beauty.

## Initial Prototype Scope

The first prototype should include:

- One level
- One player
- Two balls
- Claimed border
- Unclaimed center area
- Path drawing
- Area capture
- Health/lives
- Fixed camera
- Basic UI for lives, rounded captured percentage, and required capture percentage
