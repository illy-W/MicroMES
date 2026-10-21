namespace MicroMES.ApiFeatures.PlanningFeature.Engine;

/// <summary>
/// A planning engine is a pure function: input state -> plan with reasons. No database, no clock (the input carries "now"),
/// deterministic (same input, same output). The input and output models do not depend on the implementation, so another
/// engine (e.g. an optimizer) can replace <see cref="ForwardFiniteCapacityEngine"/> and be compared with it.
/// </summary>
public interface IPlanningEngine
{
    PlanningResult Plan(PlanningInput input);
}
