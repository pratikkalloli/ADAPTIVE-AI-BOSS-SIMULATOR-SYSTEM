# Stage 1.8: Player attack hit detection

## Root cause

The supplied Play Mode logs report one accepted player hit (boss HP `1500 -> 1425`), repeated out-of-range rejections at reported root-to-root distances around `0.5–2.5`, then player death around `t=20.280`. Those logs establish that `canAttack` was false for those input attempts; root-to-root distance alone does not establish overlap of the actual attack-range collider.

Source and KnightScene YAML inspection found the target-filter defect. `PlayerCombat.IsBossAttackCollider()` previously accepted only colliders carrying `EnemyCombat` directly. The scene's active enemy colliders on `Jaw01` and `R_Hand` satisfy that filter, but they are the boss's attack hitboxes. KnightScene also has an enabled trigger `BoxCollider` on the body mesh object `NightMareDragon` (`m_Enabled: 1`, `m_IsTrigger: 1`), whose scene instance is tagged `Enemy`; it does not carry `EnemyCombat`, so the player's hand trigger ignored the body target. The scene collider is serialized at `KnightScene.unity` file ID `1097692080`, on object `1097692077` (prefab source object `175674`). The Jaw and claw hitboxes have `EnemyCombat` and enabled trigger box colliders at scene file IDs `608761482` and `2076925395`.

No overlap query, radius, or layer mask is used in the old code. Range is an event-maintained set of colliders overlapping the enabled player hand `BoxCollider` (`KnightScene.unity` file ID `1336130676`; trigger enabled). Thus the player-boss distance values in the supplied logs were not the range test. The body collider and both combatants' physics-body setup are present; the scene has a kinematic player Rigidbody on the player root and an active boss Rigidbody on its Animator root. The inspected scene and prefab do not show the body collider disabled.

## Attack input lifecycle

The existing path is `PlayerCombat.Update()` → `PlayerCombat.PlayerAttack()` → `PlayerMovement.PlayerAttack()` → set either Animator trigger `attack` or `attack_2` on the `Attack layer` → `CombatManager.DragonTakeDamage()` on the same input update. Damage is input-timed; the player attack clips have no impact-frame damage event.

The player Animator controller defines the `attack` and `attack_2` trigger parameters and the matching states on the attack layer. `PlayerMovement.PlayerAttack()` deliberately refuses to start a new trigger while either attack state is active. The `attack animation already active` rejections are therefore expected: clicks during the attack animation are discarded, not queued. Before this fix, however, the order was animation first, range check second. An out-of-range click could start an attack animation and then reject damage, leaving subsequent clicks blocked until the state ended.

## Fix applied

- `PlayerCombat.IsBossAttackCollider()` now accepts either the existing direct `EnemyCombat` colliders or the scene's named `NightMareDragon` body collider, while retaining the root `Enemy` tag check. It still uses the existing player's hand trigger overlap as the range boundary; it does not accept arbitrary boss descendants or root-distance alone.
- `PlayerCombat.PlayerAttack()` now handles only a fresh left-click, checks the active-animation guard and current range before asking `PlayerMovement` to trigger an attack. Out-of-range clicks do not start/consume an attack animation. An in-range click uses the existing animation selection and still applies the existing 75 damage once on that input update.
- Existing Stage 1.7 timestamps, attack sequence IDs, damage logs, shared boss swing gate, and boss behavior are preserved.

No scene, prefab, Animator asset, damage amount, boss attack timing, movement setting, package, or Python file was changed.

## Files changed

- `Assets/Scripts/PlayerCombat.cs`
- `STAGE1_8_PLAYER_ATTACK_FIX.md` (this report)

Earlier uncommitted Stage 1–1.7 work was present before this task and remains untouched outside the player attack changes described above.

## Validation and limitations

`git diff --check` and final source diff review were run. No Unity Editor, C# build, automated test, or Play Mode session was run here. The root cause is verified from the filter and serialized scene references; actual collision overlap and the fix's runtime behavior still require Play Mode verification.

The body collider is a broad trigger, but using it is bounded by overlap with the player's existing hand trigger and the boss-root tag check. The exact feel/range of that hand trigger should be checked visually in Play Mode. Clicks while the player Animator remains in `attack` or `attack_2` remain intentionally unqueued.

## Unity Play Mode verification

1. Open `Assets/Scenes/KnightScene.unity` in Unity 2021.3.15f1. In the Hierarchy, verify the player attack trigger and boss body/Jaw/R_Hand colliders are enabled. Keep the Console open with Collapse disabled.
2. Enter Play Mode and approach the boss until the player's right-hand attack trigger visibly overlaps the `NightMareDragon` body collider, away from the Jaw/R_Hand attack hitboxes if practical. Click once. Expect `PlayerAttackAccepted` followed by `PlayerDamageApplied`, with boss HP decreasing exactly `75`.
3. Move outside the hand-trigger/body-collider overlap and click. Expect `PlayerAttackRejected` with the range reason, no attack animation trigger, and no boss HP change.
4. Click once while an attack animation is visibly active. Expect the active-animation rejection and no extra damage. Wait for the attack state to finish, then click in range again; expect the next attack to be accepted.
5. Repeat using Jaw and R_Hand overlap. Confirm their existing collider targets still work and no single click applies more than one player damage event.
6. Check that boss accepted-hit/gate logs and HP remain consistent with one shared swing ID and that no Console exceptions appear. Save the complete `[CombatDiag]` output from the run.
