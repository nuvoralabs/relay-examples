# Sample 032 — Automated Blue/Green Projection Rebuilds

Companion to **[Article 032 — Automated Blue/Green Projection Rebuilds](https://relay.nuvoralabs.com/articles/automated-blue-green-rebuilds/)**.

**Prerequisite:** [Article 008 — Projection Operations](https://relay.nuvoralabs.com/articles/projection-operations/), which
introduces the shadow (blue/green) rebuild and the **operator-driven** swap (`IProjectionRebuildManager.SwapAsync`).

This sample closes that loop. `IAutomaticProjectionSwapper` / `AutomaticProjectionSwapper`
(in `Nuvora.Nexus.Relay.Projections.Rebuild`) drives a shadow rebuild and **swaps the read model in
automatically** the moment the rebuild reports `Completed` — no human watching a progress bar:

- Starts a **shadow** rebuild (`StartRebuildAsync` with `RebuildMode.Shadow`).
- **Polls** `GetProgressAsync` on an interval.
- On `ProjectionRebuildStatus.Completed`, calls `SwapAsync` (atomic blue→green promotion) and returns.
- On `Failed`, `Cancelled`, or a missing rebuild row, it returns **without swapping** — the live read
  model is left untouched.

**No database** — this sample fakes the rebuild manager to show the auto-swap policy. The real manager
drives a shadow read model against PostgreSQL; the policy under test only cares about the sequence of
statuses progress reports and whether it swaps at the right moment, so a scripted
[`FakeRebuildManager`](./Projections.AutoSwap.Tests/FakeRebuildManager.cs) replaces it. The swapper takes
a `pollInterval`, set tiny here so the loop turns over fast and deterministically.

| File | Shows |
|---|---|
| [`AutomaticProjectionSwapTests.cs`](./Projections.AutoSwap.Tests/AutomaticProjectionSwapTests.cs) | starts + auto-swaps on Completed; never swaps while Running / Failed / Cancelled; defaults to Shadow |
| [`FakeRebuildManager.cs`](./Projections.AutoSwap.Tests/FakeRebuildManager.cs) | scripted in-memory `IProjectionRebuildManager` |

## Test it

```bash
dotnet test samples/Relay.Samples.slnx
```

Or just this sample:

```bash
dotnet test samples/032-automated-blue-green-rebuilds/Projections.AutoSwap.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
