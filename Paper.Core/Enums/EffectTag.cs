namespace Paper.Core.Enums
{
    /// <summary>
    /// What the reconciler needs to do with this fiber during the commit phase.
    /// </summary>
    public enum EffectTag
    {
        None,
        /// <summary>New fiber — insert into the tree.</summary>
        Placement,
        /// <summary>Existing fiber — update props/state.</summary>
        Update,
        /// <summary>Fiber removed from the new tree — clean up and destroy.</summary>
        Deletion,
    }
}