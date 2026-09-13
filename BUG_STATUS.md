# SRMP Revival — bug status

This document separates the old build-1584 list from what can actually be established in the current source. The repository contains substantial fixes made after that list was written, so a historical item is not automatically still broken.

| Area | Historical report | Revival status |
|---|---|---|
| Multiplayer UI | Window missing below 1920×1080 | Fixed in later history; regression-test only |
| Exchange rewards | Simultaneous inputs could skip rewards | Fixed in later history; regression-test only |
| Exchange chest | Could disappear without rewards | Fixed in later history; regression-test only |
| World collision | Player could fall through world | Fixed in later history; regression-test only |
| Plort collector | Effect plays but item is not pulled | **Hardened in revival:** host-authority fallback during incomplete region init + non-owner StartCollection suppression; needs two-client verification |
| Nutcracker | Wrong quantity / invalid baby output | Dedicated current patch exists; exact-output test still required |
| Slime emotion | Slime may look angry only to remote player | Actor emotion/feral patches exist; remote-state test required |
| Gadget stutter | Placement/removal causes hitching | Requires real frame-time profiling before changing timing-sensitive code |
| First-run state | First use may break slime eating until restart | **Partially hardened:** packet guard can no longer stay stuck after early return/exception; fresh-profile test required |
| DLC reconnect | DLC state incorrect after leave/rejoin | Live host/client leave/rejoin test required |
| DLC initialization | False missing-DLC prompt before Manage DLCs | Fresh-launch entitlement test required |
| Gordos | Rewards sometimes missing | Gordo eat/snare networking exists; reward replication test required |
| Slime plorts | Slime occasionally fails to produce plorts | Eating/reproduction networking exists; replication test required |
| Drones | Drone can become stuck | Multiple drone/station/program patches exist; long two-client soak required |
| Chat | Remote player can receive empty chat | **Hardened in revival:** null/blank display rejected, Return-key focus race fixed, wrapping/render-state bugs fixed; two-client soak required |
| Player upgrades | Upgrade not applied to every player | Player-state packets exist; host/client upgrade matrix required |
| Packet state guard | Network handling can leave `Globals.HandlePacket` true | **Fixed in revival** with guaranteed Harmony finalizer cleanup on client and server handlers |
| Installation | Manual setup with no safe one-click path | **Implemented** with SRML detection, backup, hash check, manifest and rollback uninstall |

## Verification rule

Timing, replication, save/rejoin, remote-client and DLC behavior are only promoted to **verified fixed** after passing the two-client checklist in `TESTING.md`. Static source inspection alone is not enough to make that claim safely.
