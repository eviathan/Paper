namespace Paper.CSX
{
    public class CSXComponent
    {
        public string Name { get; set; } = "";
        public List<CSXMethod> Methods { get; } = new();
    }
}