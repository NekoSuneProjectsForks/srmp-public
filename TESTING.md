# SRMP Revival — two-client regression checklist

Use two legitimate Slime Rancher 1 x64 installations/users with the same SRMP and SRML versions. Prefer a fresh test save first, then repeat the important cases on an existing save.

## Baseline

- [ ] Both clients launch with SRML and SRMP without exceptions.
- [ ] Multiplayer UI appears at 1920×1080 and at a smaller resolution such as 1366×768.
- [ ] Host creates a session; client joins; both can move and see each other.
- [ ] Save, exit, relaunch and reconnect.

## Fresh-install / initialization

- [ ] On a genuinely fresh SRMP install, do **not** open Manage DLCs before testing.
- [ ] Slimes eat normally on first launch; no restart workaround is required.
- [ ] DLC entitlement/state is correct immediately.
- [ ] Client leaves and rejoins; DLC state remains correct.

## Ranch replication

- [ ] Put several plorts/items in collector range; collector pulls the correct objects for host and client.
- [ ] Run the collector repeatedly while both users interact with the corral.
- [ ] Nutcracker emits the exact expected count and object type on both clients.
- [ ] Feed several slime species and largos; remote emotion state is consistent.
- [ ] Verify normal plort production and favorite-food double-plort behavior.
- [ ] Pop at least two Gordo types; all expected rewards appear for host and client.

## Gadgets / drones / performance

- [ ] Place and remove gadgets repeatedly while watching frame-time, not only average FPS.
- [ ] Place/program/refill drones from host and client where supported.
- [ ] Let drones run for at least 20–30 minutes across cell transitions.
- [ ] Sleep/fast-forward time and verify drones recover normally.
- [ ] Leave/rejoin while drones are active and confirm they resume correctly.

## Chat / player state

- [ ] Host → client chat: at least 20 messages including short, long, punctuation and Unicode text.
- [ ] Client → host chat: same matrix; no empty messages and no delayed chat re-open after send.
- [ ] Apply each relevant player upgrade on host and verify remote state.
- [ ] Apply upgrades with client connected, then reconnect and verify persistence.

## Stress / save integrity

- [ ] Two players exchange rewards at nearly the same time.
- [ ] Enter/leave ranch cells repeatedly.
- [ ] Save during an active session, exit normally, reload and reconnect.
- [ ] Verify no duplicated/lost inventory caused by reconnect.
- [ ] Check SRML/SRMP logs after every failure and preserve the first exception plus preceding network events.

## Pass rule

Do not call the revival “all known bugs fixed” until every historical item has a reproducible pass on at least two consecutive clean runs. Intermittent defects should get a GitHub issue with logs and exact reproduction steps.
