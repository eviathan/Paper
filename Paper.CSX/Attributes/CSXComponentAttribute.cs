namespace Paper.CSX.Attributes
{
    /// <summary>
    /// Marks a class as containing CSX components. The source generator will
    /// process this class to generate C# code from CSX templates.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CSXComponentAttribute : Attribute
    {
    }
}