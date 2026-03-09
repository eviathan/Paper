namespace Paper.CSX.Attributes
{
    /// <summary>
    /// Marks a method as containing CSX template code. The method should return void
    /// and contain CSX syntax.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CSXAttribute : Attribute
    {
    }
}