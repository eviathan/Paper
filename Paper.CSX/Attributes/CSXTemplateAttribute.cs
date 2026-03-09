namespace Paper.CSX.Attributes
{
    /// <summary>
    /// Marks a property as a CSX template. The property should return string and
    /// contain CSX syntax.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class CSXTemplateAttribute : Attribute
    {
    }
}