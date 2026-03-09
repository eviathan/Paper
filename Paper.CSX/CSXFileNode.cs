namespace Paper.CSX
{
    public class CSXFileNode
    {
        public string Path { get; set; } = "";
        public string Namespace { get; set; } = "";
        public List<CSXComponent> Components { get; } = new();
    }
}