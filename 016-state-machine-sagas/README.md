# Sample 016 — State-Machine Sagas

Companion to **[Article 016 — State-Machine Sagas](../../docs/articles/016-state-machine-sagas.md)**.

The same order saga as article 015, authored **declaratively** with the state-machine DSL
(`Initially` / `During` / `When().Then().Schedule().TransitionTo().Finalize()`). The framework proves the
declarative and imperative forms are behaviourally identical; the DSL makes the state graph explicit. The
saga records a `CurrentState` ("Submitted") in addition to its data. DB-free in-memory harness.

## Run it

```bash
dotnet test samples/016-state-machine-sagas/Sagas.StateMachine.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
