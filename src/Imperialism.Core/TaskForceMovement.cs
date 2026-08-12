namespace Imperialism.Core;

/// <summary>The one-leg result of a strategic sailing command.</summary>
public sealed record TaskForceMovePlan(
    TaskForceId TaskForce,
    SeaZoneId From,
    SeaZoneId RequestedDestination,
    SeaZoneId ResolvedDestination,
    long MaximumSeaZones);

/// <summary>One sailing leg applied to every member of a task force.</summary>
public sealed record TaskForceMoveResolution(
    TaskForceId TaskForce,
    SeaZoneId From,
    SeaZoneId To);
