# Xonix3D

<img width="506" height="160" alt="xonix3d-text-cropped" src="https://github.com/user-attachments/assets/2f150665-3736-477e-8812-7b57270dc0ed" />

Xonix3D is a 3D version of the classic Xonix game.

The project is inspired by the original Xonix game. The main idea is the same: the player moves on a grid, draws paths, captures empty areas, and avoids moving balls.

This project was developed for CmpE 485.

## Game Rules

<img width="679" height="362" alt="in-game" src="https://github.com/user-attachments/assets/f4d5f027-415b-4b77-becb-e742c036a70e" />

- The player can move with `W`, `A`, `S`, `D` or the arrow keys.
- The player is safe while moving on claimed territory.
- While the player moves in unclaimed territory, orange temporary path tiles are created.
- If the player returns to claimed territory, the path is completed. After a completed path, the closed area becomes claimed territory.
- The goal is to capture the target area of the level before the time ends up.
- If a ball touches the player, the player loses one life.
- If a ball hits the temporary path, the path starts burning. If the burning path reaches the player before the path is completed, the player loses one life.
- If the player touches its own path, the player loses one life.
- When lives reach zero, the game is over.

## Ball Types

There are two ball types in the game:

- Normal balls move inside the unclaimed area and bounce from blocked cells.
- EaterBalls can damage claimed territory and turn some claimed cells back into unclaimed cells.

## Levels

The game includes 10 playable levels.

Each level can define:

- Grid size
- Required capture percentage
- Timer
- Player spawn point
- Initially claimed areas
- Ball count
- Ball type
- Ball speed and direction

## Technical Details

- Unity version: `2022.3.62f3`
- The game uses Unity PhysX for ball movement and collisions.
- Level data is loaded from JSON files.
- Path tiles use object pooling to reduce object creation during gameplay.
- Grid colliders are merged into larger BoxColliders for better performance.
- Area capture uses grid and flood-fill logic.

## Links

- [Presentation](https://docs.google.com/presentation/d/1bfSzp4inqZyApGtPA4ozm9qCQsJhpx_OQRzF3RttDoQ/edit?usp=sharing)
- [Report](https://github.com/mubatu/Xonix3D/blob/main/Report.pdf)
- [Gameplay video](https://drive.google.com/file/d/10KJ6QylwWiX28Uqw4KdwIAQzAlY7As2z/view?usp=share_link)

## Developer

Developed by Mustafa Batuhan Büber.
