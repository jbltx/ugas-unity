namespace Jbltx.Ugas.Networking
{
    // =====================================================================================
    // NETWORKING & PREDICTION — EXPLICIT NON-GOAL (for now)
    //
    // This pillar is intentionally a documented stub. A faithful implementation of client
    // prediction and server reconciliation (SPEC §13) depends on open spec questions that
    // must settle first:
    //
    //   TODO(jbltx/ugas#4, #5, #7): networking & prediction
    //     - jbltx/ugas#4 — networking model underspecified
    //     - jbltx/ugas#5 — deterministic randomness
    //     - jbltx/ugas#7 — ability cancellation race during validation
    //
    // Until those resolve, the package ships single-authority (no replication). The interface
    // below sketches the eventual surface so callers can see the intended shape, but it has no
    // implementation and must not be relied upon.
    // =====================================================================================

    /// <summary>
    /// Placeholder for the future networking pillar (SPEC §13): client prediction + server
    /// reconciliation. <b>Not implemented.</b> Blocked on jbltx/ugas#4, #5, #7.
    /// </summary>
    public interface INetworkReplication
    {
        // TODO(jbltx/ugas#4, #5, #7): networking & prediction.
        // Intended surface (subject to change once the spec settles):
        //   void PredictLocally(...);
        //   void ServerReconcile(...);
        //   GCReplicationMode Mode { get; }
    }
}
