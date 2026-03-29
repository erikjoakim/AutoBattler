# Scene Markers

## Add a StartAreaMarker

1. In the Unity Hierarchy, create an empty GameObject.
2. Name it something like `PlayerStartArea` or `EnemyStartArea`.
3. Add the `StartAreaMarker` component.
4. Set `Team` to `Blue` for the player side or `Red` for the enemy side.
5. Adjust the object's position to place the area on the terrain.
6. Rotate the object so its forward direction points toward the battlefield.
7. Set the `Size` field to control the spawn area dimensions.
8. Optional: set `Priority` if you use multiple start areas for the same team.

The start area is shown with editor gizmos in the Scene view and is not rendered during gameplay.

## Add a VictoryPointMarker

1. In the Unity Hierarchy, create an empty GameObject.
2. Name it something like `CentralHill` or `BridgeObjective`.
3. Add the `VictoryPointMarker` component.
4. Position it where the objective should be captured.
5. Set `Capture Radius` to the size of the capture zone.
6. Set `Capture Time` to how long a team must hold it uncontested.
7. Set `Initial Owner` to `Red`, `Blue`, or `Neutral`.
8. Enable `Required For Victory` if capturing this point is needed to win.
9. Leave `Visible In Game` enabled if you want the runtime marker ring to appear during play.

Victory points are shown in the editor with gizmos, and during play they update ownership and capture progress automatically.
